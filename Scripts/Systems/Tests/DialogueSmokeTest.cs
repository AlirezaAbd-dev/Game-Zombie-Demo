using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.UI;

namespace TopDownDemo.Systems.Tests;

public partial class DialogueSmokeTest : Node
{
    private int _checks;
    private SceneTree Tree => GetTree();
    private DialogueManager Dialogue => DialogueManager.Instance;
    private Player Actor => (Player)Tree.GetFirstNodeInGroup("player");
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try
        {
            Tree.CurrentScene = null;
            var saves = SaveSystem.EnsureInstance(this);
            saves.CreateNewGame(0, DifficultyLevel.Survivor);
            saves.ActiveSaveData.Flags.Add("intro_seen_act1_gate");
            Check(Tree.ChangeSceneToFile("res://Scenes/Act1_Gate.tscn") == Error.Ok, "dialogue fixture loads real campaign");
            await Wait(0.15);
            Actor.IsInvulnerable = true;
            var sequence = Sequence("Read this first.", "Read this second.");
            Dialogue.StartDialogue(sequence);
            Check(Tree.Paused && Actor.IsInputLocked, "conversation owns pause and player input");
            await KeyPress(Key.E);
            for (int repeat = 0; repeat < 4; repeat++) await KeyPress(Key.E, echo: true);
            Check(Dialogue.IsDialogueActive && Dialogue.CurrentLineIndex == 0, "held continue key cannot skip lines through key-repeat");
            await KeyPress(Key.E);
            Check(Dialogue.IsDialogueActive && Dialogue.CurrentLineIndex == 1, "fresh continue press advances exactly one line");
            Dialogue.CancelDialogue();
            var settings = PlayerSettings.Current.Copy();
            Check(ControlBindings.TryRebind(settings, "weapon_1", (int)Key.J, out _) && ControlBindings.TryRebind(settings, "interact", (int)Key.Key1, out _), "interact can share a contextual choice number");
            PlayerSettings.Apply(settings);
            int chosen = -1;
            sequence = Sequence("Choose a route.");
            sequence.Lines[0].Choices.Add(new DialogueChoiceResource { ChoiceText = "Hidden route", RequiredFlag = "dialogue_test_unavailable" });
            sequence.Lines[0].Choices.Add(new DialogueChoiceResource { ChoiceText = "Visible route", SetStoryFlag = "dialogue_test_chosen" });
            Dialogue.StartDialogue(sequence, index => chosen = index);
            await KeyPress(Key.Enter);
            Check(Dialogue.ChoicesContainer.GetChildCount() == 1 && Dialogue.ChoicesContainer.GetChild<Button>(0).Text.StartsWith("[1]"), "filtered choices are numbered by visible position");
            await KeyPress(Key.Key1, echo: true);
            Check(Dialogue.IsDialogueActive && chosen == -1, "number-key repeat cannot choose an answer");
            await Capture("dialogue-choice");
            await KeyPress(Key.Key1);
            Check(!Dialogue.IsDialogueActive && chosen == 1 && StoryDirector.Instance.HasFlag("dialogue_test_chosen"), "visible choice takes priority over rebound interact and retains original branch index");
            Check(!Tree.Paused && !Actor.IsInputLocked, "choice completion returns gameplay control");
            sequence = Sequence("Only a gated answer.", "Continue along the public route.");
            sequence.Lines[0].Choices.Add(new DialogueChoiceResource { ChoiceText = "Unavailable", RequiredFlag = "dialogue_test_unavailable" });
            Dialogue.StartDialogue(sequence);
            await KeyPress(Key.Enter);
            Check(!Dialogue.ChoicesContainer.Visible && All<Label>(Dialogue).Any(label => label.Visible && label.Text.Contains("Continue")), "all-hidden answers expose a visible continue prompt");
            Check(All<Label>(Dialogue).Any(label => label.Text.StartsWith("[1] / [SPACE]")), "conversation refreshes its continue hint after rebinding");
            await KeyPress(Key.Key1);
            Check(Dialogue.CurrentLineIndex == 1, "all-hidden answer list still follows the linear route");
            Dialogue.CancelDialogue();
            PlayerSettings.Apply(new());
            sequence = Sequence("First answer.", "Second answer.", "Final text.");
            sequence.Lines[0].Choices.Add(new DialogueChoiceResource { ChoiceText = "First", SetStoryFlag = "dialogue_test_first" });
            sequence.Lines[1].Choices.Add(new DialogueChoiceResource { ChoiceText = "Second", SetStoryFlag = "dialogue_test_second", NextLineIndex = 2 });
            Dialogue.StartDialogue(sequence);
            await KeyPress(Key.E);
            var firstButton = Dialogue.ChoicesContainer.GetChild<Button>(0);
            firstButton.EmitSignal(BaseButton.SignalName.Pressed);
            int afterFirst = Dialogue.CurrentLineIndex;
            firstButton.EmitSignal(BaseButton.SignalName.Pressed);
            Check(Dialogue.CurrentLineIndex == afterFirst, "duplicate stale choice activation cannot advance or rewind dialogue");
            await KeyPress(Key.E);
            var secondButton = Dialogue.ChoicesContainer.GetChild<Button>(0);
            Dialogue.CancelDialogue();
            secondButton.EmitSignal(BaseButton.SignalName.Pressed);
            Check(!StoryDirector.Instance.HasFlag("dialogue_test_first") && !StoryDirector.Instance.HasFlag("dialogue_test_second"), "queued choice after cancellation cannot restore rolled-back story flags");
            Check(!Tree.Paused && !Actor.IsInputLocked, "cancelled conversation releases gameplay safely");
            // A normal UI click may be mapped to fire as well as dismiss a conversation.
            sequence = Sequence("Conversation complete.");
            Dialogue.StartDialogue(sequence);
            Input.ActionPress("shoot");
            int ammunition = Actor.InventoryComponent.CurrentAmmo;
            Dialogue.EndDialogue();
            await Wait(0.12);
            Check(Actor.InventoryComponent.CurrentAmmo == ammunition && !Input.IsActionPressed("shoot"), "conversation dismissal cannot carry a held trigger into gameplay");
            Input.ActionRelease("shoot");
            Tree.Paused = true; Actor.IsInputLocked = true;
            Dialogue.StartDialogue(Sequence("Already paused."));
            Dialogue.CancelDialogue();
            Check(Tree.Paused && Actor.IsInputLocked, "cancellation preserves a caller's existing pause and input lock");
            Tree.Paused = false; Actor.IsInputLocked = false;
            await VerifyModalHandoffs();
            GC.Collect(); GC.WaitForPendingFinalizers();
            GD.Print($"DIALOGUE SMOKE PASS: {_checks} checks");
            Tree.CurrentScene.QueueFree(); await Wait(0.3); Tree.Quit();
        }
        catch (Exception error) { GD.PushError("DIALOGUE SMOKE FAIL: " + error); Tree.Quit(1); }
    }
    private async Task VerifyModalHandoffs()
    {
        var inventory = All<InventoryUI>(Tree.CurrentScene).First();
        await VerifyExit("inventory", inventory.ToggleInventory, () => inventory.CloseButton.EmitSignal(BaseButton.SignalName.Pressed));
        var map = All<MiniMapUI>(Tree.CurrentScene).First();
        await VerifyExit("map", map.ToggleFullscreenMap, map.ToggleFullscreenMap);
        var pause = All<PauseMenuUI>(Tree.CurrentScene).First();
        await VerifyExit("pause menu", pause.TogglePause, () => pause.ResumeButton.EmitSignal(BaseButton.SignalName.Pressed));
        var sequenceView = new SequencePanelUI(); Tree.CurrentScene.AddChild(sequenceView);
        var sequenceData = new SequencePanelData { Steps = new[] { "TEST SWITCH" } };
        await VerifyExit("mechanical panel", () => sequenceView.Open(sequenceData, Actor), () => All<Button>(sequenceView).First(button => button.Text == "BACK [ESC]").EmitSignal(BaseButton.SignalName.Pressed));
        var powerView = new PowerRoutingUI(); Tree.CurrentScene.AddChild(powerView);
        var powerData = new PowerRoutingData();
        await VerifyExit("power panel", () => powerView.Open(powerData, Actor), () => All<Button>(powerView).First(button => button.Text == "BACK [ESC]").EmitSignal(BaseButton.SignalName.Pressed));
        Actor.IsInputLocked = true; Input.MouseMode = Input.MouseModeEnum.Visible;
        powerView.Open(powerData, Actor); powerView.Close();
        Check(Actor.IsInputLocked && Input.MouseMode == Input.MouseModeEnum.Visible, "power panel restores the caller's input lock and cursor state");
        Actor.IsInputLocked = false;
        // Discard actions at a chapter handoff as well as when closing modal screens.
        Input.ActionPress("shoot");
        SceneTransitionManager.Instance.TransitionToScene("res://Scenes/Act1_Gate.tscn", duration: 0.01f);
        await Wait(0.4);
        Check(!Tree.Paused && !Input.IsActionPressed("shoot") && !Actor.IsDead, "chapter transition drops held trigger and restores responsive gameplay");
    }
    private async Task VerifyExit(string name, Action open, Action close)
    {
        open();
        Check(Tree.Paused, name + " pauses combat while open");
        int ammunition = Actor.InventoryComponent.CurrentAmmo;
        Input.ActionPress("shoot");
        close(); await Wait(0.12);
        Check(!Tree.Paused && Actor.InventoryComponent.CurrentAmmo == ammunition && !Input.IsActionPressed("shoot"), name + " returns control without firing the closing click");
    }
    private static DialogueSequence Sequence(params string[] texts)
    {
        var sequence = new DialogueSequence();
        foreach (string text in texts) sequence.Lines.Add(new DialogueLineResource { Text = text, PortraitKey = "" });
        return sequence;
    }
    private async Task KeyPress(Key code, bool echo = false)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = true, Echo = echo });
        await Wait(0.025);
        Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = false });
        await Wait(0.025);
    }
    private async Task Wait(double seconds) => await ToSignal(Tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture-playtest")) return;
        RenderingServer.ForceDraw(false);
        using var screenshot = GetViewport().GetTexture().GetImage();
        Check(screenshot.SavePng($"/tmp/foxtrot-{name}.png") == Error.Ok, "capture " + name);
    }
    private static IEnumerable<T> All<T>(Node root) where T : Node
    {
        if (root is T match) yield return match;
        foreach (Node child in root.GetChildren()) foreach (var nested in All<T>(child)) yield return nested;
    }
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("PASS " + message);
    }
}
