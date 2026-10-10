using Godot;

public partial class PlayerLook : Node3D
{
    public enum LookMode
    {
        Mouse,
        Action
    }

    [Export] public LookMode Mode = LookMode.Mouse;
    [Export(PropertyHint.Range, "0.01,10,0.01")] public float Sensitivity = 2.0f;
    [Export] public float MinPitch = -85.0f;
    [Export] public float MaxPitch = 85.0f;

    private Node3D body;
    private float pitch = 0.0f;

    public override void _Ready()
    {
        body = GetParent<Node3D>();

        if (Mode == LookMode.Mouse)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Mode != LookMode.Mouse)
        {
            return;
        }

        if (@event is InputEventMouseMotion motion)
        {
            Vector2 delta = motion.Relative * Sensitivity * 0.05f;
            ApplyLook(delta.X, delta.Y);
        }
    }

    public override void _Process(double delta)
    {
        if (Mode != LookMode.Action)
        {
            return;
        }

        float deltaTime = (float)delta;
        float yawInput = Input.GetAxis("look_left", "look_right");
        float pitchInput = Input.GetAxis("look_down", "look_up");

        if (yawInput != 0.0f || pitchInput != 0.0f)
        {
            float yawDelta = yawInput * deltaTime;
            float pitchDelta = pitchInput * deltaTime;
            ApplyLook(yawDelta, pitchDelta);
        }
    }

    private void ApplyLook(float yawDelta, float pitchDelta)
    {
        body.RotateY(Mathf.DegToRad(-yawDelta));

        pitch -= pitchDelta;
        pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
        Rotation = new Vector3(Mathf.DegToRad(pitch), 0, 0);
    }
}