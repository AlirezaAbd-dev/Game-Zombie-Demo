using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;

namespace TopDownDemo.UI;

public partial class AirlockStatusUI : CanvasLayer
{
    [Export] public AirlockCycleController Controller { get; set; }
    private PanelContainer _panel;
    private Label _status;
    private ProgressBar _progress;
    private Player _player;

    public override void _Ready()
    {
        if (Controller == null) { GD.PushError("Airlock view requires a controller."); return; }
        ProcessMode = ProcessModeEnum.Always;
        Layer = 90;
        _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _panel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _panel.OffsetLeft = -270; _panel.OffsetRight = 270;
        _panel.OffsetTop = -210; _panel.OffsetBottom = -90;
        var style = new StyleBoxFlat { BgColor = new Color(0.025f, 0.055f, 0.07f, 0.96f),
            BorderColor = new Color(0.2f, 0.75f, 0.7f), BorderWidthTop = 2,
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 12 };
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);
        var box = new VBoxContainer(); _panel.AddChild(box);
        var title = new Label { Text = "BIOCONTAINMENT / AIRLOCK STATUS" };
        title.AddThemeFontSizeOverride("font_size", 15); title.Modulate = Colors.Aquamarine; box.AddChild(title);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeFontSizeOverride("font_size", 14); box.AddChild(_status);
        _progress = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10) };
        box.AddChild(_progress);
        Controller.StatusChanged += Refresh;
        Refresh();
        Callable.From(() => _player = GetTree().GetFirstNodeInGroup("player") as Player).CallDeferred();
    }

    private void Refresh()
    {
        _status.Text = Controller.Message;
        _progress.Value = Controller.Progress;
        _status.Modulate = Controller.State == AirlockState.Complete ? Colors.Aquamarine : Colors.White;
    }

    public override void _Process(double delta)
    {
        if (_panel == null || !IsInstanceValid(_player)) return;
        _panel.Visible = !GetTree().Paused && (Controller.IsCycling
            || _player.GlobalPosition.DistanceTo(Controller.Controls.GlobalPosition) < 280);
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(Controller)) Controller.StatusChanged -= Refresh;
    }
}
