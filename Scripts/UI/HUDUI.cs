using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class HUDUI : Control
{
    [ExportGroup("Low Health Vignette")]
    [Export] public TextureRect BloodVignette { get; set; }

    [ExportGroup("Standard HUD")]
    [Export] public ProgressBar HealthBar { get; set; }
    [Export] public Label HealthLabel { get; set; }
    [Export] public ProgressBar StaminaBar { get; set; }
    [Export] public Label StaminaLabel { get; set; }
    [Export] public Label AmmoLabel { get; set; }
    [Export] public Label WeaponLabel { get; set; }
    [Export] public Label KillLabel { get; set; }
    [Export] public Control GameOverPanel { get; set; }

    [ExportGroup("Story & Objectives")]
    [Export] public Label ObjectiveTitleLabel { get; set; }
    [Export] public Label ObjectiveSubtaskLabel { get; set; }
    [Export] public Label PurgeTimerLabel { get; set; }
    [Export] public ProgressBar DefenseProgressBar { get; set; }
    [Export] public Label DefenseProgressLabel { get; set; }

    [ExportGroup("Radio Comms")]
    [Export] public Control RadioCommsPanel { get; set; }
    [Export] public Label RadioSpeakerLabel { get; set; }
    [Export] public Label RadioMessageLabel { get; set; }

    [ExportGroup("Interaction Prompt")]
    [Export] public Label InteractionPromptLabel { get; set; }

    [ExportGroup("Boss Bar")]
    [Export] public Control BossBarContainer { get; set; }
    [Export] public ProgressBar BossProgressBar { get; set; }
    [Export] public Label BossTitleLabel { get; set; }

    [ExportGroup("Victory Screen")]
    [Export] public Control VictoryPanel { get; set; }
    [Export] public Label VictoryRankLabel { get; set; }
    [Export] public Label VictoryStatsLabel { get; set; }

    private Player _player;
    private WeaponData _currentWeapon;
    private int _kills = 0;

    private float _healthRatio = 1.0f;
    private float _currentVignetteAlpha = 0.0f;
    private float _heartbeatPhase = 0.0f;

    private float _radioTimer = 0.0f;
    private string _fullRadioMessage = string.Empty;
    private float _typewriterIndex = 0.0f;

    public override void _Ready()
    {
        BindNodes();

        if (GameOverPanel != null) GameOverPanel.Visible = false;
        if (RadioCommsPanel != null) RadioCommsPanel.Visible = false;
        if (InteractionPromptLabel != null) InteractionPromptLabel.Visible = false;
        if (DefenseProgressBar != null) DefenseProgressBar.Visible = false;
        if (BossBarContainer != null) BossBarContainer.Visible = false;
        if (VictoryPanel != null) VictoryPanel.Visible = false;
        if (PurgeTimerLabel != null) PurgeTimerLabel.Visible = false;

        BindPlayer();
        BindStoryDirector();

        Zombie.OnAnyZombieKilled += OnZombieKilled;
    }

    private void BindNodes()
    {
        BloodVignette ??= GetNodeOrNull<TextureRect>("%BloodVignette");
        if (BloodVignette != null)
        {
            BloodVignette.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
            BloodVignette.Visible = false;
        }

        HealthBar ??= GetNodeOrNull<ProgressBar>("%HealthBar");
        HealthLabel ??= GetNodeOrNull<Label>("%HealthLabel");
        StaminaBar ??= GetNodeOrNull<ProgressBar>("%StaminaBar");
        StaminaLabel ??= GetNodeOrNull<Label>("%StaminaLabel");
        AmmoLabel ??= GetNodeOrNull<Label>("%AmmoLabel");
        WeaponLabel ??= GetNodeOrNull<Label>("%WeaponLabel");
        KillLabel ??= GetNodeOrNull<Label>("%KillLabel");
        GameOverPanel ??= GetNodeOrNull<Control>("%GameOverPanel");

        ObjectiveTitleLabel ??= GetNodeOrNull<Label>("%ObjectiveTitleLabel");
        ObjectiveSubtaskLabel ??= GetNodeOrNull<Label>("%ObjectiveSubtaskLabel");
        PurgeTimerLabel ??= GetNodeOrNull<Label>("%PurgeTimerLabel");
        DefenseProgressBar ??= GetNodeOrNull<ProgressBar>("%DefenseProgressBar");
        DefenseProgressLabel ??= GetNodeOrNull<Label>("%DefenseProgressLabel");

        RadioCommsPanel ??= GetNodeOrNull<Control>("%RadioCommsPanel");
        RadioSpeakerLabel ??= GetNodeOrNull<Label>("%RadioSpeakerLabel");
        RadioMessageLabel ??= GetNodeOrNull<Label>("%RadioMessageLabel");

        InteractionPromptLabel ??= GetNodeOrNull<Label>("%InteractionPromptLabel");

        BossBarContainer ??= GetNodeOrNull<Control>("%BossBarContainer");
        BossProgressBar ??= GetNodeOrNull<ProgressBar>("%BossProgressBar");
        BossTitleLabel ??= GetNodeOrNull<Label>("%BossTitleLabel");

        VictoryPanel ??= GetNodeOrNull<Control>("%VictoryPanel");
        VictoryRankLabel ??= GetNodeOrNull<Label>("%VictoryRankLabel");
        VictoryStatsLabel ??= GetNodeOrNull<Label>("%VictoryStatsLabel");
    }

    private void BindStoryDirector()
    {
        if (StoryDirector.Instance == null) return;

        StoryDirector.Instance.ObjectiveUpdated += OnObjectiveUpdated;
        StoryDirector.Instance.RadioTransmissionTriggered += OnRadioTransmission;
        StoryDirector.Instance.PurgeTimerUpdated += OnPurgeTimerUpdated;
        StoryDirector.Instance.DefenseProgressUpdated += OnDefenseProgressUpdated;
        StoryDirector.Instance.BossSpawned += OnBossSpawned;
        StoryDirector.Instance.BossHealthUpdated += OnBossHealthUpdated;
        StoryDirector.Instance.MissionCompleted += OnMissionCompleted;
    }

    public override void _ExitTree()
    {
        Zombie.OnAnyZombieKilled -= OnZombieKilled;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        UpdateBloodVignette(dt);
        UpdateCloseCombatPrompt();

        if (IsInstanceValid(_player) && !_player.IsDead)
        {
            if (WeaponLabel != null && _currentWeapon != null)
            {
                string aimIndicator = _player.IsAiming ? " [AIMING]" : "";
                WeaponLabel.Text = $"{_currentWeapon.WeaponName.ToUpper()}{aimIndicator} (Q/1-3)";
                WeaponLabel.Modulate = _player.IsAiming ? new Color(1.0f, 0.4f, 0.4f, 1.0f) : new Color(0.85f, 0.9f, 1.0f, 0.9f);
            }
        }

        // Radio typewriter effect
        if (_radioTimer > 0.0f)
        {
            _radioTimer -= dt;
            _typewriterIndex += dt * 42.0f;
            int len = Mathf.Min((int)_typewriterIndex, _fullRadioMessage.Length);
            if (RadioMessageLabel != null)
            {
                RadioMessageLabel.Text = _fullRadioMessage[..len];
            }

            if (_radioTimer <= 0.0f && RadioCommsPanel != null)
            {
                var tween = CreateTween();
                tween.TweenProperty(RadioCommsPanel, "modulate:a", 0.0f, 0.4);
                tween.TweenCallback(Callable.From(() => RadioCommsPanel.Visible = false));
            }
        }
    }

    private void BindPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_player == null) return;

        if (_player.HealthComponent != null)
        {
            _player.HealthComponent.HealthChanged += OnHealthChanged;
            OnHealthChanged(_player.HealthComponent.CurrentHealth, _player.HealthComponent.MaxHealth);
        }

        if (_player.StaminaComponent != null)
        {
            _player.StaminaComponent.StaminaChanged += OnStaminaChanged;
            OnStaminaChanged(_player.StaminaComponent.CurrentStamina, _player.StaminaComponent.MaxStamina);
        }

        if (_player.InventoryComponent != null)
        {
            _player.InventoryComponent.WeaponChanged += OnWeaponChanged;
            _player.InventoryComponent.AmmoChanged += OnAmmoChanged;

            if (_player.InventoryComponent.CurrentWeapon != null)
            {
                OnWeaponChanged(_player.InventoryComponent.CurrentWeapon);
            }
            OnAmmoChanged(_player.InventoryComponent.CurrentAmmo, _player.InventoryComponent.CurrentReserve, _player.InventoryComponent.IsReloading);
        }

        if (_player.InteractionComponent != null)
        {
            _player.InteractionComponent.FocusedInteractableChanged += OnFocusedInteractableChanged;
        }

        _player.PlayerDied += OnPlayerDied;
    }

    private bool _hasInteractablePrompt = false;

    private void OnFocusedInteractableChanged(string promptText, bool canInteract)
    {
        if (InteractionPromptLabel == null) return;

        if (string.IsNullOrEmpty(promptText))
        {
            _hasInteractablePrompt = false;
            InteractionPromptLabel.Visible = false;
        }
        else
        {
            _hasInteractablePrompt = true;
            InteractionPromptLabel.Visible = true;
            InteractionPromptLabel.Text = $"[E] {promptText.ToUpper()}";
            InteractionPromptLabel.Modulate = canInteract ? new Color(0.2f, 1.0f, 0.5f, 1.0f) : new Color(1.0f, 0.4f, 0.3f, 1.0f);
        }
    }

    private void UpdateCloseCombatPrompt()
    {
        if (_hasInteractablePrompt || InteractionPromptLabel == null) return;
        if (!IsInstanceValid(_player) || _player.IsDead) return;

        var zombies = GetTree()?.GetNodesInGroup("zombies");
        if (zombies == null) return;

        Vector2 playerPos = _player.GlobalPosition;
        bool hasCloseZombie = false;

        foreach (var node in zombies)
        {
            if (node is Zombie z && IsInstanceValid(z) && !z.IsDead)
            {
                if (z.GlobalPosition.DistanceTo(playerPos) <= 95.0f)
                {
                    hasCloseZombie = true;
                    break;
                }
            }
        }

        if (hasCloseZombie)
        {
            InteractionPromptLabel.Visible = true;
            InteractionPromptLabel.Text = "[V] QUICK KNIFE";
            InteractionPromptLabel.Modulate = new Color(1.0f, 0.85f, 0.3f, 0.95f);
        }
        else if (!_hasInteractablePrompt)
        {
            InteractionPromptLabel.Visible = false;
        }
    }

    private void OnObjectiveUpdated(string title, string subtask)
    {
        if (ObjectiveTitleLabel != null) ObjectiveTitleLabel.Text = title;
        if (ObjectiveSubtaskLabel != null) ObjectiveSubtaskLabel.Text = subtask;

        if (ObjectiveTitleLabel != null)
        {
            var tween = CreateTween();
            ObjectiveTitleLabel.Modulate = new Color(1.5f, 1.5f, 0.5f, 1.0f);
            tween.TweenProperty(ObjectiveTitleLabel, "modulate", Colors.White, 0.4);
        }
    }

    private void OnRadioTransmission(string speaker, string message, float duration)
    {
        if (RadioCommsPanel == null) return;

        RadioCommsPanel.Visible = true;
        RadioCommsPanel.Modulate = Colors.White;
        if (RadioSpeakerLabel != null) RadioSpeakerLabel.Text = speaker;
        _fullRadioMessage = message;
        _typewriterIndex = 0.0f;
        _radioTimer = duration;
    }

    private void OnPurgeTimerUpdated(float remainingSeconds, bool isVisible)
    {
        if (PurgeTimerLabel == null) return;

        PurgeTimerLabel.Visible = isVisible;
        if (isVisible)
        {
            int mins = (int)(remainingSeconds / 60);
            int secs = (int)(remainingSeconds % 60);
            PurgeTimerLabel.Text = $"PURGE IMMINENT: {mins:D2}:{secs:D2}";
            PurgeTimerLabel.Modulate = remainingSeconds < 120 ? new Color(1f, 0.2f, 0.2f, 1f) : new Color(1f, 0.8f, 0.2f, 1f);
        }
    }

    private void OnDefenseProgressUpdated(float progress, bool isVisible)
    {
        if (DefenseProgressBar != null)
        {
            DefenseProgressBar.Visible = isVisible;
            DefenseProgressBar.Value = progress * 100f;
        }
        if (DefenseProgressLabel != null)
        {
            DefenseProgressLabel.Visible = isVisible;
            DefenseProgressLabel.Text = $"DATA UPLOAD: {(int)(progress * 100f)}%";
        }
    }

    private void OnBossSpawned(Zombie boss)
    {
        if (BossBarContainer != null)
        {
            BossBarContainer.Visible = true;
            if (BossTitleLabel != null) BossTitleLabel.Text = "SPECIMEN ZERO - CONTAINMENT ALPHA";
            if (BossProgressBar != null)
            {
                BossProgressBar.MaxValue = boss.Stats?.MaxHealth ?? 420f;
                BossProgressBar.Value = BossProgressBar.MaxValue;
            }
        }
    }

    private void OnBossHealthUpdated(float current, float max)
    {
        if (BossBarContainer != null)
        {
            BossBarContainer.Visible = current > 0;
            if (BossProgressBar != null)
            {
                BossProgressBar.MaxValue = max;
                BossProgressBar.Value = current;
            }
        }
    }

    private void OnMissionCompleted()
    {
        if (VictoryPanel != null)
        {
            VictoryPanel.Visible = true;
            var stats = StoryDirector.Instance?.Stats ?? new MissionStats();

            if (VictoryRankLabel != null)
            {
                VictoryRankLabel.Text = $"SURVIVOR GRADE: {stats.CalculateRank()}";
            }

            if (VictoryStatsLabel != null)
            {
                int mins = (int)(stats.PlaytimeSeconds / 60);
                int secs = (int)(stats.PlaytimeSeconds % 60);
                VictoryStatsLabel.Text = $"ESCAPE TIME: {mins:D2}:{secs:D2}\n" +
                                        $"HOSTILES PURGED: {stats.TotalKills}\n" +
                                        $"ACCURACY: {stats.AccuracyPercentage:F1}%\n" +
                                        $"DOCUMENTS RECOVERED: {stats.DocumentsFound} / 1\n\n" +
                                        $"STATUS: EXTRACTION SUCCESSFUL\n" +
                                        $"PRESS 'R' TO REPLAY MISSION";
            }
        }
    }

    private void OnWeaponChanged(WeaponData newWeapon)
    {
        _currentWeapon = newWeapon;
        if (WeaponLabel != null && newWeapon != null)
        {
            WeaponLabel.Text = $"{newWeapon.WeaponName.ToUpper()} (Q/1-3)";
        }
    }

    public void OnHealthChanged(float current, float maximum)
    {
        if (maximum > 0.0f)
        {
            _healthRatio = Mathf.Clamp(current / maximum, 0.0f, 1.0f);
        }

        if (HealthBar != null)
        {
            HealthBar.MaxValue = maximum;
            HealthBar.Value = current;
        }
        if (HealthLabel != null)
        {
            HealthLabel.Text = $"{(int)current} / {(int)maximum} HP";
        }
    }

    public void OnStaminaChanged(float current, float maximum)
    {
        if (StaminaBar != null)
        {
            StaminaBar.MaxValue = maximum;
            StaminaBar.Value = current;
        }
        if (StaminaLabel != null)
        {
            StaminaLabel.Text = $"{(int)current} / {(int)maximum} SP";
        }
    }

    public void OnAmmoChanged(int current, int reserve, bool isReloading)
    {
        if (AmmoLabel == null) return;

        if (isReloading)
        {
            AmmoLabel.Text = "RELOADING...";
            AmmoLabel.Modulate = new Color(1.0f, 0.4f, 0.4f, 1.0f);
        }
        else
        {
            AmmoLabel.Text = $"AMMO: {current} / {reserve}";
            AmmoLabel.Modulate = current <= 2 ? new Color(1.0f, 0.4f, 0.3f, 1.0f) : Colors.White;
        }
    }

    public void SetKillCount(int count)
    {
        if (KillLabel != null)
        {
            KillLabel.Text = $"KILLS: {count}";
        }
    }

    private void OnZombieKilled()
    {
        _kills++;
        SetKillCount(_kills);
    }

    private void OnPlayerDied()
    {
        if (GameOverPanel != null)
        {
            GameOverPanel.Visible = true;
        }
    }

    private void UpdateBloodVignette(float dt)
    {
        if (BloodVignette == null) return;

        if (IsInstanceValid(_player) && _player.IsDead)
        {
            BloodVignette.Visible = true;
            _currentVignetteAlpha = Mathf.MoveToward(_currentVignetteAlpha, 0.95f, dt * 2.0f);
            BloodVignette.Modulate = new Color(0.9f, 0.1f, 0.1f, _currentVignetteAlpha);
            return;
        }

        if (_healthRatio > 0.40f)
        {
            if (_currentVignetteAlpha > 0.001f)
            {
                _currentVignetteAlpha = Mathf.MoveToward(_currentVignetteAlpha, 0.0f, dt * 2.5f);
                BloodVignette.Modulate = new Color(1.0f, 1.0f, 1.0f, _currentVignetteAlpha);
                BloodVignette.Visible = true;
            }
            else
            {
                _currentVignetteAlpha = 0.0f;
                BloodVignette.Visible = false;
                _heartbeatPhase = 0.0f;
            }
            return;
        }

        // Low health active
        BloodVignette.Visible = true;

        if (_healthRatio > 0.20f)
        {
            // Warning stage: 20% < HP <= 40% (Resident Evil Caution)
            float severity = (0.40f - _healthRatio) / 0.20f;
            _heartbeatPhase += dt * 3.5f;

            float pulse = Mathf.Sin(_heartbeatPhase) * 0.08f;
            float targetAlpha = Mathf.Clamp(0.25f + (severity * 0.25f) + pulse, 0.15f, 0.65f);

            _currentVignetteAlpha = Mathf.MoveToward(_currentVignetteAlpha, targetAlpha, dt * 2.5f);
            BloodVignette.Modulate = new Color(1.0f, 0.92f, 0.92f, _currentVignetteAlpha);
        }
        else
        {
            // Critical stage: HP <= 20% (Resident Evil Danger rhythmic pulse)
            _heartbeatPhase += dt * 7.5f;
            float pulse = Mathf.Pow(Mathf.Max(0.0f, Mathf.Sin(_heartbeatPhase)), 2.2f);

            float targetAlpha = 0.48f + (pulse * 0.40f);
            float redTint = 1.0f - (pulse * 0.12f);

            _currentVignetteAlpha = Mathf.MoveToward(_currentVignetteAlpha, targetAlpha, dt * 5.0f);
            BloodVignette.Modulate = new Color(1.0f, redTint, redTint, _currentVignetteAlpha);
        }
    }
}
