using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class PowerRoutingUI : CanvasLayer
{
    public event Action<int> ApplyRequested;
    private bool _previousInputLock;
    private Input.MouseModeEnum _previousMouseMode;
    private Control _overlay;
    private VBoxContainer _circuits;
    private Label _title;
    private Label _instructions;
    private Label _feedback;
    private Player _player;
    private readonly System.Collections.Generic.List<CheckButton> _switches = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _overlay = new ColorRect { Color = new Color(0.015f, 0.025f, 0.04f, 0.97f), Visible = false };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_overlay);
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.AddChild(center);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(580, 420) };
        box.AddThemeConstantOverride("separation", 20);
        center.AddChild(box);
        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 25);
        _title.Modulate = Colors.Aquamarine;
        box.AddChild(_title);
        _instructions = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        box.AddChild(_instructions);
        _circuits = new VBoxContainer();
        box.AddChild(_circuits);
        _feedback = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = Colors.Orange };
        box.AddChild(_feedback);
        var apply = new Button { Text = "APPLY ROUTING", CustomMinimumSize = new Vector2(0, 45) };
        apply.Pressed += () =>
        {
            int mask = 0;
            for (int i = 0; i < _switches.Count; i++) if (_switches[i].ButtonPressed) mask |= 1 << i;
            ApplyRequested?.Invoke(mask);
        };
        box.AddChild(apply);
        var close = new Button { Text = "BACK [ESC]", CustomMinimumSize = new Vector2(0, 42) };
        close.Pressed += Close;
        box.AddChild(close);
    }

    public void Open(PowerRoutingData data, Player player)
    {
        if (GetTree().Paused || data.Circuits.Length == 0 || data.Circuits.Length > 16) return;
        _player = player;
        _title.Text = data.Title;
        _instructions.Text = data.Instructions;
        _feedback.Text = "";
        foreach (var control in _switches) { _circuits.RemoveChild(control); control.QueueFree(); }
        _switches.Clear();
        foreach (string circuit in data.Circuits)
        {
            var toggle = new CheckButton { Text = circuit, CustomMinimumSize = new Vector2(0, 40) };
            _switches.Add(toggle);
            _circuits.AddChild(toggle);
        }
        _previousInputLock = player.IsInputLocked;
        _previousMouseMode = Input.MouseMode;
        _overlay.Visible = true;
        _player.IsInputLocked = true;
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _switches[0].GrabFocus();
    }

    public void ShowFeedback(string text) => _feedback.Text = text;

    public void Close()
    {
        if (!_overlay.Visible) return;
        _overlay.Visible = false;
        ControlBindings.ReleaseGameplayActions();
        GetTree().Paused = false;
        if (GodotObject.IsInstanceValid(_player)) _player.IsInputLocked = _previousInputLock;
        Input.MouseMode = _previousMouseMode;
    }

    public override void _Input(InputEvent input)
    {
        if (_overlay.Visible && input.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
        else if (_overlay.Visible && (input.IsActionPressed("toggle_inventory")
            || input.IsActionPressed("toggle_map") || input.IsActionPressed("pause_game")))
            GetViewport().SetInputAsHandled();
    }
}
