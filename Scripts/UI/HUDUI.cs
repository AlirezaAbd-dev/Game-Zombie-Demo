using Godot;
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
    [Export] public Label KillLabel { get; set; }
    [Export] public Control GameOverPanel { get; set; }

    public override void _Ready()
    {
        HealthBar ??= GetNodeOrNull<ProgressBar>("%HealthBar");
        HealthLabel ??= GetNodeOrNull<Label>("%HealthLabel");
        StaminaBar ??= GetNodeOrNull<ProgressBar>("%StaminaBar");
        StaminaLabel ??= GetNodeOrNull<Label>("%StaminaLabel");
        AmmoLabel ??= GetNodeOrNull<Label>("%AmmoLabel");
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

    private void BindPlayer()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player == null) return;

        if (player.HealthComponent != null)
        {
            player.HealthComponent.HealthChanged += OnHealthChanged;
            OnHealthChanged(player.HealthComponent.CurrentHealth, player.HealthComponent.MaxHealth);
        }

        if (player.StaminaComponent != null)
        {
            player.StaminaComponent.StaminaChanged += OnStaminaChanged;
            OnStaminaChanged(player.StaminaComponent.CurrentStamina, player.StaminaComponent.MaxStamina);
        }

        if (player.InventoryComponent != null)
        {
            player.InventoryComponent.AmmoChanged += OnAmmoChanged;
            OnAmmoChanged(player.InventoryComponent.CurrentAmmo, player.InventoryComponent.ReserveAmmo, player.InventoryComponent.IsReloading);
        }

        player.PlayerDied += OnPlayerDied;
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
            AmmoLabel.Modulate = current <= 3 ? new Color(1.0f, 0.6f, 0.2f, 1.0f) : Colors.White;
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
