using Godot;
using Godot.Collections;

public partial class PlayerLook : Node3D, ISaveable
{
    public enum LookMode
    {
        Mouse,
        Action
    }

    [Export] public LookMode Mode 
    { 
        get => _mode; 
        set 
        { 
            _mode = value; 
            UpdateProcessing(); 
        } 
    }
    
    private LookMode _mode = LookMode.Mouse;

    [Export(PropertyHint.Range, "0.01,10,0.01")] public float Sensitivity = 2.0f;
    [Export] public float MinPitch = -85.0f;
    [Export] public float MaxPitch = 85.0f;

    private Node3D _body;
    private float _pitch = 0.0f;
    private bool _isProcessingLook = false;

    public override void _Ready()
    {
        _body = GetParent<Node3D>();
        UpdateProcessing();
    }

    private void UpdateProcessing()
    {
        _isProcessingLook = _mode == LookMode.Action;
        SetProcess(_isProcessingLook);
        SetProcessUnhandledInput(_mode == LookMode.Mouse);

        if (IsInsideTree() && _mode == LookMode.Mouse)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            Vector2 delta = motion.Relative * Sensitivity * 0.05f;
            ApplyLook(delta.X, delta.Y);
        }
    }

    public override void _Process(double delta)
    {
        if (!_isProcessingLook) return;

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
        if (_body != null)
        {
            _body.RotateY(Mathf.DegToRad(-yawDelta));
        }

        _pitch -= pitchDelta;
        _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
        Rotation = new Vector3(Mathf.DegToRad(_pitch), 0, 0);
    }

    public string GetSaveId()
    {
        return "PlayerLook_ID";
    }

    public Dictionary CaptureState()
    {
        return new Dictionary
        {
            ["pitch"] = _pitch,
            ["body_rotation_y"] = _body != null ? Mathf.Wrap(_body.Rotation.Y, -Mathf.Pi, Mathf.Pi) : 0.0f
        };
    }

    public void RestoreState(Dictionary state)
    {
        if (state.ContainsKey("pitch"))
        {
            _pitch = state["pitch"].AsSingle();
            Rotation = new Vector3(Mathf.DegToRad(_pitch), 0, 0);
        }

        if (state.ContainsKey("body_rotation_y") && _body != null)
        {
            Vector3 bodyRot = _body.Rotation;
            bodyRot.Y = state["body_rotation_y"].AsSingle();
            _body.Rotation = bodyRot;
        }
    }

    public void OnRestoreComplete() { }
}