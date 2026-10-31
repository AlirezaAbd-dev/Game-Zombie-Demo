using System;
using System.Collections.Generic;
using GameZombieDemo;
using Godot;

public partial class World : Node2D
{
    public enum WaveState
    {
        Active,
        DraftIntermission,
        GameOver
    }

    [Export] public float SpawnInterval { get; set; } = 1.4f;
    [Export] public int MaxZombiesConcurrent { get; set; } = 28;

    public int CurrentWave { get; private set; } = 1;
    public int KillCount { get; private set; } = 0;

    private WaveState _currentState = WaveState.Active;
    private int _waveBudget = 0;
    private float _spawnTimer = 0.0f;
    private readonly List<UpgradeCard> _currentDraftCards = new();

    private PackedScene _zombieScene = null!;
    private Player? _player;
    private ShaderMaterial? _vignetteMaterial;

    // Existing HUD nodes
    private ProgressBar? _healthBar;
    private Label? _healthLabel;
    private ProgressBar? _staminaBar;
    private Label? _staminaLabel;
    private Label? _ammoLabel;
    private Label? _killLabel;
    private Control? _gameOverPanel;

    // New procedural HUD nodes
    private Label? _waveLabel;
    private Label? _weaponLabel;
    private Label? _grenadeLabel;
    private Control? _activeReloadControl;
    private ColorRect? _activeReloadNeedle;
    private Control? _upgradeModal;
    private HBoxContainer? _upgradeCardsContainer;

    public override void _Ready()
    {
        // 1. Singletons
        if (AudioManager.Instance == null)
        {
            var audioMgr = new AudioManager { Name = "AudioManager" };
            AddChild(audioMgr);
        }
        if (HitStopManager.Instance == null)
        {
            var hitStop = new HitStopManager { Name = "HitStopManager" };
            AddChild(hitStop);
        }
        if (DecalManager.Instance == null)
        {
            var decalMgr = new DecalManager { Name = "DecalManager" };
            AddChild(decalMgr);
        }

        _zombieScene = GD.Load<PackedScene>("res://scenes/zombie.tscn");

        _player = GetNodeOrNull<Player>("Player");
        _healthBar = GetNodeOrNull<ProgressBar>("UIOverlay/HUD/HealthBar");
        _healthLabel = GetNodeOrNull<Label>("UIOverlay/HUD/HealthBar/HealthLabel");
        _staminaBar = GetNodeOrNull<ProgressBar>("UIOverlay/HUD/StaminaBar");
        _staminaLabel = GetNodeOrNull<Label>("UIOverlay/HUD/StaminaBar/StaminaLabel");
        _ammoLabel = GetNodeOrNull<Label>("UIOverlay/HUD/AmmoLabel");
        _killLabel = GetNodeOrNull<Label>("UIOverlay/HUD/KillLabel");
        _gameOverPanel = GetNodeOrNull<Control>("UIOverlay/GameOverPanel");

        var vignette = GetNodeOrNull<ColorRect>("UIOverlay/Vignette");
        if (vignette?.Material is ShaderMaterial sm)
        {
            _vignetteMaterial = sm;
        }

        SetupProceduralHud();
        SetupUpgradeModal();

        if (_player != null)
        {
            _player.HealthChanged += OnPlayerHealthChanged;
            _player.StaminaChanged += OnPlayerStaminaChanged;
            _player.AmmoChanged += OnPlayerAmmoChanged;
            _player.WeaponHudUpdated += OnPlayerWeaponHudUpdated;
            _player.PlayerDied += OnPlayerDied;

            OnPlayerHealthChanged(_player.Health, _player.MaxHealth);
            OnPlayerStaminaChanged(_player.CurrentStamina, _player.MaxStamina);
        }

        if (_gameOverPanel != null)
        {
            _gameOverPanel.Visible = false;
        }

        StartWave(1);
    }

    private void SetupProceduralHud()
    {
        var hud = GetNodeOrNull<Control>("UIOverlay/HUD");
        if (hud == null) return;

        // Wave Label (Top Center)
        _waveLabel = new Label
        {
            Text = "WAVE 1",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _waveLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _waveLabel.OffsetTop = 26f;
        _waveLabel.OffsetLeft = -200f;
        _waveLabel.OffsetRight = 200f;
        _waveLabel.OffsetBottom = 54f;

        var waveSettings = new LabelSettings
        {
            FontSize = 20,
            FontColor = new Color(0.95f, 0.95f, 1.0f),
            OutlineSize = 3,
            OutlineColor = new Color(0, 0, 0, 0.9f)
        };
        _waveLabel.LabelSettings = waveSettings;
        hud.AddChild(_waveLabel);

        // Weapon Label (Top Right above Ammo)
        _weaponLabel = new Label
        {
            Text = "WEAPON: PISTOL [1]",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _weaponLabel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _weaponLabel.OffsetLeft = -280f;
        _weaponLabel.OffsetRight = -30f;
        _weaponLabel.OffsetTop = 80f;
        _weaponLabel.OffsetBottom = 104f;

        var weaponSettings = new LabelSettings
        {
            FontSize = 14,
            FontColor = new Color(0.75f, 0.85f, 1.0f),
            OutlineSize = 2,
            OutlineColor = new Color(0, 0, 0, 0.85f)
        };
        _weaponLabel.LabelSettings = weaponSettings;
        hud.AddChild(_weaponLabel);

        // Grenades Label
        _grenadeLabel = new Label
        {
            Text = "GRENADES (G): 2",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _grenadeLabel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _grenadeLabel.OffsetLeft = -280f;
        _grenadeLabel.OffsetRight = -30f;
        _grenadeLabel.OffsetTop = 105f;
        _grenadeLabel.OffsetBottom = 129f;

        var grenadeSettings = new LabelSettings
        {
            FontSize = 13,
            FontColor = new Color(0.5f, 0.9f, 0.5f),
            OutlineSize = 2,
            OutlineColor = new Color(0, 0, 0, 0.85f)
        };
        _grenadeLabel.LabelSettings = grenadeSettings;
        hud.AddChild(_grenadeLabel);

        // Active Reload Bar (Center bottom of screen)
        _activeReloadControl = new Control
        {
            Visible = false,
            CustomMinimumSize = new Vector2(200, 20)
        };
        _activeReloadControl.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _activeReloadControl.OffsetTop = -130f;
        _activeReloadControl.OffsetBottom = -110f;
        _activeReloadControl.OffsetLeft = -100f;
        _activeReloadControl.OffsetRight = 100f;

        // Background
        var bg = new ColorRect
        {
            Color = new Color(0.08f, 0.1f, 0.14f, 0.85f),
            Size = new Vector2(200, 20)
        };
        _activeReloadControl.AddChild(bg);

        // Sweet spot bracket [45% to 65%]
        var sweetSpot = new ColorRect
        {
            Position = new Vector2(90, 0),
            Size = new Vector2(40, 20),
            Color = new Color(0.2f, 0.95f, 0.35f, 0.65f)
        };
        _activeReloadControl.AddChild(sweetSpot);

        // Needle
        _activeReloadNeedle = new ColorRect
        {
            Position = new Vector2(0, -2),
            Size = new Vector2(4, 24),
            Color = Colors.White
        };
        _activeReloadControl.AddChild(_activeReloadNeedle);

        // Text label
        var reloadPrompt = new Label
        {
            Text = "PRESS [R] IN SWEET SPOT",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Size = new Vector2(200, 20)
        };
        reloadPrompt.LabelSettings = new LabelSettings
        {
            FontSize = 10,
            OutlineSize = 2,
            OutlineColor = Colors.Black
        };
        _activeReloadControl.AddChild(reloadPrompt);

        hud.AddChild(_activeReloadControl);
    }

    private void SetupUpgradeModal()
    {
        var overlay = GetNodeOrNull<CanvasLayer>("UIOverlay");
        if (overlay == null) return;

        _upgradeModal = new Control
        {
            Visible = false,
            ProcessMode = ProcessModeEnum.Always
        };
        _upgradeModal.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // Background dim
        var dim = new ColorRect
        {
            Color = new Color(0.02f, 0.03f, 0.06f, 0.88f)
        };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _upgradeModal.AddChild(dim);

        // Container
        var vBox = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        vBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vBox.AddThemeConstantOverride("separation", 24);
        _upgradeModal.AddChild(vBox);

        var title = new Label
        {
            Text = "TACTICAL REINFORCEMENTS AVAILABLE",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.LabelSettings = new LabelSettings
        {
            FontSize = 26,
            FontColor = new Color(1.0f, 0.85f, 0.3f),
            OutlineSize = 4,
            OutlineColor = Colors.Black
        };
        vBox.AddChild(title);

        var subtitle = new Label
        {
            Text = "Select 1 upgrade to continue (Click or Press [1], [2], or [3])",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.LabelSettings = new LabelSettings
        {
            FontSize = 14,
            FontColor = new Color(0.8f, 0.85f, 0.9f),
            OutlineSize = 2,
            OutlineColor = Colors.Black
        };
        vBox.AddChild(subtitle);

        _upgradeCardsContainer = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _upgradeCardsContainer.AddThemeConstantOverride("separation", 30);
        vBox.AddChild(_upgradeCardsContainer);

        overlay.AddChild(_upgradeModal);
    }

    public void StartWave(int wave)
    {
        CurrentWave = wave;
        _waveBudget = 12 + (CurrentWave * 7) + (int)Mathf.Pow(CurrentWave, 1.45f);
        _currentState = WaveState.Active;
        _spawnTimer = 0.0f;

        // Grant 1 grenade per wave
        if (_player != null)
        {
            _player.Grenades = Math.Min(_player.MaxGrenades, _player.Grenades + 1);
        }

        // Spawn 3 explosive barrels around arena
        SpawnWaveBarrels(3);

        // Boss wave on wave % 5
        if (CurrentWave % 5 == 0)
        {
            SpawnBoss();
        }

        UpdateWaveHud();
    }

    private void SpawnWaveBarrels(int count)
    {
        Vector2 arenaCenter = new(600, 350);
        for (int i = 0; i < count; i++)
        {
            float ang = (float)GD.RandRange(0.0, Mathf.Tau);
            float dist = (float)GD.RandRange(250.0, 650.0);
            Vector2 pos = arenaCenter + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;

            var barrel = new ExplosiveBarrel
            {
                GlobalPosition = pos
            };
            AddChild(barrel);
        }
    }

    private void SpawnBoss()
    {
        if (_player == null) return;
        float ang = (float)GD.RandRange(0.0, Mathf.Tau);
        float dist = 850.0f;
        Vector2 spawnPos = _player.GlobalPosition + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;

        var boss = _zombieScene.Instantiate<Zombie>();
        boss.GlobalPosition = spawnPos;
        AddChild(boss);
        boss.ConfigureArchetype(Zombie.ZombieType.JuggernautBoss, CurrentWave);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Chromatic aberration linked to Player camera trauma
        if (_vignetteMaterial != null && _player != null)
        {
            float trauma = _player.CameraTrauma;
            _vignetteMaterial.SetShaderParameter("aberration_strength", trauma * 0.025f);
        }

        if (_currentState != WaveState.Active)
            return;

        if (!IsInstanceValid(_player) || _player.IsDead)
            return;

        _spawnTimer += dt;
        if (_spawnTimer >= SpawnInterval)
        {
            _spawnTimer = 0.0f;
            if (_waveBudget > 0)
            {
                SpawnZombieFromBudget();
            }
        }

        // Check wave completion
        int livingZombies = GetTree().GetNodesInGroup("zombies").Count;
        UpdateWaveHud();

        if (_waveBudget <= 0 && livingZombies == 0)
        {
            EnterDraftIntermission();
        }
    }

    private void SpawnZombieFromBudget()
    {
        int currentCount = GetTree().GetNodesInGroup("zombies").Count;
        if (currentCount >= MaxZombiesConcurrent)
            return;

        // Candidate archetypes based on wave
        var candidates = new List<(Zombie.ZombieType Type, int Cost)>();
        candidates.Add((Zombie.ZombieType.Walker, 2));

        if (CurrentWave >= 2)
            candidates.Add((Zombie.ZombieType.Runner, 3));
        if (CurrentWave >= 3)
            candidates.Add((Zombie.ZombieType.Spitter, 4));
        if (CurrentWave >= 4)
            candidates.Add((Zombie.ZombieType.Boomer, 5));

        // Filter by budget
        var affordable = candidates.FindAll(c => c.Cost <= _waveBudget);
        if (affordable.Count == 0)
        {
            _waveBudget = 0;
            return;
        }

        var picked = affordable[(int)GD.RandRange(0, affordable.Count)];
        _waveBudget -= picked.Cost;

        if (_player == null || !IsInstanceValid(_player)) return;

        float angle = (float)GD.RandRange(0.0, Mathf.Tau);
        float distance = (float)GD.RandRange(750.0, 950.0);
        Vector2 spawnPos = _player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

        var zombie = _zombieScene.Instantiate<Zombie>();
        zombie.GlobalPosition = spawnPos;
        AddChild(zombie);
        zombie.ConfigureArchetype(picked.Type, CurrentWave);
    }

    private void EnterDraftIntermission()
    {
        _currentState = WaveState.DraftIntermission;
        GetTree().Paused = true;

        _currentDraftCards.Clear();
        _currentDraftCards.AddRange(UpgradeCatalog.GetRandomPicks(3));

        PopulateUpgradeModal();
        if (_upgradeModal != null)
        {
            _upgradeModal.Visible = true;
        }

        AudioManager.Instance?.PlayGlobal("SfxPickup", 0.95f);
    }

    private void PopulateUpgradeModal()
    {
        if (_upgradeCardsContainer == null) return;

        foreach (var child in _upgradeCardsContainer.GetChildren())
        {
            child.QueueFree();
        }

        for (int i = 0; i < _currentDraftCards.Count; i++)
        {
            int index = i;
            var card = _currentDraftCards[i];

            var panel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(240, 280)
            };

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.1f, 0.14f, 0.2f, 0.95f),
                BorderWidthBottom = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                BorderColor = new Color(0.35f, 0.5f, 0.7f),
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8
            };
            panel.AddThemeStyleboxOverride("panel", style);

            var vBox = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            vBox.AddThemeConstantOverride("separation", 14);
            panel.AddChild(vBox);

            var numLabel = new Label
            {
                Text = $"[ {index + 1} ]",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            numLabel.LabelSettings = new LabelSettings
            {
                FontSize = 20,
                FontColor = new Color(1.0f, 0.8f, 0.2f),
                OutlineSize = 2,
                OutlineColor = Colors.Black
            };
            vBox.AddChild(numLabel);

            var titleLabel = new Label
            {
                Text = card.Title,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            titleLabel.LabelSettings = new LabelSettings
            {
                FontSize = 16,
                FontColor = Colors.White,
                OutlineSize = 2,
                OutlineColor = Colors.Black
            };
            vBox.AddChild(titleLabel);

            var descLabel = new Label
            {
                Text = card.Description,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            descLabel.LabelSettings = new LabelSettings
            {
                FontSize = 13,
                FontColor = new Color(0.75f, 0.85f, 0.95f),
                OutlineSize = 2,
                OutlineColor = Colors.Black
            };
            vBox.AddChild(descLabel);

            var selectBtn = new Button
            {
                Text = "SELECT"
            };
            selectBtn.Pressed += () => SelectUpgrade(index);
            vBox.AddChild(selectBtn);

            _upgradeCardsContainer.AddChild(panel);
        }
    }

    private void SelectUpgrade(int index)
    {
        if (index < 0 || index >= _currentDraftCards.Count) return;

        var card = _currentDraftCards[index];
        if (_player != null && IsInstanceValid(_player))
        {
            card.Apply(_player);
        }

        if (_upgradeModal != null)
        {
            _upgradeModal.Visible = false;
        }

        GetTree().Paused = false;
        AudioManager.Instance?.PlayGlobal("SfxPickup", 1.25f);

        StartWave(CurrentWave + 1);
    }

    private void UpdateWaveHud()
    {
        if (_waveLabel != null)
        {
            int remainingLiving = GetTree().GetNodesInGroup("zombies").Count;
            _waveLabel.Text = $"WAVE {CurrentWave} | THREATS: {remainingLiving + (_waveBudget > 0 ? _waveBudget / 2 : 0)}";
        }
    }

    public void OnZombieKilled()
    {
        KillCount++;
        UpdateKillCounter();
    }

    private void UpdateKillCounter()
    {
        if (_killLabel != null)
        {
            _killLabel.Text = $"KILLS: {KillCount}";
        }
    }

    private void OnPlayerHealthChanged(float current, float maximum)
    {
        if (_healthBar != null)
        {
            _healthBar.MaxValue = maximum;
            _healthBar.Value = current;
        }
        if (_healthLabel != null)
        {
            _healthLabel.Text = $"{(int)current} / {(int)maximum} HP";
        }
    }

    private void OnPlayerStaminaChanged(float current, float maximum)
    {
        if (_staminaBar != null)
        {
            _staminaBar.MaxValue = maximum;
            _staminaBar.Value = current;
        }
        if (_staminaLabel != null)
        {
            _staminaLabel.Text = $"{(int)current} / {(int)maximum} SP";
        }
    }

    private void OnPlayerAmmoChanged(int current, int reserve, bool isReloading)
    {
        if (_ammoLabel == null) return;

        if (isReloading)
        {
            _ammoLabel.Text = "RELOADING...";
            _ammoLabel.Modulate = new Color(1.0f, 0.4f, 0.4f, 1.0f);
        }
        else
        {
            _ammoLabel.Text = $"AMMO: {current} / {reserve}";
            _ammoLabel.Modulate = current <= 3 ? new Color(1.0f, 0.6f, 0.2f, 1.0f) : Colors.White;
        }
    }

    private void OnPlayerWeaponHudUpdated(string weaponName, int mag, int reserve, bool infinite, bool isReloading, float reloadProgress, bool isEmpowered, int grenades)
    {
        if (_weaponLabel != null)
        {
            string empTag = isEmpowered ? " [EMPOWERED]" : "";
            _weaponLabel.Text = $"WEAPON: {weaponName.ToUpper()}{empTag}";
            _weaponLabel.Modulate = isEmpowered ? new Color(1.0f, 0.85f, 0.2f) : Colors.White;
        }

        if (_grenadeLabel != null)
        {
            _grenadeLabel.Text = $"GRENADES (G): {grenades}";
        }

        if (_activeReloadControl != null && _activeReloadNeedle != null)
        {
            _activeReloadControl.Visible = isReloading;
            if (isReloading)
            {
                _activeReloadNeedle.Position = new Vector2(reloadProgress * 196.0f, -2.0f);
            }
        }
    }

    private void OnPlayerDied()
    {
        _currentState = WaveState.GameOver;
        if (_activeReloadControl != null)
        {
            _activeReloadControl.Visible = false;
        }
        if (_gameOverPanel != null)
        {
            _gameOverPanel.Visible = true;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("quit"))
        {
            GetTree().Quit();
            return;
        }

        if (_currentState == WaveState.DraftIntermission)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
            {
                if (keyEvent.Keycode == Key.Key1)
                {
                    SelectUpgrade(0);
                }
                else if (keyEvent.Keycode == Key.Key2)
                {
                    SelectUpgrade(1);
                }
                else if (keyEvent.Keycode == Key.Key3)
                {
                    SelectUpgrade(2);
                }
            }
            return;
        }

        if (_currentState == WaveState.GameOver && @event.IsActionPressed("restart"))
        {
            Engine.TimeScale = 1.0f;
            GetTree().Paused = false;
            DecalManager.Instance?.ClearDecals();
            GetTree().ReloadCurrentScene();
        }
    }
}
