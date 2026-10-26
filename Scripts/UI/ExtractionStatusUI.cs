using Godot;
using TopDownDemo.Entities;

namespace TopDownDemo.UI;

public partial class ExtractionStatusUI : CanvasLayer
{
    [Export] public HelicopterExtractionController Controller { get; set; }
    private PanelContainer _panel;
    private Label _message;
    private ProgressBar _progress;
    public override void _Ready()
    {
        if (Controller == null) { GD.PushError("Extraction status requires a flight controller."); return; }
        ProcessMode = ProcessModeEnum.Always;
        Layer = 90;
        _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _panel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _panel.OffsetLeft = -300; _panel.OffsetRight = 300; _panel.OffsetTop = -205; _panel.OffsetBottom = -90;
        _panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat { BgColor = new Color(0.02f,0.04f,0.05f,0.97f),
            BorderColor = Colors.Aquamarine, BorderWidthTop = 2, ContentMarginLeft = 18, ContentMarginRight = 18,
            ContentMarginTop = 12, ContentMarginBottom = 12 });
        AddChild(_panel);
        var column = new VBoxContainer(); _panel.AddChild(column);
        column.AddChild(new Label { Text = "VULTURE-9 / EXTRACTION CONTROL", Modulate = Colors.Aquamarine });
        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; column.AddChild(_message);
        _progress = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0,10) };
        column.AddChild(_progress);
        Controller.StatusChanged += Refresh; Refresh();
    }
    private void Refresh()
    {
        _message.Text = Controller.Message; _progress.Value = Controller.Progress;
        UpdateVisibility();
    }
    private void UpdateVisibility() => _panel.Visible = !GetTree().Paused
        && Controller.State is not (ExtractionState.WaitingForBeacon or ExtractionState.Complete);
    public override void _Process(double delta) { if (_panel != null && IsInstanceValid(Controller)) UpdateVisibility(); }
    public override void _ExitTree() { if (IsInstanceValid(Controller)) Controller.StatusChanged -= Refresh; }
}
