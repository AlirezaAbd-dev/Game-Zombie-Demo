using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.UI;

namespace TopDownDemo.Systems;

public partial class CampaignActDirector : Node
{
    [Export] public StoryAct CurrentAct { get; set; } = StoryAct.Act1_Arrival;
    [Export] public string SceneId { get; set; } = "act1_gate";
    [Export] public string ObjectiveTitle { get; set; } = "RECONNAISSANCE";
    [Export] public string ObjectiveSubtask { get; set; } = "Infiltrate Blacksite Foxtrot-7 perimeter";
    [Export] public string StartDialogueId { get; set; } = "act1_intro";
    [Export] public float StartDialogueDelay { get; set; } = 0.8f;

    public override void _Ready()
    {
        EnsureSystems();

        if (StoryDirector.Instance != null)
        {
            StoryDirector.Instance.CurrentAct = CurrentAct;
            StoryDirector.Instance.EmitSignal(StoryDirector.SignalName.ObjectiveUpdated, ObjectiveTitle, ObjectiveSubtask);
        }

        // Play Act music
        AudioManager.Instance?.PlaySceneMusic(SceneId);

        // Apply difficulty scaling
        ApplyDifficultySettings();

        // Trigger intro dialogue if specified
        if (!string.IsNullOrEmpty(StartDialogueId))
        {
            GetTree().CreateTimer(StartDialogueDelay).Timeout += () =>
            {
                var seq = DialogueDatabase.Get(StartDialogueId);
                if (seq != null && DialogueManager.Instance != null && !DialogueManager.Instance.IsDialogueActive)
                {
                    DialogueManager.Instance.StartDialogue(seq);
                }
            };
        }
    }

    private void EnsureSystems()
    {
        // Auto-instantiate singletons if missing from scene
        if (SaveSystem.Instance == null)
        {
            AddChild(new SaveSystem { Name = "SaveSystem" });
        }
        if (StealthSystem.Instance == null)
        {
            AddChild(new StealthSystem { Name = "StealthSystem" });
        }
        if (DialogueManager.Instance == null)
        {
            AddChild(new DialogueManager { Name = "DialogueManager" });
        }
        if (SceneTransitionManager.Instance == null)
        {
            AddChild(new SceneTransitionManager { Name = "SceneTransitionManager" });
        }
        if (CutsceneDirector.Instance == null)
        {
            AddChild(new CutsceneDirector { Name = "CutsceneDirector" });
        }
    }

    private void ApplyDifficultySettings()
    {
        DifficultyLevel diff = StoryDirector.Instance?.Difficulty ?? SaveSystem.Instance?.ActiveSaveData?.Difficulty ?? DifficultyLevel.Survivor;

        var zombies = GetTree().GetNodesInGroup("zombies");
        foreach (var node in zombies)
        {
            if (node is Zombie zombie && zombie.Stats != null)
            {
                if (diff == DifficultyLevel.Nightmare)
                {
                    zombie.Stats.AttackDamage *= 1.35f;
                    zombie.VisionRange *= 1.15f;
                }
                else if (diff == DifficultyLevel.Researcher)
                {
                    zombie.Stats.AttackDamage *= 1.8f;
                    zombie.VisionRange *= 1.35f;
                    zombie.ProximityHearRange *= 1.4f;
                }
            }
        }
    }
}
