using System;
using System.IO;
using System.Text.Json;
using Godot;
using TopDownDemo.Core;

namespace TopDownDemo.Systems;

/// <summary>Preferences belong to the installation profile, independently of campaign slots.</summary>
public static class PlayerSettings
{
    public const string MusicBus = "Music";
    public const string EffectsBus = "Effects";
    public const string FilePath = "user://settings.json";
    public static PlayerSettingsData Current { get; private set; } = new();
    private static bool _loaded;

    public static void EnsureLoaded()
    {
        if (!_loaded) Reload();
    }

    public static void Reload()
    {
        PlayerSettingsData data = new();
        try
        {
            string path = ProjectSettings.GlobalizePath(FilePath);
            if (File.Exists(path)) data = JsonSerializer.Deserialize<PlayerSettingsData>(File.ReadAllText(path)) ?? new();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // An invalid preferences file must never prevent loading the campaign.
            GD.Print("[Settings] Using defaults: " + error.Message);
        }
        _loaded = true;
        Apply(data);
    }

    public static void Apply(PlayerSettingsData data)
    {
        Current = data.Copy();
        Current.Normalize();
        ControlBindings.Apply(Current);
        EnsureBus(MusicBus);
        EnsureBus(EffectsBus);
        SetVolume("Master", Current.MasterVolume);
        SetVolume(MusicBus, Current.MusicVolume);
        SetVolume(EffectsBus, Current.EffectsVolume);
        if (DisplayServer.GetName() == "headless") return;
        var mode = DisplayServer.WindowGetMode();
        bool fullscreen = mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        if (fullscreen != Current.Fullscreen)
            DisplayServer.WindowSetMode(Current.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetVsyncMode(Current.VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
    }

    public static bool Save(out string errorMessage)
    {
        errorMessage = "";
        string path = ProjectSettings.GlobalizePath(FilePath);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(Current));
            File.Move(path + ".tmp", path, true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            errorMessage = "Settings could not be saved. Check disk space and folder permissions, then retry.";
            GD.Print("[Settings] " + error.Message);
            return false;
        }
    }

    private static void EnsureBus(string name)
    {
        if (AudioServer.GetBusIndex(name) >= 0) return;
        AudioServer.AddBus();
        int index = AudioServer.BusCount - 1;
        AudioServer.SetBusName(index, name);
        AudioServer.SetBusSend(index, "Master");
    }

    private static void SetVolume(string name, float volume)
    {
        int index = AudioServer.GetBusIndex(name);
        AudioServer.SetBusMute(index, volume <= 0);
        AudioServer.SetBusVolumeDb(index, volume > 0 ? Mathf.LinearToDb(volume) : -80);
    }
}
