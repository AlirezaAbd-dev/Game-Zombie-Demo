using System;
using System.Collections.Generic;
using Godot;
using TopDownDemo.Core;

namespace TopDownDemo.Systems;

/// <summary>Positive values are physical keys; negative values are mouse buttons.</summary>
public static class ControlBindings
{
    public static event Action Changed;
    public sealed record ActionDefinition(string Id, string Label, int Default);
    public static readonly IReadOnlyList<ActionDefinition> Actions = new ActionDefinition[]
    {
        new("move_up", "Move up", (int)Key.W), new("move_down", "Move down", (int)Key.S),
        new("move_left", "Move left", (int)Key.A), new("move_right", "Move right", (int)Key.D),
        new("shoot", "Fire", -(int)MouseButton.Left), new("aim", "Aim", -(int)MouseButton.Right),
        new("reload", "Reload", (int)Key.R), new("interact", "Interact", (int)Key.E),
        new("dash", "Dash", (int)Key.Space), new("finisher", "Finisher", (int)Key.F),
        new("knife", "Knife", (int)Key.V), new("quick_heal", "Quick heal", (int)Key.H),
        new("crouch", "Toggle crouch", (int)Key.Ctrl),
        new("weapon_1", "Handgun", (int)Key.Key1), new("weapon_2", "Shotgun", (int)Key.Key2),
        new("weapon_3", "Rifle", (int)Key.Key3), new("switch_weapon", "Cycle weapon", (int)Key.Q),
        new("next_weapon", "Next weapon", -(int)MouseButton.WheelDown),
        new("prev_weapon", "Previous weapon", -(int)MouseButton.WheelUp),
        new("toggle_inventory", "Inventory", (int)Key.Tab), new("toggle_map", "Map", (int)Key.M),
        new("pause_game", "Pause (Esc always works)", (int)Key.P)
    };

    public static int Get(PlayerSettingsData data, ActionDefinition action) =>
        data.Bindings != null && data.Bindings.TryGetValue(action.Id, out int code) ? code : action.Default;

    public static bool TryRebind(PlayerSettingsData data, string id, int code, out string error)
    {
        error = "";
        if (!Valid(code)) { error = "Choose a key or mouse button. Esc is reserved for closing menus."; return false; }
        bool known = false;
        foreach (var action in Actions)
        {
            if (action.Id == id) { known = true; continue; }
            if (Get(data, action) == code) { error = $"{Name(code)} is already assigned to {action.Label}."; return false; }
        }
        if (!known) { error = "Unknown control."; return false; }
        data.Bindings ??= new();
        data.Bindings[id] = code;
        return true;
    }

    public static void Apply(PlayerSettingsData data)
    {
        data.Bindings ??= new();
        var used = new HashSet<int>();
        foreach (var action in Actions)
        {
            int code = Get(data, action);
            if (!Valid(code) || !used.Add(code)) { data.Bindings.Clear(); break; }
        }
        foreach (var action in Actions)
        {
            if (!InputMap.HasAction(action.Id)) InputMap.AddAction(action.Id);
            foreach (var input in InputMap.ActionGetEvents(action.Id))
                if (input is InputEventKey or InputEventMouseButton) InputMap.ActionEraseEvent(action.Id, input);
            int code = Get(data, action);
            InputEvent binding = code > 0
                ? new InputEventKey { PhysicalKeycode = (Key)code }
                : new InputEventMouseButton { ButtonIndex = (MouseButton)(-code) };
            Input.ActionRelease(action.Id);
            InputMap.ActionAddEvent(action.Id, binding);
        }
        Changed?.Invoke();
    }

    // GUI events also update Input's polled action state. Discard held actions when
    // handing control back so a click on a modal button cannot become a gunshot.
    public static void ReleaseGameplayActions()
    {
        foreach (var action in Actions)
            if (InputMap.HasAction(action.Id)) Input.ActionRelease(action.Id);
    }

    public static string Format(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{input:")) return text;
        foreach (var action in Actions)
            text = text.Replace("{input:" + action.Id + "}", Hint(action.Id));
        return text;
    }

    public static string Hint(string id)
    {
        foreach (var action in Actions) if (action.Id == id) return Name(Get(PlayerSettings.Current, action));
        return "?";
    }

    public static string Name(int code) => code > 0 ? OS.GetKeycodeString((Key)code) : (MouseButton)(-code) switch
    {
        MouseButton.Left => "Left mouse", MouseButton.Right => "Right mouse", MouseButton.Middle => "Middle mouse",
        MouseButton.WheelUp => "Wheel up", MouseButton.WheelDown => "Wheel down",
        MouseButton.WheelLeft => "Wheel left", MouseButton.WheelRight => "Wheel right",
        _ => $"Mouse {-code}"
    };

    private static bool Valid(int code) => code > 0
        ? Enum.IsDefined(typeof(Key), (long)code) && (Key)code is not (Key.None or Key.Escape)
        : code >= -(int)MouseButton.Xbutton2 && code <= -(int)MouseButton.Left;
}
