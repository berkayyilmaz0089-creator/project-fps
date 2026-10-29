using Godot;
using System.Threading.Tasks;

public partial class SaveAnimations : Node
{
    private TextureRect _freezeLayer;
    private ImageTexture _screenTexture;
    private CanvasItem _cachedSaveIcon;
    private CanvasItem _cachedLoadingUi;
    
    private Tween _activeTween;
    private bool _uiInitialized = false;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (_uiInitialized)
        {
            return;
        }

        CanvasLayer canvasLayer = new CanvasLayer();
        canvasLayer.Layer = 100;
        canvasLayer.ProcessMode = ProcessModeEnum.Always;
        
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, canvasLayer);

        _freezeLayer = new TextureRect();
        _freezeLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _freezeLayer.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _freezeLayer.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        _freezeLayer.MouseFilter = Control.MouseFilterEnum.Stop;
        _freezeLayer.Visible = false;
        
        canvasLayer.CallDeferred(Node.MethodName.AddChild, _freezeLayer);
        _uiInitialized = true;
    }

    public void CacheUIReferences()
    {
        _cachedSaveIcon = GetTree().GetFirstNodeInGroup("save_icon") as CanvasItem;
        _cachedLoadingUi = GetTree().GetFirstNodeInGroup("loading_ui") as CanvasItem;
    }

    public void ShowLoadingUI(bool visible)
    {
        if (!IsInstanceValid(_cachedLoadingUi))
        {
            _cachedLoadingUi = GetTree().GetFirstNodeInGroup("loading_ui") as CanvasItem;
        }

        if (IsInstanceValid(_cachedLoadingUi))
        {
            _cachedLoadingUi.Visible = visible;
        }
    }

    public async Task ShowSaveIcon()
    {
        if (!IsInstanceValid(_cachedSaveIcon))
        {
            _cachedSaveIcon = GetTree().GetFirstNodeInGroup("save_icon") as CanvasItem;
        }

        if (!IsInstanceValid(_cachedSaveIcon))
        {
            return;
        }

        _cachedSaveIcon.Visible = true;
        
        if (_activeTween != null && _activeTween.IsValid())
        {
            _activeTween.Kill();
        }
        
        _activeTween = CreateTween();
        _activeTween.TweenProperty(_cachedSaveIcon, "modulate:a", 1.0f, 0.3f).From(0.0f);
        await ToSignal(_activeTween, Tween.SignalName.Finished);
    }

    public async Task HideSaveIcon()
    {
        if (!IsInstanceValid(_cachedSaveIcon))
        {
            _cachedSaveIcon = GetTree().GetFirstNodeInGroup("save_icon") as CanvasItem;
        }

        if (!IsInstanceValid(_cachedSaveIcon))
        {
            return;
        }

        if (_activeTween != null && _activeTween.IsValid())
        {
            _activeTween.Kill();
        }

        _activeTween = CreateTween();
        _activeTween.TweenProperty(_cachedSaveIcon, "modulate:a", 0.0f, 0.3f);
        await ToSignal(_activeTween, Tween.SignalName.Finished);

        if (IsInstanceValid(_cachedSaveIcon))
        {
            _cachedSaveIcon.Visible = false;
        }
    }

    public async Task TakeFreezeLayer()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePreDraw);
        
        Viewport viewport = GetTree().Root;
        Image image = viewport.GetTexture().GetImage();
        
        if (image != null && _freezeLayer != null)
        {
            _screenTexture = ImageTexture.CreateFromImage(image);
            _freezeLayer.Texture = _screenTexture;
            _freezeLayer.Visible = true;
        }
    }

    public void DeleteFreezeLayer()
    {
        if (_freezeLayer == null || !_freezeLayer.Visible)
        {
            return;
        }

        _freezeLayer.Visible = false;
        _freezeLayer.Texture = null;
        _screenTexture = null;
    }
}