using Godot;
using System;
using System.Threading.Tasks;

public partial class SaveManager : Node
{
    [Signal] public delegate void SaveStartedEventHandler();
    [Signal] public delegate void SaveCompletedEventHandler();
    [Signal] public delegate void LoadStartedEventHandler();
    [Signal] public delegate void LoadCompletedEventHandler();

    private const string SecretKey = "project_fps_1234"; // NOT A REAL KEY, ONLY FOR TESTING
    private const int SaveVersion = 1;

    private SaveDirectoryHandler _directoryHandler;
    private SaveAnimations _saveAnimations;

    private bool _isBusy = false;

    public override void _Ready()
    {
        _directoryHandler = new SaveDirectoryHandler();
        _saveAnimations = new SaveAnimations();

        AddChild(_saveAnimations);

        _directoryHandler.EnsureDirectoryExists();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_isBusy)
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
        if (_isBusy)
        {
            return;
        }

        string timestamp = Time.GetDatetimeStringFromSystem().Replace(":", "-").Replace(" ", "_");
        await SaveGame("QuickSave_" + timestamp);
    }

    public async Task ManualSave(string customName = "")
    {
        if (_isBusy)
        {
            return;
        }

        string timestamp = Time.GetDatetimeStringFromSystem().Replace(":", "-").Replace(" ", "_");
        string saveName = string.IsNullOrEmpty(customName) ? "ManualSave_" + timestamp : customName;
        await SaveGame(saveName);
    }

    public async Task QuickLoad()
    {
        if (_isBusy)
        {
            return;
        }

        string latest = _directoryHandler.GetLatestSaveFile();
        if (!string.IsNullOrEmpty(latest))
        {
            await LoadGame(latest);
        }
    }

    public async Task SaveGame(string saveName)
    {
        _isBusy = true;
        EmitSignal(SignalName.SaveStarted);
        
        _saveAnimations.CacheUIReferences();
        await _saveAnimations.ShowSaveIcon();

        _directoryHandler.ManageSaves();

        string filePath = _directoryHandler.GetSavePath(saveName);
        string tempPath = _directoryHandler.GetTempPath(saveName);

        try
        {
            var sceneManager = GetNode<SceneManager>("/root/SceneManager");
            sceneManager.CaptureCurrentScene();

            var masterData = new Godot.Collections.Dictionary
            {
                ["version"] = SaveVersion,
                ["scene_manager"] = sceneManager.SaveData(),
                ["timestamp"] = Time.GetDatetimeStringFromSystem()
            };

            string jsonString = Json.Stringify(masterData);
            if (string.IsNullOrEmpty(jsonString))
            {
                throw new Exception("JSON serialization resulted in empty string.");
            }

            using var file = FileAccess.OpenEncryptedWithPass(tempPath, FileAccess.ModeFlags.Write, SecretKey);
            if (file == null)
            {
                throw new Exception("Failed to open temporary file for writing encryption.");
            }

            file.StoreString(jsonString);
            file.Close();

            if (FileAccess.FileExists(filePath))
            {
                DirAccess.RemoveAbsolute(filePath);
            }

            Error renameError = DirAccess.RenameAbsolute(tempPath, filePath);
            if (renameError != Error.Ok)
            {
                throw new Exception("Failed to rename temporary file to target save path.");
            }

            GD.Print("Save completed successfully: " + saveName);
        }
        catch (Exception e)
        {
            GD.PrintErr("Save encryption failed: " + e.Message);
            if (FileAccess.FileExists(tempPath))
            {
                DirAccess.RemoveAbsolute(tempPath);
            }
        }

        await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
        await _saveAnimations.HideSaveIcon();

        _isBusy = false;
        EmitSignal(SignalName.SaveCompleted);
    }

    public async Task LoadGame(string fileName)
    {
        _isBusy = true;
        EmitSignal(SignalName.LoadStarted);

        string filePath = _directoryHandler.GetSavePath(fileName);

        if (!FileAccess.FileExists(filePath))
        {
            GD.PrintErr("Load failed: File does not exist -> " + fileName);
            FinishLoading();
            return;
        }

        try
        {
            _saveAnimations.ShowLoadingUI(true);
            
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            GetTree().Paused = true;
            
            await _saveAnimations.TakeFreezeLayer();

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            using var file = FileAccess.OpenEncryptedWithPass(filePath, FileAccess.ModeFlags.Read, SecretKey);
            if (file == null)
            {
                throw new Exception("Failed to open save file for decryption.");
            }

            string jsonString = file.GetAsText();
            var json = new Json();

            if (json.Parse(jsonString) != Error.Ok)
            {
                throw new Exception("JSON parse error during load.");
            }

            var masterData = json.Data.AsGodotDictionary();
            var sceneManager = GetNode<SceneManager>("/root/SceneManager");

            if (masterData.ContainsKey("scene_manager"))
            {
                var sceneData = masterData["scene_manager"].AsGodotDictionary();
                sceneManager.LoadData(sceneData);

                string targetScene = sceneData.ContainsKey("current_scene") ? sceneData["current_scene"].AsString() : "";
                if (!string.IsNullOrEmpty(targetScene))
                {
                    await sceneManager.LoadSceneFromSave(targetScene);
                }
            }

            GD.Print("Load completed successfully: " + fileName);
        }
        catch (Exception e)
        {
            GD.PrintErr("Load failed: " + e.Message);
        }

        GetTree().Paused = false;

        await ToSignal(GetTree().CreateTimer(0.1f, processAlways: true), SceneTreeTimer.SignalName.Timeout);
        _saveAnimations.ShowLoadingUI(false);
        _saveAnimations.DeleteFreezeLayer();

        FinishLoading();
        EmitSignal(SignalName.LoadCompleted);
    }

    private void FinishLoading()
    {
        _isBusy = false;
    }
}