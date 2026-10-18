using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class SceneManager : Node
{
    [Signal] public delegate void TransitionStartedEventHandler(string targetScene);
    [Signal] public delegate void TransitionCompletedEventHandler(string targetScene);

    public static SceneManager Instance { get; private set; }

    public string CurrentScenePath { get; private set; } = "";
    public bool IsTransitioning { get; private set; } = false;

    private readonly System.Collections.Generic.Dictionary<string, string> objectLocations = new();
    private readonly System.Collections.Generic.Dictionary<string, Godot.Collections.Dictionary> sceneStates = new();
    private readonly System.Collections.Generic.Dictionary<string, Godot.Collections.Dictionary> pendingTransferStates = new();
    private readonly System.Collections.Generic.Dictionary<string, Vector3> pendingOffsets = new();

    private readonly List<Node> cachedSaveables = new();
    private readonly System.Collections.Generic.Dictionary<string, SceneTransitionPoint> cachedPoints = new();

    private string pendingTargetScene = "";
    private string pendingTargetPointId = "";

    private SceneTransitionHandler transitionHandler;

    public override void _Ready()
    {
        Instance = this;

        transitionHandler = new SceneTransitionHandler();
        AddChild(transitionHandler);

        CallDeferred(nameof(TryRegisterInitialScene));
    }

    private void TryRegisterInitialScene()
    {
        Node scene = GetTree().CurrentScene;
        if (scene == null)
        {
            CallDeferred(nameof(TryRegisterInitialScene));
            return;
        }

        CurrentScenePath = scene.SceneFilePath;
        ApplySceneState(scene);
    }

    public async Task RequestTransition(SceneTransitionTrigger trigger)
    {
        if (IsTransitioning)
        {
            return;
        }
        if (string.IsNullOrEmpty(trigger.TargetScenePath))
        {
            return;
        }

        IsTransitioning = true;
        EmitSignal(SignalName.TransitionStarted, trigger.TargetScenePath);

        CaptureCurrentScene();

        pendingTransferStates.Clear();
        pendingOffsets.Clear();

        if (trigger.TransferVolume != null)
        {
            foreach (Node3D body in trigger.TransferVolume.GetTransferables())
            {
                Vector3 offset = body.GlobalPosition - trigger.GlobalPosition;

                foreach (Node node in GetSaveableNodes(body))
                {
                    if (node is ISaveable saveable)
                    {
                        string id = saveable.GetSaveId();
                        if (!string.IsNullOrEmpty(id))
                        {
                            pendingTransferStates[id] = saveable.CaptureState();
                            pendingOffsets[id] = offset;
                            objectLocations[id] = trigger.TargetScenePath;
                        }
                    }
                }
            }
        }

        pendingTargetScene = trigger.TargetScenePath;
        pendingTargetPointId = trigger.TargetPointId;

        await transitionHandler.ExecuteTransition(pendingTargetScene, OnSceneReady);

        pendingTransferStates.Clear();
        pendingOffsets.Clear();
        pendingTargetScene = "";
        pendingTargetPointId = "";

        IsTransitioning = false;
        EmitSignal(SignalName.TransitionCompleted, CurrentScenePath);
    }

    public async Task LoadSceneFromSave(string targetScenePath)
    {
        if (IsTransitioning)
        {
            return;
        }

        IsTransitioning = true;
        await transitionHandler.ExecuteTransition(targetScenePath, OnSceneReadyFromSave);
        IsTransitioning = false;
    }

    private void OnSceneReady()
    {
        Node newScene = GetTree().CurrentScene;
        CurrentScenePath = pendingTargetScene;

        ApplySceneState(newScene);
        ApplyTransferStates();
        PlaceAtTargetPoint(pendingTargetPointId);
    }

    private void OnSceneReadyFromSave()
    {
        Node newScene = GetTree().CurrentScene;
        ApplySceneState(newScene);
    }

    public void CaptureCurrentScene()
    {
        Node scene = GetTree().CurrentScene;
        if (scene == null)
        {
            return;
        }

        if (cachedSaveables.Count == 0)
        {
            CacheSceneNodes(scene);
        }

        var states = new Godot.Collections.Dictionary();

        foreach (Node node in cachedSaveables)
        {
            if (node is ISaveable saveable)
            {
                string id = saveable.GetSaveId();
                if (!string.IsNullOrEmpty(id))
                {
                    states[id] = saveable.CaptureState();
                }
            }
        }

        sceneStates[CurrentScenePath] = states;
    }

    public void ApplySceneState(Node sceneRoot)
    {
        if (sceneRoot == null)
        {
            return;
        }

        string scenePath = sceneRoot.SceneFilePath;
        if (string.IsNullOrEmpty(scenePath))
        {
            return;
        }

        CacheSceneNodes(sceneRoot);

        if (sceneStates.TryGetValue(scenePath, out var states))
        {
            foreach (Node node in cachedSaveables)
            {
                if (node is ISaveable saveable)
                {
                    string id = saveable.GetSaveId();
                    if (!string.IsNullOrEmpty(id) && states.ContainsKey(id))
                    {
                        saveable.RestoreState(states[id].AsGodotDictionary());
                    }
                }
            }
        }

        foreach (Node node in cachedSaveables)
        {
            if (node is ISaveable saveable)
            {
                string id = saveable.GetSaveId();
                if (!string.IsNullOrEmpty(id))
                {
                    objectLocations[id] = scenePath;
                }
                saveable.OnRestoreComplete();
            }
        }
    }

    private void ApplyTransferStates()
    {
        if (pendingTransferStates.Count == 0)
        {
            return;
        }

        foreach (Node node in cachedSaveables)
        {
            if (node is ISaveable saveable)
            {
                string id = saveable.GetSaveId();
                if (!string.IsNullOrEmpty(id) && pendingTransferStates.TryGetValue(id, out var state))
                {
                    saveable.RestoreState(state);
                    saveable.OnRestoreComplete();
                }
            }
        }
    }

    private void PlaceAtTargetPoint(string pointId)
    {
        if (string.IsNullOrEmpty(pointId))
        {
            return;
        }

        if (!cachedPoints.TryGetValue(pointId, out var point))
        {
            GD.PrintErr($"SceneTransitionPoint not found: {pointId}");
            return;
        }

        foreach (Node node in cachedSaveables)
        {
            if (node is ISaveable saveable && node is Node3D node3d)
            {
                string id = saveable.GetSaveId();
                if (!string.IsNullOrEmpty(id) && pendingTransferStates.ContainsKey(id))
                {
                    Vector3 offset = pendingOffsets.ContainsKey(id) ? pendingOffsets[id] : Vector3.Zero;
                    node3d.GlobalPosition = point.GlobalPosition + offset;

                    if (node3d is CharacterBody3D charBody)
                    {
                        Vector3 vel = charBody.Velocity;
                        vel.Y = 0;
                        charBody.Velocity = vel;
                    }
                }
            }
        }
    }

    private void CacheSceneNodes(Node sceneRoot)
    {
        cachedSaveables.Clear();
        cachedPoints.Clear();
        CollectSceneNodes(sceneRoot);
    }

    private void CollectSceneNodes(Node node)
    {
        if (node is ISaveable)
        {
            cachedSaveables.Add(node);
        }

        if (node is SceneTransitionPoint p && !string.IsNullOrEmpty(p.PointId))
        {
            cachedPoints[p.PointId] = p;
        }

        foreach (Node child in node.GetChildren())
        {
            CollectSceneNodes(child);
        }
    }

    public Godot.Collections.Dictionary SaveData()
    {
        var locations = new Godot.Collections.Dictionary();
        var states = new Godot.Collections.Dictionary();

        foreach (var kvp in objectLocations)
        {
            locations[kvp.Key] = kvp.Value;
        }
        foreach (var kvp in sceneStates)
        {
            states[kvp.Key] = kvp.Value;
        }

        return new Godot.Collections.Dictionary
        {
            ["current_scene"] = CurrentScenePath,
            ["object_locations"] = locations,
            ["scene_states"] = states
        };
    }

    public void LoadData(Godot.Collections.Dictionary data)
    {
        objectLocations.Clear();
        sceneStates.Clear();

        if (data.ContainsKey("object_locations"))
        {
            var locations = data["object_locations"].AsGodotDictionary();
            foreach (var key in locations.Keys)
            {
                objectLocations[key.AsString()] = locations[key].AsString();
            }
        }

        if (data.ContainsKey("scene_states"))
        {
            var states = data["scene_states"].AsGodotDictionary();
            foreach (var key in states.Keys)
            {
                sceneStates[key.AsString()] = states[key].AsGodotDictionary();
            }
        }

        if (data.ContainsKey("current_scene"))
        {
            CurrentScenePath = data["current_scene"].AsString();
        }
    }

    public string GetObjectScene(string saveId)
    {
        if (objectLocations.TryGetValue(saveId, out string s))
        {
            return s;
        }
        return "";
    }

    public static List<Node> GetSaveableNodes(Node root)
    {
        var list = new List<Node>();
        if (root is ISaveable)
        {
            list.Add(root);
        }
        foreach (Node child in root.GetChildren())
        {
            list.AddRange(GetSaveableNodes(child));
        }
        return list;
    }
}