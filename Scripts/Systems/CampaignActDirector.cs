using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.UI;

namespace TopDownDemo.Systems;

public partial class CampaignActDirector : Node
{
    public static CampaignActDirector Instance { get; private set; }
    [Export] public StoryAct CurrentAct { get; set; } = StoryAct.Act1_Arrival;
    [Export] public string SceneId { get; set; } = "act1_gate";
    [Export] public int LayoutVersion { get; set; } = 1;
    [Export] public string ObjectiveTitle { get; set; } = "RECONNAISSANCE";
    [Export] public string ObjectiveSubtask { get; set; } = "Infiltrate Blacksite Foxtrot-7 perimeter";
    [Export] public string StartDialogueId { get; set; } = "act1_intro";
    [Export] public float StartDialogueDelay { get; set; } = 0.8f;
    [Export] public Godot.Collections.Array<CampaignObjective> Objectives { get; set; } = new();
    [Export] public Godot.Collections.Array<CampaignRoomData> Rooms { get; set; } = new();
    [Export] public CanvasModulate AmbientLight { get; set; }
    public CampaignObjective ActiveObjective { get; private set; }
    private StoryDirector _story;
    private Rect2? _playableBounds;

    public override void _EnterTree()
    {
        Instance = this;
        var saves = SaveSystem.EnsureInstance(this);
        if (!saves.HasActiveSession) saves.CreateNewGame(0, DifficultyLevel.Survivor, saveImmediately: false);
    }

    public override void _Ready()
    {
        foreach (var room in Rooms)
            _playableBounds = _playableBounds.HasValue ? _playableBounds.Value.Merge(room.Bounds) : room.Bounds;
        if (StealthSystem.Instance == null) AddChild(new StealthSystem { Name = "StealthSystem" });
        if (DialogueManager.Instance == null) AddChild(new DialogueManager { Name = "DialogueManager" });
        if (SceneTransitionManager.Instance == null) AddChild(new SceneTransitionManager { Name = "SceneTransitionManager" });
        if (CutsceneDirector.Instance == null) AddChild(new CutsceneDirector { Name = "CutsceneDirector" });
        CallDeferred(nameof(InitializeCampaign));
        AddChild(new CampaignLootDirector { Name = "CampaignLootDirector" });
        AddChild(new CampaignEnvironmentView { Name = "EnvironmentView", Director = this });
    }

    public Vector2 ConstrainPosition(Vector2 position)
    {
        if (!_playableBounds.HasValue) return position;
        Rect2 bounds = _playableBounds.Value;
        return position.Clamp(bounds.Position + Vector2.One * 48, bounds.End - Vector2.One * 48);
    }

    private void InitializeCampaign()
    {
        _story = StoryDirector.Instance;
        if (_story == null)
        {
            GD.PushError("Campaign requires a StoryDirector in the level.");
            return;
        }
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        SaveSystem.Instance.ApplySaveDataToLoadedScene(player);
        _story.CurrentAct = CurrentAct;
        _story.FlagsChanged += RefreshObjectives;
        RefreshObjectives();
        AudioManager.Instance?.PlaySceneMusic(SceneId);
        ApplyDifficultySettings();

        string introFlag = "intro_seen_" + SceneId;
        if (!string.IsNullOrEmpty(StartDialogueId) && !_story.HasFlag(introFlag))
        {
            var timer = GetTree().CreateTimer(StartDialogueDelay, processAlways: false);
            timer.Timeout += () =>
            {
                if (!GodotObject.IsInstanceValid(this) || !IsInsideTree()) return;
                if (DialogueManager.Instance?.IsDialogueActive == false)
                    DialogueManager.Instance.StartDialogue(DialogueDatabase.Get(StartDialogueId), _ => _story.SetFlag(introFlag));
            };
        }
    }

    private void RefreshObjectives()
    {
        ActiveObjective = null;
        foreach (var objective in Objectives)
        {
            if (!string.IsNullOrEmpty(objective.RequiredFlag) && !_story.HasFlag(objective.RequiredFlag)) continue;
            if (!string.IsNullOrEmpty(objective.ExcludedFlag) && _story.HasFlag(objective.ExcludedFlag)) continue;
            if (!_story.HasFlag(objective.CompletionFlag))
            {
                ActiveObjective = objective;
                break;
            }
        }
        _story.EmitSignal(StoryDirector.SignalName.ObjectiveUpdated,
            ActiveObjective?.Title ?? ObjectiveTitle, ActiveObjective?.Instruction ?? ObjectiveSubtask);
        if (AmbientLight != null)
            AmbientLight.Color = _story.HasFlag(StoryFlags.FACILITY_POWER_RESTORED)
                ? new Color(0.32f, 0.39f, 0.43f) : new Color(0.10f, 0.13f, 0.18f);
    }

    private void ApplyDifficultySettings()
    {
        DifficultyLevel difficulty = _story.Difficulty;
        foreach (var node in GetTree().GetNodesInGroup("zombies"))
        {
            if (node is not Zombie zombie || zombie.Stats == null) continue;
            // Keep shared authoring resources immutable; difficulty belongs to the actor.
            float multiplier = difficulty == DifficultyLevel.Nightmare ? 1.35f
                : difficulty == DifficultyLevel.Researcher ? 1.8f : 1.0f;
            zombie.AttackDamageMultiplier = multiplier;
            zombie.VisionRange *= difficulty == DifficultyLevel.Survivor ? 1.0f : 1.15f;
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input.IsActionPressed("restart") && GetTree().GetFirstNodeInGroup("player") is Player { IsDead: true })
        {
            RestartCheckpoint();
            GetViewport().SetInputAsHandled();
        }
    }

    public void RestartCheckpoint()
    {
        var saves = SaveSystem.Instance;
        if (!saves.Load(saves.CurrentSlot)) return;
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        SceneTransitionManager.Instance.TransitionToScene(saves.ActiveSaveData.CurrentScenePath, "CHECKPOINT", "Restoring your last saved progress");
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_story)) _story.FlagsChanged -= RefreshObjectives;
        if (Instance == this) Instance = null;
    }
}
