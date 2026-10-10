using Godot;

public partial class PlayerMovement : CharacterBody3D
{
    [Signal] public delegate void StaminaChangedEventHandler(float current, float max);

    [ExportGroup("Movement Settings")]
    [Export] public float WalkSpeed = 4.0f;
    [Export] public float SprintSpeed = 7.0f;
    [Export] public float CrouchSpeed = 2.0f;
    [Export] public float JumpForce = 3.0f;

    [ExportGroup("Physics Settings")]
    [Export] public float Gravity = 9.81f;
    [Export] public float Acceleration = 10.0f;
    [Export] public float Deceleration = 12.0f;
    [Export] public float AirControl = 3.0f;

    [ExportGroup("Crouch Settings")]
    [Export] public float NormalHeight = 1.8f;
    [Export] public float CrouchHeight = 0.9f;
    [Export] public float CrouchTransitionSpeed = 10.0f;

    [ExportGroup("Stamina Settings")]
    [Export] public float MaxStamina = 5.0f;
    [Export] public float CurrentStamina = 5.0f;
    [Export] public float DrainRate = 1.0f;
    [Export] public float RegenRate = 1.0f;

    [ExportGroup("References")]
    [Export] public CollisionShape3D BodyCollider;
    [Export] public Node3D HeadNode;
    [Export] public ShapeCast3D CeilingCheck;

    private bool isExhausted = false;
    private bool isCrouching = false;
    private float originalCapsuleHeight;
    private float originalHeadY;

    public override void _Ready()
    {
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

        ApplyGravity(deltaTime);
        HandleMovement(inputDirection, sprintHeld, deltaTime);
        HandleJump();
        HandleCrouch(crouchHeld, deltaTime);
        HandleStamina(sprintHeld, inputDirection, deltaTime);
        MoveAndSlide();
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
        if (Input.IsActionJustPressed("jump") && IsOnFloor() && !isCrouching)
        {
            Vector3 velocity = Velocity;
            velocity.Y = JumpForce;
            Velocity = velocity;
        }
    }

    private void HandleCrouch(bool crouchHeld, float deltaTime)
    {
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
}