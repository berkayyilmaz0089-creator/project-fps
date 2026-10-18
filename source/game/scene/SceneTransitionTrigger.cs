using Godot;

public partial class SceneTransitionTrigger : Area3D
{
    [Export] public string TargetScenePath = "";
    [Export] public string SourcePointId = "";
    [Export] public string TargetPointId = "";
    [Export] public SceneTransitionVolume TransferVolume;
    [Export] public string RequiredGroup = "player";

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (SceneManager.Instance == null)
        {
            return;
        }
        if (SceneManager.Instance.IsTransitioning)
        {
            return;
        }
        if (!string.IsNullOrEmpty(RequiredGroup) && !body.IsInGroup(RequiredGroup))
        {
            return;
        }

        _ = SceneManager.Instance.RequestTransition(this);
    }
}