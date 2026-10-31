using System.Collections.Generic;

namespace GameZombieDemo;

public enum WeaponId
{
    Pistol = 0,
    Shotgun = 1,
    Smg = 2
}

public sealed class WeaponSpec
{
    public WeaponId Id { get; init; }
    public string DisplayName { get; init; } = "";
    public float FireInterval { get; init; }
    public int PelletCount { get; init; }
    public float BaseSpreadRad { get; init; }
    public float MaxBloomRad { get; init; }
    public float BloomPerShotRad { get; init; }
    public float BloomDecayPerSec { get; init; }
    public float BulletSpeed { get; init; }
    public float BulletDamage { get; init; }
    public float BulletLifetime { get; init; }
    public float KnockbackMultiplier { get; init; }
    public int PiercingCount { get; init; }
    public int MagCapacity { get; init; }
    public int MaxReserve { get; init; }
    public bool InfiniteReserve { get; init; }
    public float ReloadDuration { get; init; }
    public float ShakeTrauma { get; init; }
    public string SfxKey { get; init; } = "";

    public static readonly Dictionary<WeaponId, WeaponSpec> Presets = new()
    {
        [WeaponId.Pistol] = new WeaponSpec
        {
            Id = WeaponId.Pistol,
            DisplayName = "Pistol",
            FireInterval = 0.14f,
            PelletCount = 1,
            BaseSpreadRad = 0.02f,
            MaxBloomRad = 0.06f,
            BloomPerShotRad = 0.015f,
            BloomDecayPerSec = 0.12f,
            BulletSpeed = 1450f,
            BulletDamage = 26f,
            BulletLifetime = 1.3f,
            KnockbackMultiplier = 1.0f,
            PiercingCount = 0,
            MagCapacity = 15,
            MaxReserve = 999,
            InfiniteReserve = true,
            ReloadDuration = 1.1f,
            ShakeTrauma = 0.16f,
            SfxKey = "SfxPistol"
        },
        [WeaponId.Shotgun] = new WeaponSpec
        {
            Id = WeaponId.Shotgun,
            DisplayName = "Shotgun",
            FireInterval = 0.62f,
            PelletCount = 6,
            BaseSpreadRad = 0.28f,
            MaxBloomRad = 0.35f,
            BloomPerShotRad = 0.05f,
            BloomDecayPerSec = 0.20f,
            BulletSpeed = 1250f,
            BulletDamage = 18f,
            BulletLifetime = 0.42f,
            KnockbackMultiplier = 2.4f,
            PiercingCount = 1,
            MagCapacity = 6,
            MaxReserve = 36,
            InfiniteReserve = false,
            ReloadDuration = 1.45f,
            ShakeTrauma = 0.42f,
            SfxKey = "SfxShotgun"
        },
        [WeaponId.Smg] = new WeaponSpec
        {
            Id = WeaponId.Smg,
            DisplayName = "SMG",
            FireInterval = 0.075f,
            PelletCount = 1,
            BaseSpreadRad = 0.045f,
            MaxBloomRad = 0.20f,
            BloomPerShotRad = 0.022f,
            BloomDecayPerSec = 0.28f,
            BulletSpeed = 1500f,
            BulletDamage = 15.5f,
            BulletLifetime = 1.0f,
            KnockbackMultiplier = 0.65f,
            PiercingCount = 0,
            MagCapacity = 32,
            MaxReserve = 160,
            InfiniteReserve = false,
            ReloadDuration = 1.25f,
            ShakeTrauma = 0.11f,
            SfxKey = "SfxSmg"
        }
    };
}
