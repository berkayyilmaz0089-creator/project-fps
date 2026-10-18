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

    private float CurrentStamina;
    private bool isExhausted = false;
    private bool isCrouching = false;
    private bool skipNextCrouchRelease = false; // restore sonrası ilk release'i yut
    private float originalCapsuleHeight;
    private float originalHeadY;

    private float coyoteTimer = 0.0f;
    private float jumpBufferTimer = 0.0f;

    public override void _Ready()
    {
        CurrentStamina = MaxStamina;
        FloorSnapLength = 0.2f;
        FloorMaxAngle = Mathf.DegToRad(45.0f);

        if (BodyCollider?.Shape is CapsuleShape3D capsule)
        {
            BodyCollider.Shape = (CapsuleShape3D)capsule.Duplicate();
            ((CapsuleShape3D)BodyCollider.Shape).Height = NormalHeight;
            originalCapsuleHeight = NormalHeight;
            BodyCollider.Position = new Vector3(0.0f, NormalHeight * 0.5f, 0.0f);
        }

        if (HeadNode != null)
        {
            originalHeadY = NormalHeight * 0.85f;
            HeadNode.Position = new Vector3(0.0f, originalHeadY, 0.0f);
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
        HandleMovement(inputDirection, sprintHeld, deltaTime);
        HandleJump();
        HandleCrouch(crouchHeld, deltaTime);
        HandleStamina(sprintHeld, inputDirection, deltaTime);
        MoveAndSlide();
    }

    private void UpdateJumpTimers(float deltaTime)
    {
        if (IsOnFloor())
        {
            coyoteTimer = CoyoteTime;
        }
        else
        {
            coyoteTimer -= deltaTime;
        }

        if (Input.IsActionJustPressed("jump"))
        {
            jumpBufferTimer = JumpBufferTime;
        }
        else
        {
            jumpBufferTimer -= deltaTime;
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
        bool canSprint = sprintHeld && !isExhausted && !isCrouching && isMovingForward;

        float targetSpeed = WalkSpeed;

        if (isCrouching)
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
        if (jumpBufferTimer > 0.0f && coyoteTimer > 0.0f && !isCrouching)
        {
            Vector3 velocity = Velocity;
            velocity.Y = JumpForce;
            Velocity = velocity;

            jumpBufferTimer = 0.0f;
            coyoteTimer = 0.0f;
        }
    }

    private void HandleCrouch(bool crouchHeld, float deltaTime)
    {
        if (skipNextCrouchRelease)
        {
            skipNextCrouchRelease = false;
            return;
        }

        if (crouchHeld)
        {
            isCrouching = true;
        }
        else if (isCrouching)
        {
            bool hasCeiling = false;

            if (CeilingCheck != null)
            {
                CeilingCheck.ForceShapecastUpdate();
                hasCeiling = CeilingCheck.IsColliding();
            }

            if (!hasCeiling)
            {
                isCrouching = false;
            }
        }

        ApplyCrouchSmooth(deltaTime);
    }

    private void ApplyCrouchSmooth(float deltaTime)
    {
        float targetHeight = isCrouching ? CrouchHeight : originalCapsuleHeight;

        if (BodyCollider?.Shape is CapsuleShape3D capsule)
        {
            capsule.Height = Mathf.Lerp(capsule.Height, targetHeight, CrouchTransitionSpeed * deltaTime);
            BodyCollider.Position = new Vector3(0.0f, capsule.Height * 0.5f, 0.0f);
        }

        if (HeadNode != null)
        {
            float targetHeadY = isCrouching ? CrouchHeight * 0.85f : originalHeadY;
            Vector3 headPosition = HeadNode.Position;
            headPosition.Y = Mathf.Lerp(headPosition.Y, targetHeadY, CrouchTransitionSpeed * deltaTime);
            HeadNode.Position = headPosition;
        }
    }

    private void ApplyCrouchInstant()
    {
        float targetHeight = isCrouching ? CrouchHeight : originalCapsuleHeight;

        if (BodyCollider?.Shape is CapsuleShape3D capsule)
        {
            capsule.Height = targetHeight;
            BodyCollider.Position = new Vector3(0.0f, targetHeight * 0.5f, 0.0f);
        }

        if (HeadNode != null)
        {
            float targetHeadY = isCrouching ? CrouchHeight * 0.85f : originalHeadY;
            HeadNode.Position = new Vector3(0.0f, targetHeadY, 0.0f);
        }
    }

    private void HandleStamina(bool sprintHeld, Vector2 inputDirection, float deltaTime)
    {
        bool isMovingForward = inputDirection.Y > 0.1f;

        if (sprintHeld && isMovingForward && !isCrouching && !isExhausted)
        {
            CurrentStamina -= DrainRate * deltaTime;

            if (CurrentStamina <= 0.0f)
            {
                CurrentStamina = 0.0f;
                isExhausted = true;
            }

            EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
        }
        else
        {
            if (!sprintHeld && CurrentStamina < MaxStamina)
            {
                CurrentStamina += RegenRate * deltaTime;

                if (CurrentStamina >= MaxStamina)
                {
                    CurrentStamina = MaxStamina;
                    isExhausted = false;
                }

                EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
            }
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
            ["is_crouching"] = isCrouching,
            ["current_stamina"] = CurrentStamina,
            ["is_exhausted"] = isExhausted
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
            isCrouching = state["is_crouching"].AsBool();
            ApplyCrouchInstant();
            skipNextCrouchRelease = true;
        }

        if (state.ContainsKey("current_stamina"))
        {
            CurrentStamina = state["current_stamina"].AsSingle();
        }

        if (state.ContainsKey("is_exhausted"))
        {
            isExhausted = state["is_exhausted"].AsBool();
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