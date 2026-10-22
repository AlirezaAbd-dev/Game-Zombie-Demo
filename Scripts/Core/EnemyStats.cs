using Godot;

namespace TopDownDemo.Core;

[GlobalClass]
public partial class EnemyStats : Resource
{
    [Export] public string TypeName { get; set; } = "Walker";
    [Export] public float MaxHealth { get; set; } = 50.0f;
    [Export] public float BaseSpeed { get; set; } = 135.0f;
    [Export] public float AttackDamage { get; set; } = 15.0f;
    [Export] public float AttackInterval { get; set; } = 0.8f;
    [Export] public float AttackRange { get; set; } = 48.0f;
    [Export] public float StopDistance { get; set; } = 35.0f;
    [Export] public float ScaleMultiplier { get; set; } = 1.0f;
    [Export] public float KnockbackResistance { get; set; } = 0.0f;
    [Export] public Color Tint { get; set; } = Colors.White;
    [Export] public Texture2D CustomTexture { get; set; }
    [Export] public Color EyeLightColor { get; set; } = new(1.0f, 0.2f, 0.15f, 1.0f);
    [Export] public float EyeLightEnergy { get; set; } = 0.65f;

    [ExportGroup("Drop Rates")]
    [Export(PropertyHint.Range, "0,1")] public float AmmoDropChance { get; set; } = 0.24f;
    [Export(PropertyHint.Range, "0,1")] public float MedkitDropChance { get; set; } = 0.11f;
}
