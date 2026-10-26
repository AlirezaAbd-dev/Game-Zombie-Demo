using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace TopDownDemo.Systems.Tests;

/// <summary>Playback lifetime checks; Dummy audio does not assess listening quality.</summary>
public partial class AudioSmokeTest : Node
{
    private int _checks;
    private SceneTree Tree => GetTree();
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try
        {
            Tree.CurrentScene = null;
            Check(Tree.ChangeSceneToFile("res://Scenes/MainMenu.tscn") == Error.Ok, "audio fixture loads real menu");
            await Wait(1.7);
            var audio = AudioManager.Instance;
            var music = audio.GetChildren().OfType<AudioStreamPlayer>().Where(player => player.Name.ToString().StartsWith("MusicPlayer")).ToArray();
            Check(music.Length == 2 && music.Count(player => player.Playing) == 1, "completed title crossfade leaves exactly one live music voice");
            Check(music.Single(player => !player.Playing).Stream == null, "outgoing music relinquishes its stream after crossfade");
            audio.PlayMusic("gate", 0.03f); await Wait(0.12);
            Check(music.Count(player => player.Playing) == 1 && music.Count(player => player.Stream != null) == 1, "chapter crossfade stops and releases outgoing track");
            audio.PlayMusic("checkpoint", 0.3f);
            await Wait(0.03);
            audio.PlayMusic("lab", 0.03f); await Wait(0.12);
            Check(music.Count(player => player.Playing) == 1 && music.Count(player => player.Stream != null) == 1, "interrupted crossfade cannot leave a silent looping track");
            audio.PlayMusic("archive", 0.3f);
            await Wait(0.03);
            audio.StopMusic(0.03f); await Wait(0.12);
            Check(music.All(player => !player.Playing && player.Stream == null), "stop music fades and releases both sides of an active crossfade");
            audio.PlayMusic("gate", 0.03f); await Wait(0.12);
            Check(music.Count(player => player.Playing) == 1, "music restarts after a completed stop");
            audio.StopMusic(0.3f);
            audio.PlayMusic("checkpoint", 0.03f); await Wait(0.12);
            Check(music.Count(player => player.Playing) == 1 && music.Count(player => player.Stream != null) == 1, "new music supersedes a pending stop without being cleared by stale callbacks");
            Tree.Paused = true; await Wait(0.06);
            Check(music.Single(player => player.Playing).ProcessMode == ProcessModeEnum.Always, "menu pause preserves intentional background music playback");
            Tree.Paused = false;
            audio.PlaySound(SoundType.RadioChime);
            audio.StopAllPlayback(); await Wait(0.12);
            Check(audio.GetChildren().OfType<AudioStreamPlayer>().All(player => !player.Playing && player.Stream == null)
                && audio.GetChildren().OfType<AudioStreamPlayer2D>().All(player => !player.Playing && player.Stream == null), "shutdown stops and detaches music and effect streams");
            audio.StopAllPlayback();
            Check(music.All(player => !player.Playing), "repeated audio shutdown is safe");
            GD.Print($"AUDIO SMOKE PASS: {_checks} checks");
            Tree.CurrentScene.QueueFree(); await Wait(0.25); Tree.Quit();
        }
        catch (Exception error) { GD.PushError("AUDIO SMOKE FAIL: " + error); Tree.Quit(1); }
    }
    private async Task Wait(double seconds) => await ToSignal(Tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("PASS " + message);
    }
}
