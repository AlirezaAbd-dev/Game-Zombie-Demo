using System;
using System.Threading.Tasks;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;

namespace TopDownDemo.Systems.Tests;

public partial class TimingSmokeTest : Node
{
    private const string Opening = "res://Scenes/Act1_Gate.tscn";
    private int _checks;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        var tree = GetTree();
        try
        {
            tree.CurrentScene = null;
            var saves = SaveSystem.EnsureInstance(this);
            saves.CreateNewGame(0,DifficultyLevel.Survivor);
            saves.ActiveSaveData.Flags.Add("intro_seen_act1_gate");
            Check(tree.ChangeSceneToFile(Opening) == Error.Ok,"timing fixture loads");
            for (int i = 0; i < 5; i++) await ToSignal(tree,SceneTree.SignalName.ProcessFrame);
            var player = tree.GetFirstNodeInGroup("player") as Player;
            player.IsInputLocked = true; player.IsInvulnerable = true;
            await Wait(0.3);
            var timing = saves.ActiveSaveData.SceneTimings[Opening];
            Check(timing.ActiveSeconds > 0.2f,"active chapter time advances during simulation");
            var sequence = new DialogueSequence();
            sequence.Lines.Add(new DialogueLineResource { Text = "Pacing measurement fixture." });
            DialogueManager.Instance.StartDialogue(sequence);
            float active = timing.ActiveSeconds;
            await Wait(0.3);
            Check(Mathf.IsEqualApprox(timing.ActiveSeconds,active) && timing.DialogueSeconds > 0.2f,
                "dialogue contributes narrative time without running simulation clock");
            DialogueManager.Instance.EndDialogue();
            tree.Paused = true;
            float dialogue = timing.DialogueSeconds;
            await Wait(0.3);
            Check(Mathf.IsEqualApprox(timing.ActiveSeconds,active) && Mathf.IsEqualApprox(timing.DialogueSeconds,dialogue),
                "manual pause contributes neither clock");
            tree.Paused = false;
            Check(saves.Save(0,player,Opening,StoryAct.Act1_Arrival),"chapter timing checkpoint saves");
            player.HealthComponent.SetCurrentHealth(0);
            var current = saves.ActiveSaveData.SceneTimings[Opening];
            float deadActive = current.ActiveSeconds;
            await Wait(0.3);
            Check(Mathf.IsEqualApprox(current.ActiveSeconds,deadActive),"waiting after death does not inflate active playtime");
            Check(saves.Load(0),"timing checkpoint reloads");
            Check(saves.ActiveSaveData.SceneTimings[Opening].DialogueSeconds > 0.2f
                && saves.ActiveSaveData.DialoguePlaytimeSeconds > 0.2f,"dialogue and per-scene clocks survive disk roundtrip");
            GD.Print($"TIMING SMOKE PASS: {_checks} checks");
            tree.CurrentScene.QueueFree(); await Wait(0.2); tree.Quit();
        }
        catch (Exception error) { GD.PushError("TIMING SMOKE FAIL: " + error); tree.Quit(1); }
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private void Check(bool condition,string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("PASS " + message);
    }
}
