using System.Collections.Generic;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class ControlBindingsUI : Control
{
    private readonly Dictionary<string, Button> _buttons = new();
    private PlayerSettingsData _draft;
    private string _listening;
    private Label _status;
    private Button _back;
    private Control _previousFocus;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
        var background = new ColorRect { Color = new Color(0.02f, 0.03f, 0.05f) };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var rows = new VBoxContainer { CustomMinimumSize = new Vector2(660, 0) };
        rows.AddThemeConstantOverride("separation", 10);
        center.AddChild(rows);
        rows.AddChild(new Label { Text = "KEYBOARD & MOUSE", ThemeTypeVariation = "HeaderLarge" });
        rows.AddChild(new Label { Text = "Select a control, then press a key or mouse button. Esc cancels.\nReturn to settings and save to keep your changes." });
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(660, 380), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        rows.AddChild(scroll);
        var bindings = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(bindings);
        foreach (var action in ControlBindings.Actions)
        {
            var row = new HBoxContainer();
            bindings.AddChild(row);
            row.AddChild(new Label { Text = action.Label, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            var button = new Button { Name = "Bind_" + action.Id, CustomMinimumSize = new Vector2(240, 36) };
            button.Pressed += () => Listen(action.Id);
            row.AddChild(button);
            _buttons[action.Id] = button;
        }
        _status = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(660, 42) };
        rows.AddChild(_status);
        var actions = new HBoxContainer(); rows.AddChild(actions);
        var reset = new Button { Name = "ResetBindings", Text = "RESTORE DEFAULT CONTROLS", CustomMinimumSize = new Vector2(380, 40) };
        reset.Pressed += () => { _listening = null; _draft.Bindings.Clear(); PlayerSettings.Apply(_draft); Refresh(); _status.Text = "Default controls restored. Save settings to keep them."; };
        actions.AddChild(reset);
        _back = new Button { Name = "BackBindings", Text = "BACK TO SETTINGS", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _back.Pressed += Close;
        actions.AddChild(_back);
    }

    public void Open(PlayerSettingsData draft)
    {
        _draft = draft; _listening = null;
        _previousFocus = GetViewport().GuiGetFocusOwner();
        _status.Text = "Esc always closes menus. Gamepad bindings are preserved.";
        Refresh(); Visible = true; _buttons["move_up"].GrabFocus();
    }

    private void Listen(string id)
    {
        _listening = id; Refresh();
        _buttons[id].Text = "PRESS A KEY / BUTTON…";
        _status.Text = "Waiting for input. Esc cancels this assignment.";
    }

    private void Refresh()
    {
        foreach (var action in ControlBindings.Actions) _buttons[action.Id].Text = ControlBindings.Name(ControlBindings.Get(_draft, action));
    }

    public override void _Input(InputEvent input)
    {
        if (!Visible) return;
        if (input is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape)
            {
                if (_listening == null) Close();
                else { _listening = null; Refresh(); _status.Text = "Assignment cancelled."; }
            }
            else if (_listening != null) Assign((int)(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode));
            else return; // GUI keyboard navigation remains available.
            GetViewport().SetInputAsHandled();
        }
        else if (_listening != null && input is InputEventMouseButton mouse && mouse.Pressed)
        {
            Assign(-(int)mouse.ButtonIndex); GetViewport().SetInputAsHandled();
        }
    }

    private void Assign(int code)
    {
        if (!ControlBindings.TryRebind(_draft, _listening, code, out string error)) { _status.Text = error; return; }
        PlayerSettings.Apply(_draft);
        var button = _buttons[_listening];
        _listening = null; Refresh(); button.GrabFocus();
        _status.Text = "Control updated. Save settings to keep it.";
    }

    private void Close()
    {
        _listening = null; Visible = false;
        if (IsInstanceValid(_previousFocus) && _previousFocus.IsVisibleInTree()) _previousFocus.GrabFocus();
    }
}
