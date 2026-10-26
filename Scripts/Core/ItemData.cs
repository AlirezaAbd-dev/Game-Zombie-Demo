using Godot;

namespace TopDownDemo.Core;

public enum ItemType
{
    Health,
    Ammo,
    Key,
    Document,
    Herb,
    Booster,
    Weapon
}

[GlobalClass]
public partial class ItemData : Resource
{
    [Export] public string Id { get; set; } = string.Empty;
    [Export] public string ItemName { get; set; } = string.Empty;
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
    [Export] public ItemType Type { get; set; } = ItemType.Health;
    [Export] public float Value { get; set; } = 35.0f;
    [Export] public float StaminaRestore { get; set; }
    [Export] public int MaxStack { get; set; } = 99;
    [Export] public Texture2D Icon { get; set; }
    [Export] public WeaponData GrantedWeapon { get; set; }
    [Export] public int LegacyAmmoRounds { get; set; }
    [Export(PropertyHint.Range,"12,48,1")] public float WorldIconSize { get; set; } = 32;
    [Export] public string CombineWithId { get; set; } = string.Empty;
    [Export] public string CombineResultId { get; set; } = string.Empty;
}
