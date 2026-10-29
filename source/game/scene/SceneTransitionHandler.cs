using Godot;
using System.Threading.Tasks;

public partial class SceneTransitionHandler : Node
{
    private SaveAnimations _saveAnimations;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _saveAnimations = new SaveAnimations();
        AddChild(_saveAnimations);
    }

    public async Task ExecuteTransition(string targetScenePath, System.Action onSceneReady)
    {
        _saveAnimations.ShowLoadingUI(true);
        
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        GetTree().Paused = true;
        
        await _saveAnimations.TakeFreezeLayer();

        Error err = ResourceLoader.LoadThreadedRequest(targetScenePath, "PackedScene", true);
        if (err != Error.Ok)
        {
            GD.PrintErr($"Thread load failed: {err}");
            GetTree().Paused = false;
            _saveAnimations.ShowLoadingUI(false);
            _saveAnimations.DeleteFreezeLayer();
            return;
        }

        while (ResourceLoader.LoadThreadedGetStatus(targetScenePath) == ResourceLoader.ThreadLoadStatus.InProgress)
        {
            await ToSignal(GetTree().CreateTimer(0.02f, processAlways: true), SceneTreeTimer.SignalName.Timeout);
        }

        var packedScene = ResourceLoader.LoadThreadedGet(targetScenePath) as PackedScene;
        if (packedScene != null)
        {
            GetTree().ChangeSceneToPacked(packedScene);
        }

        for (int i = 0; i < 5; i++)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        onSceneReady?.Invoke();

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        GetTree().Paused = false;
        
        await ToSignal(GetTree().CreateTimer(0.1f, processAlways: true), SceneTreeTimer.SignalName.Timeout);

        _saveAnimations.ShowLoadingUI(false);
        _saveAnimations.DeleteFreezeLayer();
    }
}