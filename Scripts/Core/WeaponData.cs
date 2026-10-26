using Godot;

namespace TopDownDemo.Core;

[GlobalClass]
public partial class WeaponData : Resource
{
    [Export] public string WeaponId { get; set; } = "handgun";
    [Export] public string WeaponName { get; set; } = "9mm Handgun";
    [Export] public string AmmoId { get; set; } = "ammo_handgun";
    [Export] public ItemData AmmoPickup { get; set; }
    [Export] public Texture2D WeaponSprite { get; set; }
    [Export] public Vector2 WeaponOffset { get; set; } = new(12.333334f, -0.33333334f);
    [Export] public Vector2 MuzzleOffset { get; set; } = new(55.0f, 0.0f);

    [ExportGroup("Combat Parameters")]
    [Export] public float Damage { get; set; } = 25.0f;
    [Export] public float FireRate { get; set; } = 0.14f;
    [Export] public int Pellets { get; set; } = 1;
    [Export] public float BaseSpread { get; set; } = 0.045f;
    [Export] public float AimSpread { get; set; } = 0.006f;
    [Export] public int MagCapacity { get; set; } = 15;
    [Export] public int MaxReserve { get; set; } = 90;
    [Export] public float ReloadDuration { get; set; } = 1.2f;
    [Export] public float CameraTrauma { get; set; } = 0.18f;
    [Export] public float RecoilKick { get; set; } = 3.5f;
    [Export] public float ProjectileSpeed { get; set; } = 1400.0f;
    [Export] public float NoiseRadius { get; set; } = 450.0f;
    [Export] public Color MuzzleFlashColor { get; set; } = new(1.0f, 0.85f, 0.45f, 1.0f);
}
