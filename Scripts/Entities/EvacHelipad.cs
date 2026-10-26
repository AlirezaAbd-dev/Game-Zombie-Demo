using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities;

public partial class EvacHelipad : Area2D
{
    [Export] public PointLight2D BeaconLight { get; set; }
    [Export] public Sprite2D PadSprite { get; set; }
    [Export] public InteractableComponent Interactable { get; set; }
    [Export] public HelicopterExtractionController Extraction { get; set; }

    private float _beaconPulse = 0.0f;

    public override void _Ready()
    {
        BeaconLight ??= GetNodeOrNull<PointLight2D>("%BeaconLight");
        PadSprite ??= GetNodeOrNull<Sprite2D>("%PadSprite");
        Interactable ??= GetNodeOrNull<InteractableComponent>("%InteractableComponent");

        if (Interactable != null)
        {
            Interactable.Interacted += OnInteracted;
        }

        if (StoryDirector.Instance != null)
        {
            StoryDirector.Instance.PhaseChanged += OnStoryPhaseChanged;
            StoryDirector.Instance.FlagsChanged += UpdateBeaconState;
        }

        UpdateBeaconState();
        if (Extraction != null) Extraction.StatusChanged += UpdateBeaconState;
    }

    private void OnStoryPhaseChanged(int phaseVal)
    {
        UpdateBeaconState();
    }

    private void UpdateBeaconState()
    {
        var phase = StoryDirector.Instance?.CurrentPhase ?? StoryPhase.Act1_Blackout;
        bool readyForEvac = phase == StoryPhase.Act3_HelipadEvacuation || phase == StoryPhase.Act3_BossBattle ||
                            (StoryDirector.Instance != null && StoryDirector.Instance.HasFlag(StoryFlags.UNIT9_DEFEATED));
        if (StoryDirector.Instance?.IsCampaign == true)
            readyForEvac = StoryDirector.Instance.HasFlag(StoryFlags.UNIT9_DEFEATED);
        if (Extraction != null) readyForEvac = Extraction.CanBoard;

        if (Interactable != null)
        {
            Interactable.IsActive = readyForEvac;
        }

        if (BeaconLight != null)
        {
            BeaconLight.Color = readyForEvac ? new Color(0.1f, 1.0f, 0.4f, 1.0f) : new Color(1.0f, 0.4f, 0.1f, 1.0f);
        }
    }

    public override void _Process(double delta)
    {
        _beaconPulse += (float)delta * 4.0f;
        if (BeaconLight != null)
        {
            BeaconLight.Energy = 1.0f + Mathf.Sin(_beaconPulse) * 0.45f;
        }
    }

    private void OnInteracted(Player player)
    {
        if (Extraction != null) { Extraction.RequestBoard(player); return; }
        if (StoryDirector.Instance?.IsCampaign == true && !StoryDirector.Instance.HasFlag(StoryFlags.UNIT9_DEFEATED)) return;
        AudioManager.Instance?.PlaySound(SoundType.RadioChime);
        if (StoryDirector.Instance != null && StoryDirector.Instance.CurrentAct == StoryAct.Act5_Extraction)
        {
            if (SaveSystem.Instance?.Save(SaveSystem.Instance.CurrentSlot, player,
                GetTree().CurrentScene.SceneFilePath, StoryAct.Act5_Extraction) != true) return;
            SceneTransitionManager.Instance?.TransitionToScene(
                "res://Scenes/Credits.tscn",
                "EXTRACTION CONFIRMED",
                "Blacksite Foxtrot-7 Neutralized"
            );
        }
        else
        {
            StoryDirector.Instance?.TriggerMissionCompleted();
        }
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(Extraction)) Extraction.StatusChanged -= UpdateBeaconState;
        if (StoryDirector.Instance == null) return;
        StoryDirector.Instance.PhaseChanged -= OnStoryPhaseChanged;
        StoryDirector.Instance.FlagsChanged -= UpdateBeaconState;
    }
}
