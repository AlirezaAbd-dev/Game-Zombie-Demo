using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.UI;

namespace TopDownDemo.Systems.Tests;

/// <summary>Parent process must assert exit code zero; the fixture uses the real Quit buttons.</summary>
public partial class ExitSmokeTest : Node
{
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        var tree = GetTree();
        try
        {
            tree.CurrentScene = null;
            string mode = new[] { "menu", "pause", "combat", "window", "dialogue", "transition" }
                .FirstOrDefault(candidate => OS.GetCmdlineUserArgs().Contains("--exit-" + candidate)) ?? "menu";
            var watchdog = tree.CreateTimer(10);
            watchdog.Timeout += () => { GD.PushError("EXIT SMOKE FAIL: Quit did not terminate process"); tree.Quit(1); };
            if (mode is "menu" or "window")
            {
                if (tree.ChangeSceneToFile("res://Scenes/MainMenu.tscn") != Error.Ok) throw new InvalidOperationException("Menu failed to load");
                await ToSignal(tree.CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
                GD.Print("EXIT SMOKE REQUEST: " + mode);
                if (mode == "window") tree.Root.EmitSignal(Window.SignalName.CloseRequested);
                else
                {
                    var quit = All<Button>(tree.CurrentScene).First(button => button.Name == "BtnQuit");
                    quit.EmitSignal(BaseButton.SignalName.Pressed);
                    quit.EmitSignal(BaseButton.SignalName.Pressed); // Requests must be idempotent.
                }
            }
            else
            {
                var saves = SaveSystem.EnsureInstance(this);
                saves.CreateNewGame(0, DifficultyLevel.Survivor);
                saves.ActiveSaveData.Flags.Add("intro_seen_act1_gate");
                if (tree.ChangeSceneToFile("res://Scenes/Act1_Gate.tscn") != Error.Ok) throw new InvalidOperationException("Campaign failed to load");
                await ToSignal(tree.CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
                var player = (Player)tree.GetFirstNodeInGroup("player");
                if (mode == "dialogue")
                {
                    var sequence = new DialogueSequence();
                    sequence.Lines.Add(new DialogueLineResource { Text = "Interrupted by a window close request." });
                    DialogueManager.Instance.StartDialogue(sequence);
                    GD.Print("EXIT SMOKE REQUEST: dialogue");
                    tree.Root.EmitSignal(Window.SignalName.CloseRequested);
                    return;
                }
                if (mode == "transition")
                {
                    SceneTransitionManager.Instance.TransitionToScene("res://Scenes/MainMenu.tscn", duration: 1);
                    GD.Print("EXIT SMOKE REQUEST: transition");
                    tree.Root.EmitSignal(Window.SignalName.CloseRequested);
                    return;
                }
                if (mode == "combat") player.Shoot();
                var pause = All<PauseMenuUI>(tree.CurrentScene).First();
                pause.TogglePause();
                if (!tree.Paused || !pause.Visible) throw new InvalidOperationException("Pause did not open");
                GD.Print("EXIT SMOKE REQUEST: " + mode + " quit button");
                pause.QuitButton.EmitSignal(BaseButton.SignalName.Pressed);
            }
        }
        catch (Exception error) { GD.PushError("EXIT SMOKE FAIL: " + error); tree.Quit(1); }
    }
    private static IEnumerable<T> All<T>(Node root) where T : Node
    {
        if (root is T match) yield return match;
        foreach (Node child in root.GetChildren()) foreach (var nested in All<T>(child)) yield return nested;
    }
}
