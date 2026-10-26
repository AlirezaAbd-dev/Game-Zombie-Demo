using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;
using TopDownDemo.UI;

namespace TopDownDemo.Entities;

public partial class ActTransitionTrigger : Area2D
{
    [Export] public string TargetScenePath { get; set; } = "res://Scenes/Act2_Checkpoint.tscn";
    [Export] public string ActTitle { get; set; } = "ACT 2: THE BUNKER";
    [Export] public string Subtitle { get; set; } = "Security Checkpoint";
    [Export] public string RequiredStoryFlag { get; set; } = "";
    [Export] public string LockedMessage { get; set; } = "GATE LOCKED - COMPLETE OBJECTIVE FIRST";
    [Export] public StoryAct TargetAct { get; set; } = StoryAct.Act2_Bunker;
    [Export] public bool RequireInteraction { get; set; }
    [Export] public string DepartureFlag { get; set; } = string.Empty;
    [Export] public TopDownDemo.Entities.Components.InteractableComponent ExitInteractable { get; set; }

    private bool _triggered = false;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1; // Detect player layer 1
        BodyEntered += OnBodyEntered;
        if (RequireInteraction)
        {
            if (ExitInteractable == null)
            {
                GD.PushError($"Explicit scene exit requires an interaction area: {GetPath()}");
                return;
            }
            ExitInteractable.RequiredStoryFlag = RequiredStoryFlag;
            ExitInteractable.LockedPromptText = LockedMessage;
            ExitInteractable.Interacted += player => TryTransition(player);
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (!RequireInteraction && body is Player player) TryTransition(player);
    }

    public bool TryTransition(Player player)
    {
        if (_triggered || player == null || player.IsDead) return false;
        var transitions = SceneTransitionManager.Instance;
        var saves = SaveSystem.Instance;
        if (transitions == null || transitions.IsTransitioning || saves == null) return false;
        if (!string.IsNullOrEmpty(RequiredStoryFlag) && StoryDirector.Instance?.HasFlag(RequiredStoryFlag) != true)
        {
            HUDUI.Instance?.ShowHealNotification("SECURITY LOCKDOWN", 0, LockedMessage);
            return false;
        }
        bool departureWasSet = !string.IsNullOrEmpty(DepartureFlag) && StoryDirector.Instance?.HasFlag(DepartureFlag) == true;
        if (!string.IsNullOrEmpty(DepartureFlag)) StoryDirector.Instance?.SetFlag(DepartureFlag);
        if (!saves.Save(saves.CurrentSlot, player, TargetScenePath, TargetAct))
        {
            if (!departureWasSet && !string.IsNullOrEmpty(DepartureFlag)) StoryDirector.Instance?.RemoveFlag(DepartureFlag);
            HUDUI.Instance?.ShowFloatingNotification("CHECKPOINT FAILED - TRY THE EXIT AGAIN", Colors.OrangeRed);
            return false;
        }
        _triggered = true;
        if (StoryDirector.Instance != null) StoryDirector.Instance.CurrentAct = TargetAct;
        transitions.TransitionToScene(TargetScenePath, ActTitle, Subtitle);
        return true;
    }
}
