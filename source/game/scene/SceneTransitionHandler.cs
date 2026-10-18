using Godot;
using System.Threading.Tasks;

public partial class SceneTransitionHandler : Node
{
    private SaveAnimations saveAnimations;

    public override void _Ready()
    {
        saveAnimations = new SaveAnimations();
        AddChild(saveAnimations);
    }

    public async Task ExecuteTransition(string targetScenePath, System.Action onSceneReady)
    {
        saveAnimations.ShowLoadingUI(true);

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        GetTree().Paused = true;

        Error err = GetTree().ChangeSceneToFile(targetScenePath);
        if (err != Error.Ok)
        {
            GD.PrintErr($"Scene change failed: {err}");
            GetTree().Paused = false;
            saveAnimations.ShowLoadingUI(false);
            return;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        onSceneReady?.Invoke();

        GetTree().Paused = false;
        saveAnimations.ShowLoadingUI(false);
    }
}