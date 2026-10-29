using Godot;
using Godot.Collections;

public partial class PlayerPickup : Node3D, ISaveable
{
    [ExportGroup("Pickup Settings")]
    [Export(PropertyHint.Layers3DPhysics)] public uint PickupLayerMask = 1;
    [Export(PropertyHint.Layers3DPhysics)] public uint ObstructionLayerMask = 1;
    [Export] public float PickupRange = 1.5f;
    [Export] public float HoldDistance = 1.5f;
    [Export] public float MaxHoldDistance = 2.5f;
    [Export] public float PullForce = 250.0f;
    [Export] public float BaseThrowSpeed = 5.0f;
    [Export] public float MassSpeedPenalty = 0.5f;

    [ExportGroup("References")]
    [Export] public Camera3D PlayerCamera;
    [Export] public CollisionShape3D BodyCollider;

    private RayCast3D _pickupRay;
    private Node3D _holdPoint;
    private ShapeCast3D _obstructionCast;
    private RigidBody3D _heldObject;
    private bool _isHolding = false;
    
    private string _pendingRestoreHoldId = "";

    public override void _Ready()
    {
        if (PlayerCamera == null)
        {
            return;
        }

        InitializeHoldPoint();

        _pickupRay = new RayCast3D();
        _pickupRay.TargetPosition = new Vector3(0, 0, -PickupRange);
        _pickupRay.CollisionMask = PickupLayerMask;
        _pickupRay.CollideWithAreas = false;
        _pickupRay.CollideWithBodies = true;
        PlayerCamera.AddChild(_pickupRay);

        _obstructionCast = new ShapeCast3D();
        SphereShape3D sphere = new SphereShape3D();
        sphere.Radius = 0.2f;
        _obstructionCast.Shape = sphere;
        _obstructionCast.TargetPosition = new Vector3(0, 0, -HoldDistance);
        _obstructionCast.CollisionMask = ObstructionLayerMask;
        _obstructionCast.Enabled = false;
        PlayerCamera.AddChild(_obstructionCast);
    }

    private void InitializeHoldPoint()
    {
        if (_holdPoint == null || !IsInstanceValid(_holdPoint))
        {
            _holdPoint = new Node3D();
            _holdPoint.Position = new Vector3(0, 0, -HoldDistance);
            PlayerCamera.AddChild(_holdPoint);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("interact"))
        {
            if (_isHolding)
            {
                DropObject();
            }
            else
            {
                TryPickup();
            }
        }
        else if (@event.IsActionPressed("primary_attack") && _isHolding)
        {
            ThrowObject();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (PlayerCamera == null)
        {
            return;
        }

        if (!_isHolding || _heldObject == null)
        {
            return;
        }

        if (!IsInstanceValid(_heldObject))
        {
            _isHolding = false;
            _heldObject = null;
            _obstructionCast.Enabled = false;
            return;
        }

        float deltaTime = (float)delta;

        _obstructionCast.ForceShapecastUpdate();
        if (_obstructionCast.IsColliding())
        {
            _holdPoint.Position = _holdPoint.Position.Lerp(new Vector3(0, 0, -0.5f), deltaTime * 5.0f);
        }
        else
        {
            _holdPoint.Position = _holdPoint.Position.Lerp(new Vector3(0, 0, -HoldDistance), deltaTime * 5.0f);
        }

        if (IsTooFarAway())
        {
            DropObject();
            return;
        }

        CalculateDynamicDistance();

        Vector3 targetPosition = _holdPoint.GlobalPosition;
        Vector3 direction = targetPosition - _heldObject.GlobalPosition;
        float distance = direction.Length();

        if (distance > 0.1f)
        {
            float forceMultiplier = Mathf.Clamp(distance * PullForce, 0.0f, 500.0f);
            _heldObject.ApplyCentralForce(direction.Normalized() * forceMultiplier);
            
            _heldObject.LinearVelocity = _heldObject.LinearVelocity.Lerp(Vector3.Zero, deltaTime * 15.0f);
            _heldObject.AngularVelocity = Vector3.Zero;
        }
    }

    private bool IsTooFarAway()
    {
        if (_heldObject == null || !IsInstanceValid(_heldObject))
        {
            return true;
        }

        return _heldObject.GlobalPosition.DistanceTo(PlayerCamera.GlobalPosition) > MaxHoldDistance;
    }

    private void TryPickup()
    {
        _pickupRay.ForceRaycastUpdate();
        
        if (!_pickupRay.IsColliding())
        {
            return;
        }

        var collider = _pickupRay.GetCollider();
        
        if (collider is RigidBody3D rigidBody && collider is PhysicsObject && IsInstanceValid(rigidBody))
        {
            if (IsStandingOnObject(rigidBody))
            {
                return;
            }

            _heldObject = rigidBody;
            _heldObject.GravityScale = 0.0f;
            _heldObject.LinearDamp = 20.0f;
            _heldObject.AngularDamp = 20.0f;
            _heldObject.LockRotation = true;
            
            _isHolding = true;
            _obstructionCast.Enabled = true;
        }
    }

    private void DropObject()
    {
        if (_heldObject == null || !IsInstanceValid(_heldObject))
        {
            _heldObject = null;
            _isHolding = false;
            _obstructionCast.Enabled = false;
            return;
        }

        _heldObject.GravityScale = 1.0f;
        _heldObject.LinearDamp = 0.0f;
        _heldObject.AngularDamp = 0.0f;
        _heldObject.LockRotation = false;
        
        _heldObject.LinearVelocity = Vector3.Zero;
        _heldObject.AngularVelocity = Vector3.Zero;
        
        _heldObject = null;
        _isHolding = false;
        _obstructionCast.Enabled = false;
    }

    private void ThrowObject()
    {
        if (_heldObject == null || !IsInstanceValid(_heldObject))
        {
            return;
        }

        var throwTarget = _heldObject;
        float mass = throwTarget.Mass;
        
        DropObject();
        
        Vector3 forward = -PlayerCamera.GlobalTransform.Basis.Z;
        
        float massFactor = Mathf.Max(1.0f - (mass * MassSpeedPenalty / 10.0f), 0.1f);
        float finalSpeed = BaseThrowSpeed * massFactor;
        
        if (IsInstanceValid(throwTarget))
        {
            throwTarget.LinearVelocity = forward * finalSpeed;
        }
    }

    private void CalculateDynamicDistance()
    {
        Vector3 rayStart = PlayerCamera.GlobalPosition;
        Vector3 rayEnd = rayStart + (-PlayerCamera.GlobalTransform.Basis.Z * MaxHoldDistance);
        
        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(rayStart, rayEnd);
        query.CollisionMask = ObstructionLayerMask;

        var result = spaceState.IntersectRay(query);
        
        if (result.Count > 0)
        {
            float wallDistance = rayStart.DistanceTo((Vector3)result["position"]);
            float targetDistance = Mathf.Min(wallDistance - 0.3f, HoldDistance);
            targetDistance = Mathf.Max(targetDistance, 0.5f);
            _holdPoint.Position = new Vector3(0, 0, -targetDistance);
        }
    }

    private bool IsStandingOnObject(RigidBody3D body)
    {
        if (BodyCollider == null)
        {
            return false;
        }

        Vector3 down = Vector3.Down;
        Vector3 start = GlobalPosition;
        Vector3 end = start + (down * 0.2f);
        
        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(start, end);
        query.CollisionMask = PickupLayerMask;

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            return result["collider"].AsGodotObject() == body;
        }

        return false;
    }
    
    public RigidBody3D GetHeldObject()
    {
        if (_heldObject != null && IsInstanceValid(_heldObject))
        {
            return _heldObject;
        }

        return null;
    }

    public void ForceHoldObject(RigidBody3D obj)
    {
        if (obj == null || _isHolding || !IsInstanceValid(obj)) 
        {
            return;
        }
        
        InitializeHoldPoint();
        
        _heldObject = obj;
        _heldObject.GravityScale = 0.0f;
        _heldObject.LinearDamp = 20.0f;
        _heldObject.AngularDamp = 20.0f;
        _heldObject.LockRotation = true;
        
        _isHolding = true;
        _obstructionCast.Enabled = true;
    }

    public string GetSaveId()
    {
        return "PlayerPickup_ID";
    }

    public Dictionary CaptureState()
    {
        var state = new Dictionary
        {
            ["is_holding"] = _isHolding
        };

        if (_isHolding && _heldObject != null && IsInstanceValid(_heldObject) && _heldObject is ISaveable saveableObject)
        {
            state["held_object_id"] = saveableObject.GetSaveId();
            state["held_object_state"] = saveableObject.CaptureState();
            state["held_object_scene_path"] = _heldObject.SceneFilePath;
        }

        return state;
    }

    public void RestoreState(Dictionary state)
    {
        if (state.ContainsKey("is_holding"))
        {
            bool wasHolding = state["is_holding"].AsBool();
            if (wasHolding && state.ContainsKey("held_object_id"))
            {
                _pendingRestoreHoldId = state["held_object_id"].AsString();
            }
        }
    }

    public void OnRestoreComplete()
    {
        if (!string.IsNullOrEmpty(_pendingRestoreHoldId))
        {
            string heldId = _pendingRestoreHoldId;
            _pendingRestoreHoldId = "";

            var allSaveables = SceneManager.GetSaveableNodes(GetTree().CurrentScene);
            foreach (var node in allSaveables)
            {
                if (node is ISaveable saveable && saveable.GetSaveId() == heldId && node is RigidBody3D rb && IsInstanceValid(rb))
                {
                    ForceHoldObject(rb);
                    break;
                }
            }
        }
    }
}