using Godot;

namespace TopDownDemo.Entities.Components;

[GlobalClass]
public partial class StaminaComponent : Node
{
    [Signal]
    public delegate void StaminaChangedEventHandler(float current, float max);

    [Export] public float MaxStamina { get; set; } = 100.0f;
    [Export] public float DashCost { get; set; } = 35.0f;
    [Export] public float DashSpeed { get; set; } = 750.0f;
    [Export] public float DashDuration { get; set; } = 0.22f;
    [Export] public float RegenRate { get; set; } = 30.0f;

    public float CurrentStamina { get; private set; }

    public override void _Ready()
    {
        CurrentStamina = MaxStamina;
    }

    public bool CanDash() => CurrentStamina >= DashCost;

    public bool TryConsume(float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0) return false;
        if (CurrentStamina < amount) return false;

        CurrentStamina -= amount;
        EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
        return true;
    }

    public bool TryConsumeDash()
    {
        return TryConsume(DashCost);
    }

    public void Regenerate(float delta)
    {
        Restore(RegenRate * delta);
    }

    public float Restore(float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0 || CurrentStamina >= MaxStamina) return 0;
        float restored = Mathf.Min(amount, MaxStamina - CurrentStamina);
        CurrentStamina += restored;
        EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
        return restored;
    }

    public void SetCurrentStamina(float stamina)
    {
        CurrentStamina = Mathf.Clamp(stamina, 0.0f, MaxStamina);
        EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
    }
}
