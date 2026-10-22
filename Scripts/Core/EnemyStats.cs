using Godot;

namespace TopDownDemo.Core;

[GlobalClass]
public partial class EnemyStats : Resource
{
    [Export] public float MaxHealth { get; set; } = 50.0f;
    [Export] public float BaseSpeed { get; set; } = 135.0f;
    [Export] public float AttackDamage { get; set; } = 15.0f;
    [Export] public float AttackInterval { get; set; } = 0.8f;
    [Export] public float AttackRange { get; set; } = 48.0f;
    [Export] public float StopDistance { get; set; } = 35.0f;
    [Export(PropertyHint.Range, "0,1")] public float AmmoDropChance { get; set; } = 0.24f;
    [Export(PropertyHint.Range, "0,1")] public float MedkitDropChance { get; set; } = 0.11f;
}
