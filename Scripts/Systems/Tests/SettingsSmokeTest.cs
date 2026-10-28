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

/// <summary>Run with isolated XDG directories: deliberately exercises corrupt/failed preferences writes.</summary>
public partial class SettingsSmokeTest : Node
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
            PlayerSettings.Apply(new PlayerSettingsData { MasterVolume = -2, MusicVolume = 2, CameraShake = float.NaN });
            Check(PlayerSettings.Current.MasterVolume == 0 && PlayerSettings.Current.MusicVolume == 1 && PlayerSettings.Current.CameraShake == 1,
                "invalid preference ranges normalize safely");
            PlayerSettings.Apply(new PlayerSettingsData { MasterVolume = 0.75f, MusicVolume = 0.4f, EffectsVolume = 0, CameraShake = 0 });
            Check(AudioServer.IsBusMute(AudioServer.GetBusIndex(PlayerSettings.EffectsBus))
                && Mathf.IsEqualApprox(Mathf.DbToLinear(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex(PlayerSettings.MusicBus))),0.4f),
                "music and effects have independent volume and mute controls");
            Check(PlayerSettings.Save(out _),"preferences save outside campaign slots");
            PlayerSettings.Apply(new PlayerSettingsData()); PlayerSettings.Reload();
            Check(PlayerSettings.Current.CameraShake == 0 && PlayerSettings.Current.EffectsVolume == 0,"preferences survive disk reload");
            string path = ProjectSettings.GlobalizePath(PlayerSettings.FilePath);
            Directory.CreateDirectory(path + ".tmp");
            PlayerSettings.Apply(new PlayerSettingsData { MusicVolume = 0.9f });
            Check(!PlayerSettings.Save(out string error) && error.Length > 0,"failed preferences write reports a retryable error");
            Directory.Delete(path + ".tmp"); PlayerSettings.Reload();
            Check(Mathf.IsEqualApprox(PlayerSettings.Current.MusicVolume,0.4f),"failed write retains preceding settings file");
            File.WriteAllText(path,"{ broken json"); PlayerSettings.Reload();
            Check(PlayerSettings.Current.MasterVolume == 1,"corrupt preferences fall back without blocking launch");
            Check(Tree.ChangeSceneToFile("res://Scenes/MainMenu.tscn") == Error.Ok,"main menu loads");
            await Frames();
            var menu = Tree.CurrentScene;
            Find<Button>(menu,"BtnSettings").EmitSignal(BaseButton.SignalName.Pressed);
            var settings = Find<SettingsUI>(menu);
            Check(settings.Visible && settings.Size == GetViewport().GetVisibleRect().Size,"main-menu settings fills the viewport");
            Find<HSlider>(settings,"MusicVolume").Value = 25;
            Check(Mathf.IsEqualApprox(PlayerSettings.Current.MusicVolume,0.25f),"slider previews live audio changes");
            await Capture("settings");
            Find<CheckButton>(settings,"Fullscreen").ButtonPressed = true;
            Find<CheckButton>(settings,"VSync").ButtonPressed = false;
            await WaitForWindowMode(true);
            Check(PlayerSettings.Current.Fullscreen && !PlayerSettings.Current.VSync
                && (DisplayServer.GetName() == "headless" || DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen),
                "display preferences preview on the game window");
            await KeyPress(Key.Escape);
            Check(!settings.Visible && PlayerSettings.Current.MusicVolume == 1,"escape cancels preview and returns to main menu");
            await WaitForWindowMode(false);
            Check(!PlayerSettings.Current.Fullscreen && PlayerSettings.Current.VSync
                && (DisplayServer.GetName() == "headless" || DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed),
                "cancel restores preceding window and VSync preferences (actual mode: " + DisplayServer.WindowGetMode() + ")");
            settings.Open();
            Find<HSlider>(settings,"CameraShake").Value = 0;
            Find<CheckButton>(settings,"CameraLookAhead").ButtonPressed = false;
            Find<CheckButton>(settings,"FlashlightFlicker").ButtonPressed = false;
            Directory.CreateDirectory(path + ".tmp");
            Find<Button>(settings,"SaveSettings").EmitSignal(BaseButton.SignalName.Pressed);
            Check(settings.Visible && All<Label>(settings).Any(label => label.Text.StartsWith("Settings could not be saved")),
                "preferences save failure keeps dialog open with retry feedback");
            Directory.Delete(path + ".tmp");
            Find<Button>(settings,"SaveSettings").EmitSignal(BaseButton.SignalName.Pressed);
            PlayerSettings.Reload();
            Check(!settings.Visible && PlayerSettings.Current.CameraShake == 0 && !PlayerSettings.Current.CameraLookAhead
                && !PlayerSettings.Current.FlashlightFlicker,"saved comfort settings persist independently of campaign saves");
            Find<Button>(menu,"BtnNewGame").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            Check(Find<DifficultySelectUI>(menu).Size == GetViewport().GetVisibleRect().Size,"difficulty overlay covers full main menu");
            Check(GetViewport().GuiGetFocusOwner() is Button focused && focused.Text == "START SURVIVOR",
                "difficulty selection captures keyboard focus from the underlying menu");
            await Capture("difficulty");
            await KeyPress(Key.Escape);
            var saves = SaveSystem.Instance;
            saves.CreateNewGame(0,DifficultyLevel.Survivor);
            saves.ActiveSaveData.Flags.Add("intro_seen_act1_gate");
            Check(Tree.ChangeSceneToFile("res://Scenes/Act1_Gate.tscn") == Error.Ok,"campaign opens with saved preferences");
            await Frames();
            var player = Tree.GetFirstNodeInGroup("player") as Player;
            player.IsInvulnerable = true;
            var pause = Find<PauseMenuUI>(Tree.CurrentScene);
            Check(pause.ResumeButton != null && pause.SettingsButton != null && pause.QuitButton != null,"pause menu builds real actionable buttons");
            Check(Find<AudioStreamPlayer>(Tree.CurrentScene,"MusicPlayerA").Bus == PlayerSettings.MusicBus,
                "scene music uses preference bus");
            player.AddTrauma(1);
            await Frames();
            Check(player.Camera.Offset.Length() < 0.01f,"disabling shake and aim follow keeps camera steady");
            float energy = player.FlashlightComponent.Light.Energy;
            await Frames();
            Check(Mathf.IsEqualApprox(energy,player.FlashlightComponent.Light.Energy),"disabled flashlight flicker stays stable");
            await KeyPress(Key.Escape);
            Check(pause.Visible && Tree.Paused && pause.ResumeButton.HasFocus(),"escape opens visible pause menu with keyboard focus");
            var position = player.GlobalPosition;
            Input.ActionPress("move_right");
            await ToSignal(Tree.CreateTimer(0.2),SceneTreeTimer.SignalName.Timeout);
            Input.ActionRelease("move_right");
            Check(position.DistanceTo(player.GlobalPosition) < 0.01f,"movement stays frozen while pause menu is open");
            await Capture("pause-menu");
            pause.SaveButton.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            var slots = Find<SaveSlotUI>(pause);
            Check(slots.Visible && slots.Size == GetViewport().GetVisibleRect().Size
                && All<Label>(slots).Any(label => label.Text == "SELECT SAVE SLOT"),"save overlay is centered and shows current mode");
            Check(GetViewport().GuiGetFocusOwner() is Button back && back.Text == "BACK [ESC]",
                "save overlay captures keyboard focus from pause controls");
            await Capture("save-menu");
            await KeyPress(Key.Escape);
            Check(!slots.Visible && pause.Visible && Tree.Paused,"closing save overlay preserves pause ownership");
            pause.SettingsButton.EmitSignal(BaseButton.SignalName.Pressed);
            Find<HSlider>(pause.SettingsMenu,"EffectsVolume").Value = 0;
            await KeyPress(Key.Escape);
            Check(!pause.SettingsMenu.Visible && pause.Visible && Tree.Paused && PlayerSettings.Current.EffectsVolume == 1,
                "cancel settings restores audio and keeps campaign paused");
            pause.InventoryButton.EmitSignal(BaseButton.SignalName.Pressed);
            Check(!pause.Visible && Tree.Paused && Find<InventoryUI>(Tree.CurrentScene).Visible,"pause inventory button hands pause to inventory");
            await KeyPress(Key.Escape);
            Check(!Tree.Paused && !Find<InventoryUI>(Tree.CurrentScene).Visible,"closing inventory resumes gameplay");
            await KeyPress(Key.Escape); pause.MapButton.EmitSignal(BaseButton.SignalName.Pressed);
            Check(!pause.Visible && Tree.Paused && Find<MiniMapUI>(Tree.CurrentScene).IsFullscreen,"pause map button hands pause to map");
            await KeyPress(Key.Escape);
            Check(!Tree.Paused,"closing tactical map resumes gameplay");
            await KeyPress(Key.Escape); pause.ResumeButton.EmitSignal(BaseButton.SignalName.Pressed);
            Check(!pause.Visible && !Tree.Paused,"resume button returns control to campaign");
            player.FlashlightComponent.SetLightState(false,0.2f);
            await Frames();
            Check(Mathf.IsEqualApprox(player.FlashlightComponent.Light.Energy,0.2f),"flicker update preserves scripted dimmed flashlight state");
            GC.Collect(); GC.WaitForPendingFinalizers();
            GD.Print($"SETTINGS SMOKE PASS: {_checks} checks");
            Tree.CurrentScene.QueueFree();
            await ToSignal(Tree.CreateTimer(0.25),SceneTreeTimer.SignalName.Timeout);
            GC.Collect(); GC.WaitForPendingFinalizers();
            await ToSignal(Tree.CreateTimer(0.25),SceneTreeTimer.SignalName.Timeout);
            Tree.Quit();
        }
        catch (Exception error) { GD.PushError("SETTINGS SMOKE FAIL: " + error); Tree.Quit(1); }
    }

    private async Task WaitForWindowMode(bool fullscreen)
    {
        if (DisplayServer.GetName() == "headless") return;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var mode = DisplayServer.WindowGetMode();
            if ((mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen) == fullscreen) return;
            await ToSignal(Tree.CreateTimer(0.025), SceneTreeTimer.SignalName.Timeout);
        }
    }
    private async Task KeyPress(Key code)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = false });
        await Frames();
    }
    private async Task Frames()
    { for (int frame = 0; frame < 4; frame++) await ToSignal(Tree,SceneTree.SignalName.ProcessFrame); }
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture-playtest")) return;
        await Frames(); RenderingServer.ForceDraw(false);
        using var screenshot = GetViewport().GetTexture().GetImage();
        Check(screenshot.SavePng($"/tmp/foxtrot-{name}.png") == Error.Ok,$"capture {name}");
    }
    private static IEnumerable<T> All<T>(Node root) where T : Node
    {
        if (root is T match) yield return match;
        foreach (Node child in root.GetChildren()) foreach (T nested in All<T>(child)) yield return nested;
    }
    private static T Find<T>(Node root,string name = null) where T : Node => All<T>(root).First(node => name == null || node.Name == name);
    private void Check(bool condition,string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("PASS " + message);
    }
}
