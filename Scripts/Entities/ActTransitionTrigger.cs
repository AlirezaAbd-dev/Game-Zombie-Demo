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

    private bool _triggered = false;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1; // Detect player layer 1
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_triggered) return;

        if (body is Player player && !player.IsDead)
        {
            if (!string.IsNullOrEmpty(RequiredStoryFlag) && StoryDirector.Instance != null && !StoryDirector.Instance.HasFlag(RequiredStoryFlag))
            {
                HUDUI.Instance?.ShowHealNotification("SECURITY LOCKDOWN", 0, LockedMessage);
                AudioManager.Instance?.PlaySound(SoundType.AlarmKlaxon);
                return;
            }

            _triggered = true;
            if (StoryDirector.Instance != null)
            {
                StoryDirector.Instance.CurrentAct = TargetAct;
            }

            // Auto-save progress at checkpoint
            SaveSystem.Instance?.Save(SaveSystem.Instance.CurrentSlot, player, TargetScenePath, TargetAct);

            SceneTransitionManager.Instance?.TransitionToScene(TargetScenePath, ActTitle, Subtitle);
        }
    }
}
