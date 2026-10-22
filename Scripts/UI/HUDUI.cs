using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.Entities.Components;

namespace TopDownDemo.UI;

public partial class HUDUI : Control
{
    [ExportGroup("UI Elements")]
    [Export] public ProgressBar HealthBar { get; set; }
    [Export] public Label HealthLabel { get; set; }
    [Export] public ProgressBar StaminaBar { get; set; }
    [Export] public Label StaminaLabel { get; set; }
    [Export] public Label AmmoLabel { get; set; }
    [Export] public Label WeaponLabel { get; set; }
    [Export] public Label KillLabel { get; set; }
    [Export] public Control GameOverPanel { get; set; }

    private Player _player;
    private WeaponData _currentWeapon;

    public override void _Ready()
    {
        HealthBar ??= GetNodeOrNull<ProgressBar>("%HealthBar");
        HealthLabel ??= GetNodeOrNull<Label>("%HealthLabel");
        StaminaBar ??= GetNodeOrNull<ProgressBar>("%StaminaBar");
        StaminaLabel ??= GetNodeOrNull<Label>("%StaminaLabel");
        AmmoLabel ??= GetNodeOrNull<Label>("%AmmoLabel");
        WeaponLabel ??= GetNodeOrNull<Label>("%WeaponLabel");
        KillLabel ??= GetNodeOrNull<Label>("%KillLabel");
        GameOverPanel ??= GetNodeOrNull<Control>("%GameOverPanel");

        if (GameOverPanel != null)
        {
            GameOverPanel.Visible = false;
        }

        BindPlayer();
        Zombie.OnAnyZombieKilled += OnZombieKilled;
    }

    public override void _ExitTree()
    {
        Zombie.OnAnyZombieKilled -= OnZombieKilled;
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_player) && !_player.IsDead)
        {
            if (WeaponLabel != null && _currentWeapon != null)
            {
                string aimIndicator = _player.IsAiming ? " [AIMING]" : "";
                WeaponLabel.Text = $"{_currentWeapon.WeaponName.ToUpper()}{aimIndicator} (Q/1-3)";
                WeaponLabel.Modulate = _player.IsAiming ? new Color(1.0f, 0.4f, 0.4f, 1.0f) : new Color(0.85f, 0.9f, 1.0f, 0.9f);
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

        _player.PlayerDied += OnPlayerDied;
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

    private int _kills = 0;
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
}
