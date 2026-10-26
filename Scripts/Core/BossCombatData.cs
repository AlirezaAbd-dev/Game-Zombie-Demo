using Godot;

namespace TopDownDemo.Core;

[GlobalClass]
public partial class BossCombatData : Resource
{
    [Export] public float MeleeWindup { get; set; } = 0.65f;
    [Export] public float AcidWindup { get; set; } = 1.0f;
    [Export] public float RecoverySeconds { get; set; } = 0.55f;
    [Export] public float MeleeCooldown { get; set; } = 1.3f;
    [Export] public float AcidCooldown { get; set; } = 7.0f;
    [Export] public float SweepHalfAngle { get; set; } = 0.95f;
}
