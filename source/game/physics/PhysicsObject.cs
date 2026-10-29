using Godot;
using Godot.Collections;

public partial class PhysicsObject : RigidBody3D, ISaveable
{
    [ExportGroup("Audio Settings")]
    [Export] public AudioStreamPlayer3D ImpactAudioPlayer;
    [Export] public AudioStream DefaultImpactSound;
    [Export] public Dictionary<string, AudioStream> GroupImpactSounds = new();
    [Export] public float MinPitch = 0.85f;
    [Export] public float MaxPitch = 1.15f;
    [Export] public float MinImpactVelocity = 0.25f;

    [ExportGroup("Save Settings")]
    [Export] public string CustomSaveId = "";

    private float _lastImpactTime = 0.0f;
    private const float ImpactCooldown = 0.1f;

    public override void _Ready()
    {
        ContinuousCd = true;
        ContactMonitor = true;
        MaxContactsReported = 4;
        
        if (ImpactAudioPlayer == null)
        {
            ImpactAudioPlayer = new AudioStreamPlayer3D();
            AddChild(ImpactAudioPlayer);
        }

        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node body)
    {
        if (ImpactAudioPlayer == null || !IsInstanceValid(this))
        {
            return;
        }

        float currentTime = Time.GetTicksMsec() / 1000.0f;
        if (currentTime - _lastImpactTime < ImpactCooldown)
        {
            return;
        }

        float impactSpeed = LinearVelocity.Length();
        if (impactSpeed < MinImpactVelocity)
        {
            return;
        }

        _lastImpactTime = currentTime;
        
        AudioStream soundToPlay = DefaultImpactSound;

        if (body != null && IsInstanceValid(body))
        {
            foreach (var group in body.GetGroups())
            {
                if (GroupImpactSounds.ContainsKey(group.ToString()))
                {
                    soundToPlay = GroupImpactSounds[group.ToString()];
                    break;
                }
            }
        }

        if (soundToPlay != null)
        {
            ImpactAudioPlayer.Stream = soundToPlay;
            ImpactAudioPlayer.PitchScale = (float)GD.RandRange(MinPitch, MaxPitch);
            ImpactAudioPlayer.Play();
        }
    }

    public string GetSaveId()
    {
        if (!string.IsNullOrEmpty(CustomSaveId))
        {
            return CustomSaveId;
        }

        var idGenerator = GetTree().Root.GetNodeOrNull<NodeIDGenerator>("NodeIDGenerator");
        if (idGenerator != null)
        {
            return idGenerator.GetSaveID(this);
        }

        return Name.ToString();
    }

    public Dictionary CaptureState()
    {
        return new Dictionary
        {
            ["pos_x"] = GlobalPosition.X,
            ["pos_y"] = GlobalPosition.Y,
            ["pos_z"] = GlobalPosition.Z,
            ["rot_x"] = GlobalRotation.X,
            ["rot_y"] = GlobalRotation.Y,
            ["rot_z"] = GlobalRotation.Z,
            ["vel_x"] = LinearVelocity.X,
            ["vel_y"] = LinearVelocity.Y,
            ["vel_z"] = LinearVelocity.Z,
            ["ang_x"] = AngularVelocity.X,
            ["ang_y"] = AngularVelocity.Y,
            ["ang_z"] = AngularVelocity.Z,
            ["sleeping"] = Sleeping
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

        if (state.ContainsKey("rot_x") && state.ContainsKey("rot_y") && state.ContainsKey("rot_z"))
        {
            GlobalRotation = new Vector3(
                state["rot_x"].AsSingle(),
                state["rot_y"].AsSingle(),
                state["rot_z"].AsSingle()
            );
        }

        if (state.ContainsKey("vel_x") && state.ContainsKey("vel_y") && state.ContainsKey("vel_z"))
        {
            LinearVelocity = new Vector3(
                state["vel_x"].AsSingle(),
                state["vel_y"].AsSingle(),
                state["vel_z"].AsSingle()
            );
        }

        if (state.ContainsKey("ang_x") && state.ContainsKey("ang_y") && state.ContainsKey("ang_z"))
        {
            AngularVelocity = new Vector3(
                state["ang_x"].AsSingle(),
                state["ang_y"].AsSingle(),
                state["ang_z"].AsSingle()
            );
        }
    }

    public void OnRestoreComplete()
    {
        if (Sleeping)
        {
            Sleeping = false;
        }
    }
}