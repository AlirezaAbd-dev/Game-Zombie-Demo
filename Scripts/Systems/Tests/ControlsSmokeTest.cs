using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.UI;

namespace TopDownDemo.Systems.Tests;

public partial class ControlsSmokeTest : Node
{
    private int _checks;
    private SceneTree Tree => GetTree();
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try
        {
            Tree.CurrentScene = null;
            PlayerSettings.Reload();
            Check(InputMap.ActionGetEvents("interact").Any(input => input is InputEventJoypadButton), "keyboard setup preserves gamepad bindings");
            Check(!InputMap.ActionGetEvents("knife").Any(input => input is InputEventKey key && key.PhysicalKeycode == Key.C), "default crouch/knife conflict is removed");
            var draft = PlayerSettings.Current.Copy();
            Check(ControlBindings.TryRebind(draft, "interact", (int)Key.G, out _), "unused physical key can be assigned");
            Check(PlayerSettings.Current.Bindings.Count == 0, "editing a copy cannot mutate active preferences");
            Check(!ControlBindings.TryRebind(draft, "reload", (int)Key.G, out string conflict) && conflict.Contains("Interact"), "duplicate binding names the conflicting action");
            Check(!ControlBindings.TryRebind(draft, "reload", (int)Key.Escape, out _), "escape remains reserved as a menu escape route");
            Check(!ControlBindings.TryRebind(draft, "reload", int.MinValue, out _), "malformed binding is rejected safely");
            draft.Bindings["reload"] = (int)Key.G;
            PlayerSettings.Apply(draft);
            Check(ControlBindings.Hint("interact") == "E" && ControlBindings.Hint("reload") == "R", "conflicting disk preferences safely restore defaults");
            string path = ProjectSettings.GlobalizePath(PlayerSettings.FilePath);
            File.WriteAllText(path, "{\"Bindings\":null,\"MusicVolume\":0.4}");
            PlayerSettings.Reload();
            Check(ControlBindings.Hint("interact") == "E" && Mathf.IsEqualApprox(PlayerSettings.Current.MusicVolume, 0.4f), "older and null bindings retain other preferences");
            Check(Tree.ChangeSceneToFile("res://Scenes/MainMenu.tscn") == Error.Ok, "main menu loads for real control editor");
            await Frames();
            var menu = Tree.CurrentScene;
            Find<Button>(menu, "BtnSettings").EmitSignal(BaseButton.SignalName.Pressed);
            var settings = Find<SettingsUI>(menu);
            Find<Button>(settings, "EditBindings").GrabFocus();
            Find<Button>(settings, "EditBindings").EmitSignal(BaseButton.SignalName.Pressed);
            var editor = Find<ControlBindingsUI>(settings);
            Check(editor.Visible && editor.Size == GetViewport().GetVisibleRect().Size, "controls editor fills viewport and captures focus");
            await Rebind(editor, "interact", Key.G);
            Check(ControlBindings.Hint("interact") == "G", "key capture previews interact binding");
            Find<Button>(editor, "Bind_reload").EmitSignal(BaseButton.SignalName.Pressed);
            await KeyPress(Key.G);
            Check(ControlBindings.Hint("reload") == "R" && All<Label>(editor).Any(label => label.Text.Contains("already assigned")), "control editor refuses duplicate assignment");
            await KeyPress(Key.Escape);
            Check(editor.Visible && settings.Visible, "escape cancels capture while keeping controls editor open");
            await Capture("controls");
            Find<Button>(editor, "Bind_pause_game").GrabFocus();
            await Frames();
            Check(Find<ScrollContainer>(editor).ScrollVertical > 0, "keyboard focus scrolls lower controls into view");
            await Capture("controls-lower");
            Find<Button>(editor, "BackBindings").EmitSignal(BaseButton.SignalName.Pressed);
            Check(Find<Button>(settings, "EditBindings").HasFocus(), "returning restores settings keyboard focus");
            await KeyPress(Key.Escape);
            Check(!settings.Visible && ControlBindings.Hint("interact") == "E", "cancel settings restores original controls as well as audio");
            settings.Open();
            Find<Button>(settings, "EditBindings").EmitSignal(BaseButton.SignalName.Pressed);
            await Rebind(editor, "interact", Key.G);
            await Rebind(editor, "move_right", Key.Right);
            await Rebind(editor, "toggle_inventory", Key.B);
            await Rebind(editor, "toggle_map", Key.N);
            await Rebind(editor, "pause_game", Key.O);
            Find<Button>(editor, "Bind_shoot").EmitSignal(BaseButton.SignalName.Pressed);
            await MousePress(MouseButton.Xbutton1);
            Check(ControlBindings.Hint("shoot") == "Mouse 8", "mouse side button can replace fire");
            Find<Button>(editor, "BackBindings").EmitSignal(BaseButton.SignalName.Pressed);
            Find<Button>(settings, "SaveSettings").EmitSignal(BaseButton.SignalName.Pressed);
            PlayerSettings.Apply(new()); PlayerSettings.Reload();
            Check(ControlBindings.Hint("interact") == "G" && ControlBindings.Hint("toggle_map") == "N" && ControlBindings.Hint("shoot") == "Mouse 8", "saved keyboard and mouse controls survive disk reload");
            var saves = SaveSystem.Instance;
            saves.CreateNewGame(0, DifficultyLevel.Survivor);
            saves.ActiveSaveData.Flags.Add("intro_seen_act1_gate");
            Check(Tree.ChangeSceneToFile("res://Scenes/Act1_Gate.tscn") == Error.Ok, "campaign loads with custom controls");
            await Frames();
            var player = (Player)Tree.GetFirstNodeInGroup("player");
            player.IsInvulnerable = true;
            var start = player.GlobalPosition;
            await HoldKey(Key.D, 0.15);
            Check(start.DistanceTo(player.GlobalPosition) < 1, "former movement key no longer moves player");
            await HoldKey(Key.Right, 0.15);
            Check(player.GlobalPosition.X > start.X + 5, "rebound movement key drives real player physics");
            var pickup = new Pickup { Item = GD.Load<ItemData>("res://Resources/Items/Bandage.tres"), Position = player.GlobalPosition + new Vector2(0, -70), CollisionLayer = 0, CollisionMask = 0 };
            Tree.CurrentScene.AddChild(pickup);
            await ToSignal(Tree.CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            await Frames();
            Check(HUDUI.Instance.InteractionPromptLabel.Text.StartsWith("[G]"), "nearby pickup prompt displays rebound interact key");
            await KeyPress(Key.E);
            Check(GodotObject.IsInstanceValid(pickup), "former interact key cannot collect pickup");
            await KeyPress(Key.G);
            Check(!GodotObject.IsInstanceValid(pickup), "rebound interact key collects through real physics path");
            Check(ControlBindings.Format(GD.Load<CampaignInteractionData>("res://Resources/Campaign/ArrivalNote.tres").Message).Contains("[G] Interact"), "authored tutorial resolves current controls");
            StoryDirector.Instance.EmitSignal(StoryDirector.SignalName.ObjectiveUpdated, "TEST", "BOARD [{input:interact}]");
            Check(HUDUI.Instance.ObjectiveSubtaskLabel.Text == "BOARD [G]", "objective templates display current interact key");
            var preview = PlayerSettings.Current.Copy();
            ControlBindings.TryRebind(preview, "interact", (int)Key.J, out _);
            PlayerSettings.Apply(preview);
            Check(HUDUI.Instance.ObjectiveSubtaskLabel.Text == "BOARD [J]", "active objective hints refresh after controls change");
            PlayerSettings.Reload();
            int rounds = player.InventoryComponent.CurrentAmmo;
            await MousePress(MouseButton.Left);
            Check(player.InventoryComponent.CurrentAmmo == rounds, "former fire button no longer fires weapon");
            await MousePress(MouseButton.Xbutton1);
            Check(player.InventoryComponent.CurrentAmmo < rounds, "rebound mouse side button fires real weapon");
            await KeyPress(Key.Tab);
            Check(!Find<InventoryUI>(Tree.CurrentScene).Visible, "former inventory key no longer opens inventory");
            await KeyPress(Key.B);
            Check(Tree.Paused && Find<InventoryUI>(Tree.CurrentScene).Visible, "rebound inventory key opens real inventory");
            await KeyPress(Key.Escape);
            await KeyPress(Key.M);
            Check(!Find<MiniMapUI>(Tree.CurrentScene).IsFullscreen, "former map key no longer opens map");
            await KeyPress(Key.N);
            var map = Find<MiniMapUI>(Tree.CurrentScene);
            Check(Tree.Paused && map.IsFullscreen && map.MapTitleLabel.Text.Contains("N TO CLOSE"), "rebound map opens with matching close prompt");
            await KeyPress(Key.Escape);
            await KeyPress(Key.P);
            Check(!Tree.Paused, "former pause key no longer pauses game");
            await KeyPress(Key.O);
            var pause = Find<PauseMenuUI>(Tree.CurrentScene);
            Check(Tree.Paused && pause.Visible, "rebound pause key opens real pause menu");
            await KeyPress(Key.O);
            Check(!Tree.Paused && !pause.Visible, "rebound pause key also resumes");
            await KeyPress(Key.Escape);
            Check(Tree.Paused && pause.Visible, "fixed escape still provides reliable pause access");
            pause.SettingsButton.EmitSignal(BaseButton.SignalName.Pressed);
            Find<Button>(pause.SettingsMenu, "EditBindings").EmitSignal(BaseButton.SignalName.Pressed);
            editor = Find<ControlBindingsUI>(pause.SettingsMenu);
            Find<Button>(editor, "ResetBindings").EmitSignal(BaseButton.SignalName.Pressed);
            Check(ControlBindings.Hint("interact") == "E" && ControlBindings.Hint("shoot") == "Left mouse", "restore defaults updates all keyboard and mouse actions");
            Find<Button>(editor, "BackBindings").EmitSignal(BaseButton.SignalName.Pressed);
            await KeyPress(Key.Escape);
            Check(Tree.Paused && ControlBindings.Hint("interact") == "G", "cancelling reset restores custom controls without releasing pause");
            pause.SettingsMenu.Open();
            Find<Button>(pause.SettingsMenu, "EditBindings").EmitSignal(BaseButton.SignalName.Pressed);
            Find<Button>(editor, "ResetBindings").EmitSignal(BaseButton.SignalName.Pressed);
            Find<Button>(editor, "BackBindings").EmitSignal(BaseButton.SignalName.Pressed);
            Find<Button>(pause.SettingsMenu, "SaveSettings").EmitSignal(BaseButton.SignalName.Pressed);
            PlayerSettings.Reload();
            Check(ControlBindings.Hint("interact") == "E" && ControlBindings.Hint("shoot") == "Left mouse", "saved restore defaults survives reload");
            Tree.Paused = false;
            GD.Print($"CONTROLS SMOKE PASS: {_checks} checks");
            Tree.CurrentScene.QueueFree();
            await ToSignal(Tree.CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            Tree.Quit();
        }
        catch (Exception error) { GD.PushError("CONTROLS SMOKE FAIL: " + error); Tree.Quit(1); }
    }

    private async Task Rebind(ControlBindingsUI editor, string action, Key code)
    {
        Find<Button>(editor, "Bind_" + action).EmitSignal(BaseButton.SignalName.Pressed);
        await KeyPress(code);
    }
    private async Task KeyPress(Key code) => await HoldKey(code, 0.04);
    private async Task HoldKey(Key code, double seconds)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = true });
        await ToSignal(Tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = false });
        await Frames();
    }
    private async Task MousePress(MouseButton button)
    {
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = false });
        await Frames();
    }
    private async Task Frames()
    { for (int i = 0; i < 4; i++) await ToSignal(Tree, SceneTree.SignalName.ProcessFrame); }
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture-playtest")) return;
        await Frames(); RenderingServer.ForceDraw(false);
        using var screenshot = GetViewport().GetTexture().GetImage();
        Check(screenshot.SavePng($"/tmp/foxtrot-{name}.png") == Error.Ok, "capture " + name);
    }
    private static IEnumerable<T> All<T>(Node root) where T : Node
    {
        if (root is T match) yield return match;
        foreach (Node child in root.GetChildren()) foreach (var nested in All<T>(child)) yield return nested;
    }
    private static T Find<T>(Node root, string name = null) where T : Node => All<T>(root).First(node => name == null || node.Name == name);
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("PASS " + message);
    }
}
