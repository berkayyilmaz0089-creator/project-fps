using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// I have been working on this for a few days, I thought this would be easy dammit.
public partial class SceneManager : Node
{
    [Signal] public delegate void TransitionStartedEventHandler(string targetScene);
    [Signal] public delegate void TransitionCompletedEventHandler(string targetScene);

    public static SceneManager Instance { get; private set; }

    public string CurrentScenePath { get; private set; } = "";
    public bool IsTransitioning { get; private set; } = false;

    private readonly Dictionary<string, string> _objectLocations = new();
    private readonly Dictionary<string, Godot.Collections.Dictionary> _sceneStates = new();
    private readonly Dictionary<string, Godot.Collections.Dictionary> _pendingTransferStates = new();
    private readonly Dictionary<string, Vector3> _pendingOffsets = new();

    private readonly List<Node> _cachedSaveables = new();
    private readonly Dictionary<string, SceneTransitionPoint> _cachedPoints = new();

    private string _pendingTargetScene = "";
    private string _pendingTargetPointId = "";

    private SceneTransitionHandler _transitionHandler;

    public override void _Ready()
    {
        Instance = this;

        _transitionHandler = new SceneTransitionHandler();
        AddChild(_transitionHandler);

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

        _pendingTransferStates.Clear();
        _pendingOffsets.Clear();

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
                            _pendingTransferStates[id] = saveable.CaptureState();
                            _pendingOffsets[id] = offset;
                            _objectLocations[id] = trigger.TargetScenePath;
                        }
                    }
                }
            }
        }

        _pendingTargetScene = trigger.TargetScenePath;
        _pendingTargetPointId = trigger.TargetPointId;

        await _transitionHandler.ExecuteTransition(_pendingTargetScene, OnSceneReady);

        _pendingTransferStates.Clear();
        _pendingOffsets.Clear();
        _pendingTargetScene = "";
        _pendingTargetPointId = "";

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
        await _transitionHandler.ExecuteTransition(targetScenePath, OnSceneReadyFromSave);
        IsTransitioning = false;
    }

    private void OnSceneReady()
    {
        Node newScene = GetTree().CurrentScene;
        CurrentScenePath = _pendingTargetScene;

        CacheSceneNodes(newScene);
        ApplySceneState(newScene);
        
        CallDeferred(nameof(ApplyTransferAndPlace));
    }

    private void OnSceneReadyFromSave()
    {
        Node newScene = GetTree().CurrentScene;
        CacheSceneNodes(newScene);
        ApplySceneState(newScene);
    }

    private void ApplyTransferAndPlace()
    {
        ApplyTransferStates();
        PlaceAtTargetPoint(_pendingTargetPointId);
    }

    public void CaptureCurrentScene()
    {
        Node scene = GetTree().CurrentScene;
        if (scene == null)
        {
            return;
        }

        if (_cachedSaveables.Count == 0)
        {
            CacheSceneNodes(scene);
        }

        var states = new Godot.Collections.Dictionary();

        foreach (Node node in _cachedSaveables)
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

        _sceneStates[CurrentScenePath] = states;
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

        if (_sceneStates.TryGetValue(scenePath, out var states))
        {
            foreach (Node node in _cachedSaveables)
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

        foreach (Node node in _cachedSaveables)
        {
            if (node is ISaveable saveable)
            {
                string id = saveable.GetSaveId();
                if (!string.IsNullOrEmpty(id))
                {
                    _objectLocations[id] = scenePath;
                }
                saveable.OnRestoreComplete();
            }
        }
    }

    private void ApplyTransferStates()
    {
        if (_pendingTransferStates.Count == 0)
        {
            return;
        }

        foreach (Node node in _cachedSaveables)
        {
            if (node is ISaveable saveable)
            {
                string id = saveable.GetSaveId();
                if (!string.IsNullOrEmpty(id) && _pendingTransferStates.TryGetValue(id, out var state))
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

        if (!_cachedPoints.TryGetValue(pointId, out var point))
        {
            GD.PrintErr($"SceneTransitionPoint not found: {pointId}");
            return;
        }

        foreach (Node node in _cachedSaveables)
        {
            if (node is ISaveable saveable && node is Node3D node3d)
            {
                string id = saveable.GetSaveId();
                if (!string.IsNullOrEmpty(id) && _pendingTransferStates.ContainsKey(id))
                {
                    Vector3 offset = _pendingOffsets.ContainsKey(id) ? _pendingOffsets[id] : Vector3.Zero;
                    node3d.GlobalPosition = point.GlobalPosition + offset;

                    if (node3d is CharacterBody3D charBody)
                    {
                        charBody.Velocity = Vector3.Zero;
                    }
                }
            }
        }
    }

    private void CacheSceneNodes(Node sceneRoot)
    {
        _cachedSaveables.Clear();
        _cachedPoints.Clear();
        CollectSceneNodes(sceneRoot);
    }

    private void CollectSceneNodes(Node node)
    {
        if (node is ISaveable)
        {
            _cachedSaveables.Add(node);
        }

        if (node is SceneTransitionPoint p && !string.IsNullOrEmpty(p.PointId))
        {
            _cachedPoints[p.PointId] = p;
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

        foreach (var kvp in _objectLocations)
        {
            locations[kvp.Key] = kvp.Value;
        }
        
        foreach (var kvp in _sceneStates)
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
        _objectLocations.Clear();
        _sceneStates.Clear();

        if (data.ContainsKey("object_locations"))
        {
            var locations = data["object_locations"].AsGodotDictionary();
            foreach (var key in locations.Keys)
            {
                _objectLocations[key.AsString()] = locations[key].AsString();
            }
        }

        if (data.ContainsKey("scene_states"))
        {
            var states = data["scene_states"].AsGodotDictionary();
            foreach (var key in states.Keys)
            {
                _sceneStates[key.AsString()] = states[key].AsGodotDictionary();
            }
        }

        if (data.ContainsKey("current_scene"))
        {
            CurrentScenePath = data["current_scene"].AsString();
        }
    }

    public string GetObjectScene(string saveId)
    {
        if (_objectLocations.TryGetValue(saveId, out string s))
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