using System;
using Godot;
using TopDownDemo.Core;

namespace TopDownDemo.Entities.Components;

[GlobalClass]
public partial class HealthComponent : Node, IDamageable
{
    [Signal]
    public delegate void HealthChangedEventHandler(float current, float max);

    [Signal]
    public delegate void DiedEventHandler();

    [Export] public float MaxHealth { get; set; } = 100.0f;

    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0.0f;

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
    }

    public void Initialize(float maxHealth)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
    }

    public void TakeDamage(float amount, Vector2 knockbackDir)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Max(0.0f, CurrentHealth - amount);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);

        if (IsDead)
        {
            EmitSignal(SignalName.Died);
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
    }
}
