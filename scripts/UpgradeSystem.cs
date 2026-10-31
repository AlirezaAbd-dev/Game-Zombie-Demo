using System;
using System.Collections.Generic;

namespace GameZombieDemo;

public enum UpgradeId
{
    HollowPoint,
    OverclockedReceiver,
    PlasmaDash,
    VampiricSiphon,
    TungstenCores,
    MunitionsDrop
}

public sealed class UpgradeCard
{
    public UpgradeId Id { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public Action<Player> Apply { get; init; } = null!;
}

public static class UpgradeCatalog
{
    public static readonly List<UpgradeCard> AllUpgrades = new()
    {
        new UpgradeCard
        {
            Id = UpgradeId.HollowPoint,
            Title = "Hollow-Point Rounds",
            Description = "+22% Bullet Damage & +25% Knockback",
            Apply = player =>
            {
                player.DamageMultiplier *= 1.22f;
            }
        },
        new UpgradeCard
        {
            Id = UpgradeId.OverclockedReceiver,
            Title = "Overclocked Receiver",
            Description = "+18% Fire Rate & -18% Reload Duration",
            Apply = player =>
            {
                player.FireRateMultiplier *= 1.18f;
                player.ReloadDurationMultiplier *= 0.82f;
            }
        },
        new UpgradeCard
        {
            Id = UpgradeId.PlasmaDash,
            Title = "Plasma Afterburner",
            Description = "+30 Max Stamina & Dash scorches enemies for 40 DMG",
            Apply = player =>
            {
                player.MaxStamina += 30f;
                player.DashBurnsEnemies = true;
            }
        },
        new UpgradeCard
        {
            Id = UpgradeId.VampiricSiphon,
            Title = "Vampiric Siphon",
            Description = "Heal 6 HP every 6th kill",
            Apply = player =>
            {
                player.VampiricKillInterval = 6;
            }
        },
        new UpgradeCard
        {
            Id = UpgradeId.TungstenCores,
            Title = "Tungsten Penetrators",
            Description = "+1 Piercing on all weapons",
            Apply = player =>
            {
                player.BonusPierce += 1;
            }
        },
        new UpgradeCard
        {
            Id = UpgradeId.MunitionsDrop,
            Title = "Tactical Resupply",
            Description = "Refill 60% Reserve Ammo & +1 Frag Grenade immediately",
            Apply = player =>
            {
                player.AddReserveAmmo(50);
                player.Grenades = Math.Min(player.MaxGrenades, player.Grenades + 1);
            }
        }
    };

    public static List<UpgradeCard> GetRandomPicks(int count = 3)
    {
        var pool = new List<UpgradeCard>(AllUpgrades);
        var picks = new List<UpgradeCard>();
        var rng = new Random();

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = rng.Next(pool.Count);
            picks.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return picks;
    }
}
