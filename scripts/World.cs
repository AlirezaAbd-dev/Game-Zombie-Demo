using Godot;

public partial class World : Node2D
{
    [Export] public float SpawnInterval { get; set; } = 2.5f;
    [Export] public int MaxZombies { get; set; } = 25;

    private PackedScene _zombieScene;
    public int KillCount { get; private set; } = 0;
    private float _spawnTimer = 0.0f;

    private Player _player;
    private ProgressBar _healthBar;
    private Label _healthLabel;
    private ProgressBar _staminaBar;
    private Label _staminaLabel;
    private Label _ammoLabel;
    private Label _killLabel;
    private Control _gameOverPanel;

    public override void _Ready()
    {
        _zombieScene = GD.Load<PackedScene>("res://scenes/zombie.tscn");

        _player = GetNodeOrNull<Player>("Player");
        _healthBar = GetNodeOrNull<ProgressBar>("UIOverlay/HUD/HealthBar");
        _healthLabel = GetNodeOrNull<Label>("UIOverlay/HUD/HealthBar/HealthLabel");
        _staminaBar = GetNodeOrNull<ProgressBar>("UIOverlay/HUD/StaminaBar");
        _staminaLabel = GetNodeOrNull<Label>("UIOverlay/HUD/StaminaBar/StaminaLabel");
        _ammoLabel = GetNodeOrNull<Label>("UIOverlay/HUD/AmmoLabel");
        _killLabel = GetNodeOrNull<Label>("UIOverlay/HUD/KillLabel");
        _gameOverPanel = GetNodeOrNull<Control>("UIOverlay/GameOverPanel");

        if (_player != null)
        {
            _player.HealthChanged += OnPlayerHealthChanged;
            _player.StaminaChanged += OnPlayerStaminaChanged;
            _player.AmmoChanged += OnPlayerAmmoChanged;
            _player.PlayerDied += OnPlayerDied;

            OnPlayerHealthChanged(_player.Health, _player.MaxHealth);
            OnPlayerStaminaChanged(_player.CurrentStamina, _player.MaxStamina);
            OnPlayerAmmoChanged(_player.CurrentAmmo, _player.ReserveAmmo, _player.IsReloading);
        }

        if (_gameOverPanel != null)
        {
            _gameOverPanel.Visible = false;
        }

        UpdateKillCounter();
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_player) || _player.IsDead)
            return;

        _spawnTimer += (float)delta;
        if (_spawnTimer >= SpawnInterval)
        {
            _spawnTimer = 0.0f;
            SpawnZombieWave();
        }
    }

    private void SpawnZombieWave()
    {
        int currentZombies = GetTree().GetNodesInGroup("zombies").Count;
        if (currentZombies >= MaxZombies)
            return;

        int spawnCount = (int)GD.RandRange(1, 2);
        for (int i = 0; i < spawnCount; i++)
        {
            if (!IsInstanceValid(_player)) break;

            float angle = (float)GD.RandRange(0.0, Mathf.Tau);
            float distance = (float)GD.RandRange(750.0, 950.0);
            Vector2 spawnPos = _player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            var zombie = _zombieScene.Instantiate<CharacterBody2D>();
            zombie.GlobalPosition = spawnPos;
            AddChild(zombie);
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

    private void OnPlayerDied()
    {
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
        }
        else if (@event.IsActionPressed("restart") && _player != null && _player.IsDead)
        {
            GetTree().ReloadCurrentScene();
        }
    }
}
