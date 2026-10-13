using Godot;
using System.Threading.Tasks;

public partial class SaveManager : Node
{
    [Signal] public delegate void SaveStartedEventHandler();
    [Signal] public delegate void SaveCompletedEventHandler();
    [Signal] public delegate void LoadStartedEventHandler();
    [Signal] public delegate void LoadCompletedEventHandler();

    private const string SecretKey = "project_fps_1234"; // NOT A REAL KEY, ONLU FOR TESTING

    private NodeIDGenerator idGenerator;
    private SaveDirectoryHandler directoryHandler;
    private SaveAnimations saveAnimations;

    private bool isBusy = false;

    public override void _Ready()
    {
        idGenerator = new NodeIDGenerator();
        directoryHandler = new SaveDirectoryHandler();
        saveAnimations = new SaveAnimations();

        AddChild(idGenerator);
        AddChild(directoryHandler);
        AddChild(saveAnimations);

        directoryHandler.EnsureDirectoryExists();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (isBusy)
        {
            return;
        }

        if (@event.IsActionPressed("quick_save"))
        {
            _ = QuickSave();
        }
        else if (@event.IsActionPressed("quick_load"))
        {
            _ = QuickLoad();
        }
    }

    public async Task QuickSave()
    {
        if (isBusy)
        {
            return;
        }

        string timestamp = Time.GetDatetimeStringFromSystem().Replace(":", "-").Replace(" ", "_");
        await SaveGame("QuickSave_" + timestamp);
    }

    public async Task ManualSave(string customName = "")
    {
        if (isBusy)
        {
            return;
        }

        string timestamp = Time.GetDatetimeStringFromSystem().Replace(":", "-").Replace(" ", "_");
        string saveName = string.IsNullOrEmpty(customName) ? "ManualSave_" + timestamp : customName;
        await SaveGame(saveName);
    }

    public async Task QuickLoad()
    {
        if (isBusy)
        {
            return;
        }

        string latest = directoryHandler.GetLatestSaveFile();
        if (!string.IsNullOrEmpty(latest))
        {
            await LoadGame(latest);
        }
    }

    public async Task SaveGame(string saveName)
    {
        isBusy = true;
        EmitSignal(SignalName.SaveStarted);
        await saveAnimations.ShowSaveIcon();

        directoryHandler.ManageSaves();

        string filePath = directoryHandler.GetSavePath(saveName);
        string currentScene = GetTree().CurrentScene != null ? GetTree().CurrentScene.SceneFilePath : string.Empty;

        var nodeStates = new Godot.Collections.Dictionary();
        var saveableNodes = GetSaveableNodes(GetTree().Root);

        foreach (Node node in saveableNodes)
        {
            if (node is ISaveable saveable)
            {
                string key = idGenerator.GetSaveID(node);
                nodeStates[key] = saveable.CaptureState();
            }
        }

        var masterData = new Godot.Collections.Dictionary
        {
            ["scene_path"] = currentScene,
            ["nodes"] = nodeStates,
            ["timestamp"] = Time.GetDatetimeStringFromSystem()
        };

        string jsonString = Json.Stringify(masterData);

        using var file = FileAccess.OpenEncryptedWithPass(filePath, FileAccess.ModeFlags.Write, SecretKey);
        if (file != null)
        {
            file.StoreString(jsonString);
        }

        await ToSignal(GetTree().CreateTimer(0.8f), SceneTreeTimer.SignalName.Timeout);
        await saveAnimations.HideSaveIcon();

        isBusy = false;
        EmitSignal(SignalName.SaveCompleted);
    }

    public async Task LoadGame(string fileName)
    {
        isBusy = true;
        saveAnimations.ShowLoadingUI(true);
        EmitSignal(SignalName.LoadStarted);

        string filePath = directoryHandler.GetSavePath(fileName);

        if (!FileAccess.FileExists(filePath))
        {
            FinishLoading();
            return;
        }

        using var file = FileAccess.OpenEncryptedWithPass(filePath, FileAccess.ModeFlags.Read, SecretKey);
        if (file == null)
        {
            FinishLoading();
            return;
        }

        string jsonString = file.GetAsText();
        var json = new Json();

        if (json.Parse(jsonString) != Error.Ok)
        {
            FinishLoading();
            return;
        }

        var masterData = json.Data.AsGodotDictionary();

        if (masterData.ContainsKey("scene_path"))
        {
            string targetScene = masterData["scene_path"].AsString();
            if (!string.IsNullOrEmpty(targetScene))
            {
                GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, targetScene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        if (masterData.ContainsKey("nodes"))
        {
            var nodeStates = masterData["nodes"].AsGodotDictionary();
            var saveableNodes = GetSaveableNodes(GetTree().Root);

            foreach (Node node in saveableNodes)
            {
                if (node is ISaveable saveable)
                {
                    string key = idGenerator.GetSaveID(node);

                    if (nodeStates.ContainsKey(key))
                    {
                        var nodeData = nodeStates[key].AsGodotDictionary();
                        saveable.RestoreState(nodeData);
                    }
                }
            }
        }

        GetTree().Paused = false;
        FinishLoading();
        EmitSignal(SignalName.LoadCompleted);
    }

    private System.Collections.Generic.List<Node> GetSaveableNodes(Node rootNode)
    {
        var list = new System.Collections.Generic.List<Node>();
        if (rootNode is ISaveable)
        {
            list.Add(rootNode);
        }

        foreach (Node child in rootNode.GetChildren())
        {
            list.AddRange(GetSaveableNodes(child));
        }

        return list;
    }

    private void FinishLoading()
    {
        saveAnimations.ShowLoadingUI(false);
        isBusy = false;
    }
}