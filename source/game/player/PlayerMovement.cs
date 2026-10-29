using Godot;
using Godot.Collections;

public partial class PlayerMovement : CharacterBody3D, ISaveable
{
    [Signal] public delegate void StaminaChangedEventHandler(float current, float max);

    [ExportGroup("Movement Settings")]
    [Export] public float WalkSpeed = 4.0f;
    [Export] public float SprintSpeed = 7.0f;
    [Export] public float CrouchSpeed = 2.0f;
    [Export] public float JumpForce = 5.0f;

    [ExportGroup("Physics Settings")]
    [Export] public float Gravity = 19.62f;
    [Export] public float Acceleration = 10.0f;
    [Export] public float Deceleration = 12.0f;
    [Export] public float AirControl = 2.0f;
    [Export] public float CoyoteTime = 0.15f;
    [Export] public float JumpBufferTime = 0.15f;

    [ExportGroup("Crouch Settings")]
    [Export] public float NormalHeight = 1.8f;
    [Export] public float CrouchHeight = 0.9f;
    [Export] public float CrouchTransitionSpeed = 10.0f;

    [ExportGroup("Stamina Settings")]
    [Export] public float MaxStamina = 5.0f;
    [Export] public float DrainRate = 1.0f;
    [Export] public float RegenRate = 1.0f;

    [ExportGroup("References")]
    [Export] public CollisionShape3D BodyCollider;
    [Export] public Node3D HeadNode;
    [Export] public ShapeCast3D CeilingCheck;

    private float _currentStamina;
    private bool _isExhausted = false;
    private bool _isCrouching = false;
    private bool _skipNextCrouchRelease = false;
    private float _originalCapsuleHeight;
    private float _originalHeadY;

    private float _coyoteTimer = 0.0f;
    private float _jumpBufferTimer = 0.0f;
    
    private bool _wasMovingLastFrame = false;
    private Vector3 _lastVelocity = Vector3.Zero;
    private float _lastStamina = 0.0f;

    public override void _Ready()
    {
        _currentStamina = MaxStamina;
        _lastStamina = _currentStamina;
        
        FloorSnapLength = 0.2f;
        FloorMaxAngle = Mathf.DegToRad(45.0f);

        if (BodyCollider?.Shape is CapsuleShape3D capsule)
        {
            BodyCollider.Shape = (CapsuleShape3D)capsule.Duplicate();
            ((CapsuleShape3D)BodyCollider.Shape).Height = NormalHeight;
            _originalCapsuleHeight = NormalHeight;
            BodyCollider.Position = new Vector3(0.0f, NormalHeight * 0.5f, 0.0f);
        }

        if (HeadNode != null)
        {
            _originalHeadY = NormalHeight * 0.85f;
            HeadNode.Position = new Vector3(0.0f, _originalHeadY, 0.0f);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float deltaTime = (float)delta;

        Vector2 inputDirection = Input.GetVector("move_left", "move_right", "move_backward", "move_forward");
        bool sprintHeld = Input.IsActionPressed("sprint");
        bool crouchHeld = Input.IsActionPressed("crouch");

        UpdateJumpTimers(deltaTime);
        ApplyGravity(deltaTime);
        
        bool isMovingInput = inputDirection != Vector2.Zero;
        bool needsMovementUpdate = isMovingInput || !IsOnFloor() || _wasMovingLastFrame;

        if (needsMovementUpdate)
        {
            HandleMovement(inputDirection, sprintHeld, deltaTime);
        }
        else
        {
            Vector3 vel = Velocity;
            if (Mathf.Abs(vel.X) > 0.01f || Mathf.Abs(vel.Z) > 0.01f)
            {
                vel.X = Mathf.MoveToward(vel.X, 0, Deceleration * deltaTime);
                vel.Z = Mathf.MoveToward(vel.Z, 0, Deceleration * deltaTime);
                Velocity = vel;
            }
        }

        HandleJump();
        HandleCrouch(crouchHeld, deltaTime);
        HandleStamina(sprintHeld, inputDirection, deltaTime);
        
        MoveAndSlide();
        
        _wasMovingLastFrame = isMovingInput;
        _lastVelocity = Velocity;
    }

    private void UpdateJumpTimers(float deltaTime)
    {
        if (IsOnFloor())
        {
            _coyoteTimer = CoyoteTime;
        }
        else
        {
            _coyoteTimer -= deltaTime;
        }

        if (Input.IsActionJustPressed("jump"))
        {
            _jumpBufferTimer = JumpBufferTime;
        }
        else if (_jumpBufferTimer > 0)
        {
            _jumpBufferTimer -= deltaTime;
        }
    }

    private void ApplyGravity(float deltaTime)
    {
        if (!IsOnFloor())
        {
            Vector3 velocity = Velocity;
            velocity.Y -= Gravity * deltaTime;
            Velocity = velocity;
        }
    }

    private void HandleMovement(Vector2 inputDirection, bool sprintHeld, float deltaTime)
    {
        Vector3 moveDirection = (GlobalTransform.Basis * new Vector3(inputDirection.X, 0.0f, -inputDirection.Y)).Normalized();

        bool isMovingForward = inputDirection.Y > 0.1f;
        bool canSprint = sprintHeld && !_isExhausted && !_isCrouching && isMovingForward;

        float targetSpeed = WalkSpeed;

        if (_isCrouching)
        {
            targetSpeed = CrouchSpeed;
        }
        else if (canSprint)
        {
            targetSpeed = SprintSpeed;
        }

        Vector3 velocity = Velocity;
        float accelRate = IsOnFloor() ? (moveDirection != Vector3.Zero ? Acceleration : Deceleration) : AirControl;

        Vector3 targetVelocity = moveDirection * targetSpeed;
        velocity.X = Mathf.Lerp(velocity.X, targetVelocity.X, accelRate * deltaTime);
        velocity.Z = Mathf.Lerp(velocity.Z, targetVelocity.Z, accelRate * deltaTime);

        Velocity = velocity;
    }

    private void HandleJump()
    {
        if (_jumpBufferTimer > 0.0f && _coyoteTimer > 0.0f && !_isCrouching)
        {
            Vector3 velocity = Velocity;
            velocity.Y = JumpForce;
            Velocity = velocity;

            _jumpBufferTimer = 0.0f;
            _coyoteTimer = 0.0f;
        }
    }

    private void HandleCrouch(bool crouchHeld, float deltaTime)
    {
        if (_skipNextCrouchRelease)
        {
            _skipNextCrouchRelease = false;
            return;
        }

        if (crouchHeld)
        {
            _isCrouching = true;
        }
        else if (_isCrouching)
        {
            if (CeilingCheck != null)
            {
                CeilingCheck.ForceShapecastUpdate();
                _isCrouching = CeilingCheck.IsColliding();
            }
            else
            {
                _isCrouching = false;
            }
        }

        ApplyCrouchSmooth(deltaTime);
    }

    private void ApplyCrouchSmooth(float deltaTime)
    {
        float targetHeight = _isCrouching ? CrouchHeight : _originalCapsuleHeight;

        if (BodyCollider?.Shape is CapsuleShape3D capsule)
        {
            if (Mathf.Abs(capsule.Height - targetHeight) > 0.01f)
            {
                capsule.Height = Mathf.Lerp(capsule.Height, targetHeight, CrouchTransitionSpeed * deltaTime);
                BodyCollider.Position = new Vector3(0.0f, capsule.Height * 0.5f, 0.0f);
            }
        }

        if (HeadNode != null)
        {
            float targetHeadY = _isCrouching ? CrouchHeight * 0.85f : _originalHeadY;
            if (Mathf.Abs(HeadNode.Position.Y - targetHeadY) > 0.01f)
            {
                Vector3 headPosition = HeadNode.Position;
                headPosition.Y = Mathf.Lerp(headPosition.Y, targetHeadY, CrouchTransitionSpeed * deltaTime);
                HeadNode.Position = headPosition;
            }
        }
    }

    private void ApplyCrouchInstant()
    {
        float targetHeight = _isCrouching ? CrouchHeight : _originalCapsuleHeight;

        if (BodyCollider?.Shape is CapsuleShape3D capsule)
        {
            capsule.Height = targetHeight;
            BodyCollider.Position = new Vector3(0.0f, targetHeight * 0.5f, 0.0f);
        }

        if (HeadNode != null)
        {
            float targetHeadY = _isCrouching ? CrouchHeight * 0.85f : _originalHeadY;
            HeadNode.Position = new Vector3(0.0f, targetHeadY, 0.0f);
        }
    }

    private void HandleStamina(bool sprintHeld, Vector2 inputDirection, float deltaTime)
    {
        bool isMovingForward = inputDirection.Y > 0.1f;
        bool staminaChanged = false;

        if (sprintHeld && isMovingForward && !_isCrouching && !_isExhausted)
        {
            _currentStamina -= DrainRate * deltaTime;

            if (_currentStamina <= 0.0f)
            {
                _currentStamina = 0.0f;
                _isExhausted = true;
            }
            staminaChanged = true;
        }
        else if (!sprintHeld && _currentStamina < MaxStamina)
        {
            _currentStamina += RegenRate * deltaTime;

            if (_currentStamina >= MaxStamina)
            {
                _currentStamina = MaxStamina;
                _isExhausted = false;
            }
            staminaChanged = true;
        }

        if (staminaChanged && !Mathf.IsEqualApprox(_currentStamina, _lastStamina))
        {
            EmitSignal(SignalName.StaminaChanged, _currentStamina, MaxStamina);
            _lastStamina = _currentStamina;
        }
    }

    public string GetSaveId()
    {
        return "PlayerMovement_ID";
    }

    public Dictionary CaptureState()
    {
        return new Dictionary
        {
            ["pos_x"] = GlobalPosition.X,
            ["pos_y"] = GlobalPosition.Y,
            ["pos_z"] = GlobalPosition.Z,
            ["vel_x"] = Velocity.X,
            ["vel_y"] = Velocity.Y,
            ["vel_z"] = Velocity.Z,
            ["is_crouching"] = _isCrouching,
            ["current_stamina"] = _currentStamina,
            ["is_exhausted"] = _isExhausted
        };
    }

    public void RestoreState(Dictionary state)
    {
        if (state.ContainsKey("pos_x") && state.ContainsKey("pos_y") && state.ContainsKey("pos_z"))
        {
            GlobalPosition = new Vector3(
                state["pos_x"].AsSingle(),
                state["pos_y"].AsSingle(),
                state["pos_z"].AsSingle()
            );
        }

        if (state.ContainsKey("vel_x") && state.ContainsKey("vel_y") && state.ContainsKey("vel_z"))
        {
            Velocity = new Vector3(
                state["vel_x"].AsSingle(),
                state["vel_y"].AsSingle(),
                state["vel_z"].AsSingle()
            );
        }

        if (state.ContainsKey("is_crouching"))
        {
            _isCrouching = state["is_crouching"].AsBool();
            ApplyCrouchInstant();
            _skipNextCrouchRelease = true;
        }

        if (state.ContainsKey("current_stamina"))
        {
            _currentStamina = state["current_stamina"].AsSingle();
            _lastStamina = _currentStamina;
        }

        if (state.ContainsKey("is_exhausted"))
        {
            _isExhausted = state["is_exhausted"].AsBool();
        }
    }

    public void OnRestoreComplete()
    {
        if (CeilingCheck != null)
        {
            CeilingCheck.ForceShapecastUpdate();
        }
    }
}