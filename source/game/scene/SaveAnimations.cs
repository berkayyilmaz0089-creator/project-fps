using Godot;
using System.Threading.Tasks;

public partial class SaveAnimations : Node
{
    public async Task ShowSaveIcon()
    {
        var icon = GetTree().GetFirstNodeInGroup("save_icon") as CanvasItem;
        if (!IsInstanceValid(icon))
        {
            return;
        }

        icon.Visible = true;
        Tween tween = CreateTween();
        tween.TweenProperty(icon, "modulate:a", 1.0f, 0.3f).From(0.0f);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    public async Task HideSaveIcon()
    {
        var icon = GetTree().GetFirstNodeInGroup("save_icon") as CanvasItem;
        if (!IsInstanceValid(icon))
        {
            return;
        }

        Tween tween = CreateTween();
        tween.TweenProperty(icon, "modulate:a", 0.0f, 0.3f);
        await ToSignal(tween, Tween.SignalName.Finished);

        if (IsInstanceValid(icon))
        {
            icon.Visible = false;
        }
    }

    public void ShowLoadingUI(bool visible)
    {
        var ui = GetTree().GetFirstNodeInGroup("loading_ui") as CanvasItem;
        if (IsInstanceValid(ui))
        {
            ui.Visible = visible;
        }
    }
}