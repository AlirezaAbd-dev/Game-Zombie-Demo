using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class SequencePanelUI : CanvasLayer
{
    public event Action<int> StepRequested;
    public bool IsOpen => _overlay?.Visible == true;
    private ColorRect _overlay;
    private VBoxContainer _box;
    private Label _feedback;
    private Player _player;
    private bool _previousInputLock;
    private Input.MouseModeEnum _previousMouseMode;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _overlay = new ColorRect { Color = new Color(0.015f, 0.025f, 0.04f, 0.98f), Visible = false };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_overlay);
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.AddChild(center);
        _box = new VBoxContainer { CustomMinimumSize = new Vector2(540, 380) };
        _box.AddThemeConstantOverride("separation", 16);
        center.AddChild(_box);
    }

    public void Open(SequencePanelData data, Player player)
    {
        if (GetTree().Paused || !IsInstanceValid(player)) return;
        foreach (Node child in _box.GetChildren()) { _box.RemoveChild(child); child.QueueFree(); }
        var title = new Label { Text = data.Title, Modulate = Colors.Aquamarine };
        title.AddThemeFontSizeOverride("font_size", 24); _box.AddChild(title);
        _box.AddChild(new Label { Text = data.Instructions, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        for (int i = 0; i < data.Steps.Length; i++)
        {
            int step = i;
            var button = new Button { Text = data.Steps[i], CustomMinimumSize = new Vector2(0, 44) };
            button.Pressed += () => StepRequested?.Invoke(step);
            _box.AddChild(button);
        }
        _feedback = new Label { Text = "SEQUENCE READY", AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = Colors.Orange };
        _box.AddChild(_feedback);
        var back = new Button { Text = "BACK [ESC]", CustomMinimumSize = new Vector2(0, 42) };
        back.Pressed += Close; _box.AddChild(back);
        _player = player;
        _previousInputLock = player.IsInputLocked;
        _previousMouseMode = Input.MouseMode;
        player.IsInputLocked = true;
        _overlay.Visible = true;
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _box.GetChild<Button>(2).GrabFocus();
    }

    public void ShowFeedback(string text) => _feedback.Text = text;

    public void Close()
    {
        if (!IsOpen) return;
        _overlay.Visible = false;
        ControlBindings.ReleaseGameplayActions();
        GetTree().Paused = false;
        if (IsInstanceValid(_player)) _player.IsInputLocked = _previousInputLock;
        Input.MouseMode = _previousMouseMode;
    }

    public override void _Input(InputEvent input)
    {
        if (!IsOpen) return;
        if (input.IsActionPressed("ui_cancel")) Close();
        if (input.IsActionPressed("ui_cancel") || input.IsActionPressed("toggle_inventory")
            || input.IsActionPressed("toggle_map") || input.IsActionPressed("pause_game"))
            GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree() => Close();
}
