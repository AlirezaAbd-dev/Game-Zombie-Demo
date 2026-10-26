using System;
using System.Collections.Generic;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;

namespace TopDownDemo.Systems;

public partial class StoryDirector : Node
{
    private static StoryDirector _instance;
    public static StoryDirector Instance
    {
        get => GodotObject.IsInstanceValid(_instance) ? _instance : null;
        private set => _instance = value;
    }

    [Signal]
    public delegate void PhaseChangedEventHandler(int newPhase);

    [Signal]
    public delegate void ObjectiveUpdatedEventHandler(string title, string subtask);

    [Signal]
    public delegate void RadioTransmissionTriggeredEventHandler(string speaker, string message, float duration);

    [Signal]
    public delegate void PurgeTimerUpdatedEventHandler(float remainingSeconds, bool isVisible);

    [Signal]
    public delegate void DefenseProgressUpdatedEventHandler(float progress, bool isVisible);

    [Signal]
    public delegate void BossSpawnedEventHandler(Zombie boss);

    [Signal]
    public delegate void BossHealthUpdatedEventHandler(float current, float max);

    [Signal]
    public delegate void MissionCompletedEventHandler();

    [Signal]
    public delegate void FacilityPowerRestoredEventHandler();

    [Export] public EnemyStats BossStats { get; set; }
    [Export] public PackedScene ZombieScene { get; set; }
    [Export] public bool IsCampaign { get; set; } = false;

    public StoryPhase CurrentPhase { get; private set; } = StoryPhase.Act1_Blackout;
    public MissionStats Stats { get; private set; } = new();

    public HashSet<string> Flags { get; private set; } = new();
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Survivor;
    public StoryAct CurrentAct { get; set; } = StoryAct.None;

    public bool HasFlag(string flag) => Flags.Contains(flag);
    public event Action FlagsChanged;
    public void SetFlag(string flag)
    {
        if (Flags.Add(flag)) FlagsChanged?.Invoke();
    }
    public void RemoveFlag(string flag)
    {
        if (Flags.Remove(flag)) FlagsChanged?.Invoke();
    }
    public void RestoreFlags(IEnumerable<string> flags)
    {
        Flags = new HashSet<string>(flags);
    }

    private float _phaseTimer = 0.0f;
    private float _purgeCountdown = 480.0f; // 8 minutes countdown once purge initiates
    private bool _purgeTimerActive = false;

    private float _defenseTimer = 0.0f;
    private const float DefenseDuration = 30.0f;
    private bool _isDefending = false;

    private Zombie _bossInstance = null;
    private Player _player;
    private string _timingScenePath = string.Empty;

    public override void _EnterTree()
    {
        Instance = this;
        SaveSystem.EnsureInstance(this);
    }

    public override void _ExitTree()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public override void _Ready()
    {
        _timingScenePath = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
        Instance = this;

        if (SaveSystem.Instance?.ActiveSaveData != null)
        {
            RestoreFlags(SaveSystem.Instance.ActiveSaveData.Flags);
            RestoreMissionStats(SaveSystem.Instance.ActiveSaveData);
            Difficulty = SaveSystem.Instance.ActiveSaveData.Difficulty;
            if (SaveSystem.Instance.ActiveSaveData.CurrentAct != StoryAct.None)
            {
                CurrentAct = SaveSystem.Instance.ActiveSaveData.CurrentAct;
            }
        }

        BossStats ??= GD.Load<EnemyStats>("res://Resources/Enemies/Zombie_Boss.tres");
        ZombieScene ??= GD.Load<PackedScene>("res://Prefabs/Zombie.tscn");

        CallDeferred(nameof(InitPlayerAndStory));
    }

    private void InitPlayerAndStory()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (!IsCampaign) StartAct1();
    }

    public override void _Process(double delta)
    {
        if (IsCampaign && _player is { IsDead: true }) return;
        float dt = (float)delta;
        if (IsCampaign) CampaignTiming.Record(_timingScenePath,dt);
        Stats.PlaytimeSeconds += dt;
        _phaseTimer += dt;

        if (_purgeTimerActive && _purgeCountdown > 0.0f)
        {
            _purgeCountdown -= dt;
            EmitSignal(SignalName.PurgeTimerUpdated, Mathf.Max(0.0f, _purgeCountdown), true);

            if (_purgeCountdown <= 0.0f && CurrentPhase != StoryPhase.MissionCompleted)
            {
                // Chemical decontamination purge triggered
                _player?.TakeDamage(999.0f, Vector2.Zero);
            }
        }

        if (_isDefending)
        {
            _defenseTimer += dt;
            float progress = Mathf.Clamp(_defenseTimer / DefenseDuration, 0.0f, 1.0f);
            EmitSignal(SignalName.DefenseProgressUpdated, progress, true);

            if (_defenseTimer >= DefenseDuration)
            {
                OnDefenseCompleted();
            }
        }

        if (_bossInstance != null && IsInstanceValid(_bossInstance) && !_bossInstance.IsDead)
        {
            var hpComp = _bossInstance.HealthComponent;
            if (hpComp != null)
            {
                EmitSignal(SignalName.BossHealthUpdated, hpComp.CurrentHealth, hpComp.MaxHealth);
            }
        }
    }

    public void StartAct1()
    {
        CurrentPhase = StoryPhase.Act1_Blackout;
        EmitSignal(SignalName.PhaseChanged, (int)CurrentPhase);
        EmitSignal(SignalName.ObjectiveUpdated, "MISSION 1: RESTORE SECTOR POWER", "Grab the glowing Substation Keycard in front of you.");

        // Opening Comms
        TriggerRadioComms("CMDR. REYES (HQ)", "Cole! Sector-4 is in emergency blackout. Grab the glowing Substation Keycard on the floor in front of you, unlock the East blast door, and restart the generator!", 7.0f);
    }

    public void OnGeneratorActivated()
    {
        if (IsCampaign)
        {
            SetFlag(StoryFlags.FACILITY_POWER_RESTORED);
            EmitSignal(SignalName.FacilityPowerRestored);
            return;
        }
        if (CurrentPhase != StoryPhase.Act1_Blackout) return;

        CurrentPhase = StoryPhase.Act1_PowerRestored;
        EmitSignal(SignalName.PhaseChanged, (int)CurrentPhase);
        EmitSignal(SignalName.FacilityPowerRestored);

        TriggerRadioComms("CMDR. REYES (HQ)", "Auxiliary grid is online! Facility lighting restored. Warning: automated decontamination sequence initiated! You have limited time before purge!", 6.0f);

        _purgeTimerActive = true;
        _purgeCountdown = 540.0f; // 9 minutes remaining

        var timer = GetTree().CreateTimer(5.5f);
        timer.Timeout += StartAct2;
    }

    public void StartAct2()
    {
        CurrentPhase = StoryPhase.Act2_ContainmentBreach;
        EmitSignal(SignalName.PhaseChanged, (int)CurrentPhase);
        EmitSignal(SignalName.ObjectiveUpdated, "MISSION 2: SECURE BIO-TELEMETRY", "Enter Central Research Lab and download Dr. Vance's pathogen data.");

        TriggerRadioComms("CMDR. REYES (HQ)", "Central Wing blast doors are opening. Be alert: high-velocity bio-specimens detected moving through ventilation!", 5.5f);
    }

    public void OnTerminalAccessed()
    {
        if (_isDefending || CurrentPhase == StoryPhase.Act3_HelipadEvacuation || CurrentPhase == StoryPhase.MissionCompleted)
            return;

        CurrentPhase = StoryPhase.Act2_TerminalDefense;
        _isDefending = true;
        _defenseTimer = 0.0f;
        Stats.DocumentsFound++;

        EmitSignal(SignalName.PhaseChanged, (int)CurrentPhase);
        EmitSignal(SignalName.ObjectiveUpdated, "DEFEND TERMINAL UPLOAD", "Survive the breach while pathogen telemetry transfers (30s).");

        AudioManager.Instance?.PlayAlarm(true);
        TriggerRadioComms("CMDR. REYES (HQ)", "Data uplink established! Audio sensors picking up horde activity closing on your location! HOLD THE PERIMETER!", 6.0f);
    }

    private void OnDefenseCompleted()
    {
        _isDefending = false;
        EmitSignal(SignalName.DefenseProgressUpdated, 1.0f, false);
        AudioManager.Instance?.StopAlarm();

        CurrentPhase = StoryPhase.Act3_HelipadEvacuation;
        EmitSignal(SignalName.PhaseChanged, (int)CurrentPhase);
        EmitSignal(SignalName.ObjectiveUpdated, "MISSION 3: EVACUATE TO CARGO HELIPAD", "Proceed north to the Surface Cargo Lift and signal rescue transport.");

        TriggerRadioComms("CMDR. REYES (HQ)", "Telemetry secured! Evac transport 'Vulture-9' is inbound to the Cargo Helipad. Heavy blast gates unlocking now. MOVE!", 6.5f);
    }

    public void SpawnBossAtPosition(Vector2 bossPosition)
    {
        if (_bossInstance != null && IsInstanceValid(_bossInstance))
            return;

        if (ZombieScene == null || BossStats == null) return;

        CurrentPhase = StoryPhase.Act3_BossBattle;
        EmitSignal(SignalName.PhaseChanged, (int)CurrentPhase);
        EmitSignal(SignalName.ObjectiveUpdated, "ELIMINATE SPECIMEN ZERO", "Defeat the mutated Alpha Brute to secure the landing pad!");

        var boss = ZombieScene.Instantiate<Zombie>();
        boss.GlobalPosition = bossPosition;
        boss.Stats = BossStats;

        var parent = GetTree().CurrentScene;
        parent.AddChild(boss);
        _bossInstance = boss;

        AudioManager.Instance?.PlaySound(SoundType.BossRoar, bossPosition);
        AudioManager.Instance?.PlayAlarm(true);

        boss.Died += OnBossDied;
        EmitSignal(SignalName.BossSpawned, boss);

        TriggerRadioComms("PILOT (VULTURE-9)", "Holy hell! What is that thing on the pad?! We can't set down with that monster in the landing zone! Take it down, operative!", 6.0f);
    }

    private void OnBossDied()
    {
        AudioManager.Instance?.StopAlarm();
        EmitSignal(SignalName.BossHealthUpdated, 0.0f, BossStats.MaxHealth);
        EmitSignal(SignalName.ObjectiveUpdated, "BOARD EVACUATION TRANSPORT", "Step onto the landing pad beacon to board Vulture-9!");

        TriggerRadioComms("PILOT (VULTURE-9)", "Target neutralized! Bringing the bird in for touchdown! Ramps down, get aboard NOW!", 5.5f);
    }

    public void TriggerMissionCompleted()
    {
        if (CurrentPhase == StoryPhase.MissionCompleted) return;

        CurrentPhase = StoryPhase.MissionCompleted;
        _purgeTimerActive = false;
        EmitSignal(SignalName.PhaseChanged, (int)CurrentPhase);
        EmitSignal(SignalName.PurgeTimerUpdated, 0.0f, false);
        EmitSignal(SignalName.MissionCompleted);
    }

    public void TriggerRadioComms(string speaker, string message, float duration = 5.0f)
    {
        AudioManager.Instance?.PlaySound(SoundType.RadioChime);
        EmitSignal(SignalName.RadioTransmissionTriggered, speaker, ControlBindings.Format(message), duration);
    }

    public void RecordShotFired() => Stats.ShotsFired++;
    public void RecordShotHit() => Stats.ShotsHit++;
    public void RecordKill() => Stats.TotalKills++;

    public void RestoreMissionStats(GameSaveData data)
    {
        Stats = new MissionStats
        {
            PlaytimeSeconds = data.PlaytimeSeconds, TotalKills = data.TotalKills,
            ShotsFired = data.ShotsFired, ShotsHit = data.ShotsHit, DocumentsFound = data.DocumentsFound
        };
    }
}
