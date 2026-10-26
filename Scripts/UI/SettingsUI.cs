using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class SettingsUI : Control
{
    public event Action Closed;
    private PlayerSettingsData _original;
    private PlayerSettingsData _draft;
    private VBoxContainer _rows;
    private Label _status;
    private Button _apply;
    private ControlBindingsUI _bindings;
    private Control _previousFocus;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
        var background = new ColorRect { Color = new Color(0.02f, 0.03f, 0.05f, 0.98f) };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(660, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.09f, 0.13f), ContentMarginLeft = 28, ContentMarginRight = 28,
            ContentMarginTop = 24, ContentMarginBottom = 24
        });
        center.AddChild(panel);
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 10);
        panel.AddChild(_rows);
        _bindings = new ControlBindingsUI { Name = "ControlBindingsUI" };
        AddChild(_bindings);
    }

    public void Open()
    {
        if (Visible) return;
        PlayerSettings.EnsureLoaded();
        _original = PlayerSettings.Current.Copy();
        _draft = _original.Copy();
        _previousFocus = GetViewport().GuiGetFocusOwner();
        foreach (Node child in _rows.GetChildren()) { _rows.RemoveChild(child); child.QueueFree(); }
        var title = new Label { Text = "SETTINGS & CONTROLS" };
        title.AddThemeFontSizeOverride("font_size", 26);
        _rows.AddChild(title);
        AddSlider("MasterVolume", "Master volume", _draft.MasterVolume, value => _draft.MasterVolume = value);
        AddSlider("MusicVolume", "Music", _draft.MusicVolume, value => _draft.MusicVolume = value);
        AddSlider("EffectsVolume", "Effects", _draft.EffectsVolume, value => _draft.EffectsVolume = value);
        AddSlider("CameraShake", "Camera shake", _draft.CameraShake, value => _draft.CameraShake = value);
        AddToggle("CameraLookAhead", "Camera follows aim", _draft.CameraLookAhead, value => _draft.CameraLookAhead = value);
        AddToggle("FlashlightFlicker", "Flashlight flicker", _draft.FlashlightFlicker, value => _draft.FlashlightFlicker = value);
        AddToggle("Fullscreen", "Fullscreen", _draft.Fullscreen, value => _draft.Fullscreen = value);
        AddToggle("VSync", "VSync", _draft.VSync, value => _draft.VSync = value);
        AddButton(_rows, "EditBindings", "KEYBOARD & MOUSE CONTROLS", () => _bindings.Open(_draft));
        _status = new Label { Text = "Changes preview immediately. Save to keep them.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.CustomMinimumSize = new Vector2(600, 36);
        _status.AddThemeFontSizeOverride("font_size", 14);
        _rows.AddChild(_status);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 12);
        _rows.AddChild(actions);
        _apply = AddButton(actions, "SaveSettings", "SAVE SETTINGS", Save);
        AddButton(actions, "CancelSettings", "CANCEL [ESC]", Cancel);
        Visible = true;
        _apply.GrabFocus();
    }

    public void Cancel()
    {
        if (!Visible) return;
        PlayerSettings.Apply(_original);
        Close();
    }

    private void Save()
    {
        if (!PlayerSettings.Save(out string error)) { _status.Text = error; return; }
        Close();
    }

    private void Close()
    {
        Visible = false;
        if (IsInstanceValid(_previousFocus) && _previousFocus.IsVisibleInTree()) _previousFocus.GrabFocus();
        Closed?.Invoke();
    }

    public override void _Input(InputEvent @event)
    {
        if (Visible && !_bindings.Visible && @event is InputEventKey key && key.Pressed && !key.Echo
            && (key.Keycode == Key.Escape))
        {
            Cancel();
            GetViewport().SetInputAsHandled();
        }
    }

    private void AddSlider(string id, string label, float initial, Action<float> update)
    {
        var row = new HBoxContainer();
        _rows.AddChild(row);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(210, 30) });
        var slider = new HSlider { Name = id, MinValue = 0, MaxValue = 100, Step = 5, Value = initial * 100, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(slider);
        var percent = new Label { Text = $"{initial * 100:0}%", CustomMinimumSize = new Vector2(55, 0), HorizontalAlignment = HorizontalAlignment.Right };
        row.AddChild(percent);
        slider.ValueChanged += value => { update((float)value / 100); percent.Text = $"{value:0}%"; PlayerSettings.Apply(_draft); };
    }

    private void AddToggle(string id, string label, bool initial, Action<bool> update)
    {
        var toggle = new CheckButton { Name = id, Text = label, ButtonPressed = initial };
        _rows.AddChild(toggle);
        toggle.Toggled += value => { update(value); PlayerSettings.Apply(_draft); };
    }

    private static Button AddButton(Node owner, string id, string text, Action action)
    {
        var button = new Button { Name = id, Text = text, CustomMinimumSize = new Vector2(0, 40), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        button.Pressed += action;
        owner.AddChild(button);
        return button;
    }
}
