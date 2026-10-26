using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;

namespace TopDownDemo.Systems;

public partial class SaveSystem : Node
{
    private static SaveSystem _instance;
    public static SaveSystem Instance
    {
        get
        {
            return GodotObject.IsInstanceValid(_instance) ? _instance : null;
        }
        private set => _instance = value;
    }

    public const int MaxSlots = 3;
    public int CurrentSlot { get; set; } = 0;
    public GameSaveData ActiveSaveData { get; private set; } = new();
    public bool HasActiveSession { get; private set; }
    private ulong _appliedPlayerId;
    private StartingLoadoutData _startingLoadout;
    public StartingLoadoutData StartingLoadout => _startingLoadout ??= GD.Load<StartingLoadoutData>("res://Resources/Campaign/StartingLoadout.tres");

    public static SaveSystem EnsureInstance(Node owner)
    {
        if (Instance != null) return Instance;
        var system = new SaveSystem { Name = "SaveSystem" };
        owner.AddChild(system);
        return system;
    }

    private static string GetSaveDirectory()
    {
        string userPath = ProjectSettings.GlobalizePath("user://saves");
        return userPath;
    }

    private static string GetSlotFilePath(int slot)
    {
        return Path.Combine(GetSaveDirectory(), $"slot_{slot}.json");
    }

    public override void _EnterTree()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }
        Instance = this;
        // A session outlives individual levels and the main menu.
        Callable.From(() =>
        {
            if (IsInsideTree() && GetParent() != GetTree().Root)
                Reparent(GetTree().Root);
        }).CallDeferred();
    }

    public bool HasSave(int slot)
    {
        if (slot < 0 || slot >= MaxSlots) return false;
        return File.Exists(GetSlotFilePath(slot));
    }

    public GameSaveData GetSlotInfo(int slot)
    {
        if (!HasSave(slot)) return null;

        try
        {
            string json = File.ReadAllText(GetSlotFilePath(slot));
            return JsonSerializer.Deserialize<GameSaveData>(json);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SaveSystem] Failed to read save slot {slot}: {ex.Message}");
            return null;
        }
    }

    public void CreateNewGame(int slot, DifficultyLevel difficulty, bool saveImmediately = true)
    {
        HasActiveSession = true;
        _appliedPlayerId = 0;
        CurrentSlot = slot;
        ActiveSaveData = new GameSaveData
        {
            SlotIndex = slot,
            Difficulty = difficulty,
            CurrentAct = StoryAct.Act1_Arrival,
            CurrentScenePath = "res://Scenes/Act1_Gate.tscn",
            CurrentSceneId = "scene_gate",
            PlayerHealth = 100.0f,
            PlayerMaxHealth = 100.0f,
            PlayerStamina = 100.0f,
            PlaytimeSeconds = 0.0f,
            SavedAt = DateTime.UtcNow,
            HasSavedPosition = false
        };

        var loadout = StartingLoadout;
        if (loadout?.StarterWeapon == null || loadout.Dressing == null)
            throw new InvalidOperationException("Campaign requires a starting weapon and dressing resource.");
        ActiveSaveData.UnlockedWeaponIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { loadout.StarterWeapon.WeaponId };
        InitializeWeaponAmmo(loadout.StarterWeapon,loadout.Rounds(difficulty));
        ActiveSaveData.InventoryItems[loadout.Dressing.ResourcePath] = loadout.Dressings(difficulty);

        if (StoryDirector.Instance != null)
        {
            StoryDirector.Instance.Difficulty = difficulty;
            StoryDirector.Instance.CurrentAct = StoryAct.Act1_Arrival;
        }

        if (saveImmediately) Save(slot);
    }

    private void InitializeWeaponAmmo(WeaponData weapon,int totalRounds)
    {
        if (weapon == null) return;
        int magazine = Math.Min(totalRounds,weapon.MagCapacity);
        ActiveSaveData.MagAmmo[weapon.WeaponId] = magazine;
        ActiveSaveData.ReserveAmmo[weapon.AmmoId] = Math.Min(weapon.MaxReserve,totalRounds - magazine);
    }

    public bool Save(int slot, Player player = null, string currentScenePath = null, StoryAct currentAct = StoryAct.None)
    {
        if (slot < 0 || slot >= MaxSlots) return false;
        var previousData = ActiveSaveData;
        int previousSlot = CurrentSlot;
        try
        {
            ActiveSaveData = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(ActiveSaveData ?? new GameSaveData()));
            CurrentSlot = slot;

            ActiveSaveData ??= new GameSaveData();
            ActiveSaveData.SlotIndex = slot;
            ActiveSaveData.SavedAt = DateTime.UtcNow;

            string liveScenePath = GetTree().CurrentScene?.SceneFilePath;
            if (CampaignActDirector.Instance != null && !string.IsNullOrEmpty(liveScenePath))
                ActiveSaveData.SceneLayoutVersions[liveScenePath] = CampaignActDirector.Instance.LayoutVersion;
            bool isSceneTransition = !string.IsNullOrEmpty(currentScenePath) && currentScenePath != liveScenePath;
            foreach (var node in GetTree().GetNodesInGroup("persistent_world_objects"))
                if (node is TopDownDemo.Entities.Components.WorldStateComponent state) state.Capture();

            if (!string.IsNullOrEmpty(currentScenePath))
            {
                ActiveSaveData.CurrentScenePath = currentScenePath;
            }

            if (currentAct != StoryAct.None)
            {
                ActiveSaveData.CurrentAct = currentAct;
            }

            if (player != null && GodotObject.IsInstanceValid(player))
            {
                ActiveSaveData.PlayerHealth = player.HealthComponent?.CurrentHealth ?? 100.0f;
                ActiveSaveData.PlayerMaxHealth = player.HealthComponent?.MaxHealth ?? 100.0f;
                ActiveSaveData.PlayerStamina = player.StaminaComponent?.CurrentStamina ?? 100.0f;

                if (!isSceneTransition)
                {
                    ActiveSaveData.PlayerPosX = player.GlobalPosition.X;
                    ActiveSaveData.PlayerPosY = player.GlobalPosition.Y;
                    ActiveSaveData.HasSavedPosition = true;
                }
                else
                {
                    ActiveSaveData.HasSavedPosition = false;
                }

                if (player.InventoryComponent != null)
                {
                    ActiveSaveData.EquippedWeaponIndex = player.InventoryComponent.CurrentWeaponIndex;
                    ActiveSaveData.UnlockedWeaponIds = new HashSet<string>(player.InventoryComponent.UnlockedWeaponIds,StringComparer.OrdinalIgnoreCase);

                    ActiveSaveData.MagAmmo.Clear();
                    foreach (var kvp in player.InventoryComponent.GetMagAmmo())
                    {
                        ActiveSaveData.MagAmmo[kvp.Key] = kvp.Value;
                    }

                    ActiveSaveData.ReserveAmmo.Clear();
                    foreach (var kvp in player.InventoryComponent.GetReserveAmmo())
                    {
                        ActiveSaveData.ReserveAmmo[kvp.Key] = kvp.Value;
                    }

                    ActiveSaveData.InventoryItems.Clear();
                    foreach (var missionItem in player.InventoryComponent.MissionItems.Values)
                        ActiveSaveData.InventoryItems[missionItem.Item.ResourcePath] = missionItem.Count;
                    foreach (var slotItem in player.InventoryComponent.GetSlots())
                    {
                        if (slotItem?.Item != null && !string.IsNullOrEmpty(slotItem.Item.ResourcePath))
                        {
                            if (ActiveSaveData.InventoryItems.ContainsKey(slotItem.Item.ResourcePath))
                            {
                                ActiveSaveData.InventoryItems[slotItem.Item.ResourcePath] += slotItem.Count;
                            }
                            else
                            {
                                ActiveSaveData.InventoryItems[slotItem.Item.ResourcePath] = slotItem.Count;
                            }
                        }
                    }
                }
            }

            if (StoryDirector.Instance != null && GodotObject.IsInstanceValid(StoryDirector.Instance))
            {
                ActiveSaveData.PlaytimeSeconds = StoryDirector.Instance.Stats.PlaytimeSeconds;
                ActiveSaveData.TotalKills = StoryDirector.Instance.Stats.TotalKills;
                ActiveSaveData.ShotsFired = StoryDirector.Instance.Stats.ShotsFired;
                ActiveSaveData.ShotsHit = StoryDirector.Instance.Stats.ShotsHit;
                ActiveSaveData.DocumentsFound = StoryDirector.Instance.Stats.DocumentsFound;
                ActiveSaveData.Flags = new System.Collections.Generic.HashSet<string>(StoryDirector.Instance.Flags);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(ActiveSaveData, options);
            Directory.CreateDirectory(GetSaveDirectory());
            string path = GetSlotFilePath(slot);
            File.WriteAllText(path + ".tmp", json);
            File.Move(path + ".tmp", path, true);
            GD.Print($"[SaveSystem] Successfully saved game to slot {slot}.");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SaveSystem] Failed to save game to slot {slot}: {ex.Message}");
            ActiveSaveData = previousData;
            CurrentSlot = previousSlot;
            return false;
        }
    }

    public bool Load(int slot)
    {
        var data = GetSlotInfo(slot);
        if (data == null) return false;

        CurrentSlot = slot;
        ActiveSaveData = data;
        HasActiveSession = true;
        _appliedPlayerId = 0;

        if (StoryDirector.Instance != null)
        {
            StoryDirector.Instance.Difficulty = data.Difficulty;
            StoryDirector.Instance.CurrentAct = data.CurrentAct;
        }

        GD.Print($"[SaveSystem] Successfully loaded game slot {slot} (Act: {data.CurrentAct}, Scene: {data.CurrentScenePath}).");
        return true;
    }

    public bool DeleteSave(int slot)
    {
        if (!HasSave(slot)) return false;

        try
        {
            File.Delete(GetSlotFilePath(slot));
            GD.Print($"[SaveSystem] Deleted save slot {slot}.");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SaveSystem] Error deleting save slot {slot}: {ex.Message}");
            return false;
        }
    }

    public void ApplySaveDataToLoadedScene(Player player)
    {
        if (ActiveSaveData == null || player == null || _appliedPlayerId == player.GetInstanceId()) return;
        _appliedPlayerId = player.GetInstanceId();

        if (StoryDirector.Instance != null && GodotObject.IsInstanceValid(StoryDirector.Instance))
        {
            StoryDirector.Instance.Difficulty = ActiveSaveData.Difficulty;
            StoryDirector.Instance.CurrentAct = ActiveSaveData.CurrentAct;
            StoryDirector.Instance.RestoreMissionStats(ActiveSaveData);
            StoryDirector.Instance.RestoreFlags(ActiveSaveData.Flags);
        }

        if (player != null && GodotObject.IsInstanceValid(player))
        {
            // Reset stealth & locker status so player is visible and has collisions
            player.IsHiddenInLocker = false;
            player.Visible = true;
            player.CollisionLayer = 1;
            player.CollisionMask = 1 | 2;

            bool layoutMatches = CampaignActDirector.Instance == null
                || (ActiveSaveData.SceneLayoutVersions.TryGetValue(GetTree().CurrentScene.SceneFilePath, out int version)
                    && version == CampaignActDirector.Instance.LayoutVersion);
            if (ActiveSaveData.HasSavedPosition && layoutMatches
                && ActiveSaveData.CurrentScenePath == GetTree().CurrentScene.SceneFilePath)
            {
                player.GlobalPosition = new Vector2(ActiveSaveData.PlayerPosX, ActiveSaveData.PlayerPosY);
            }

            if (player.HealthComponent != null)
            {
                player.HealthComponent.Initialize(ActiveSaveData.PlayerMaxHealth);
                player.HealthComponent.SetCurrentHealth(ActiveSaveData.PlayerHealth);
            }
            if (player.StaminaComponent != null)
            {
                player.StaminaComponent.SetCurrentStamina(ActiveSaveData.PlayerStamina);
            }

            // Restore ammo state & weapons
            if (player.InventoryComponent != null)
            {
                player.InventoryComponent.SetUnlockedWeapons(ActiveSaveData.UnlockedWeaponIds);
                player.InventoryComponent.SetAmmoState(ActiveSaveData.MagAmmo, ActiveSaveData.ReserveAmmo, ActiveSaveData.EquippedWeaponIndex);
                RestoreAmmunitionOverflow(player);

                player.InventoryComponent.ClearInventory();
                foreach (var kvp in ActiveSaveData.InventoryItems)
                {
                    if (!ResourceLoader.Exists(kvp.Key)) continue;
                    var item = GD.Load<ItemData>(kvp.Key);
                    if (item == null || kvp.Value <= 0) continue;
                    if (player.InventoryComponent.AddItem(item,kvp.Value)) continue;
                    // Migrate old oversized stacks without discarding earned supplies.
                    for (int unit = 0; unit < kvp.Value; unit++)
                        if (!player.InventoryComponent.AddItem(item))
                            CampaignLootDirector.Instance?.SpawnEnemyDrop(
                                GetTree().CurrentScene.SceneFilePath + "::restore-overflow::" + item.Id + "::" + unit,
                                item,player.GlobalPosition);
                }
            }
        }
    }

    private void RestoreAmmunitionOverflow(Player player)
    {
        if (CampaignLootDirector.Instance == null) return;
        var inventory = player.InventoryComponent;
        var visited = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var weapon in inventory.Weapons)
        {
            if (weapon?.AmmoPickup == null || !visited.Add(weapon.AmmoId)) continue;
            long saved = Math.Max(0,ActiveSaveData.ReserveAmmo.GetValueOrDefault(weapon.AmmoId,0));
            long restored = inventory.GetReserveAmmo().GetValueOrDefault(weapon.AmmoId,0);
            foreach (var sameAmmoWeapon in inventory.Weapons)
            {
                if (sameAmmoWeapon == null || !string.Equals(sameAmmoWeapon.AmmoId,weapon.AmmoId,StringComparison.OrdinalIgnoreCase)) continue;
                saved += Math.Max(0,ActiveSaveData.MagAmmo.GetValueOrDefault(sameAmmoWeapon.WeaponId,0));
                restored += inventory.GetMagAmmo().GetValueOrDefault(sameAmmoWeapon.WeaponId,0);
            }
            long excess = saved - restored;
            for (int batch = 0; excess > 0; batch++)
            {
                int rounds = (int)Math.Min(excess,int.MaxValue);
                CampaignLootDirector.Instance.SpawnEnemyDrop(
                    GetTree().CurrentScene.SceneFilePath + "::restore-ammo-overflow::" + weapon.AmmoId + "::" + batch,
                    weapon.AmmoPickup,player.GlobalPosition,rounds);
                excess -= rounds;
            }
        }
    }
}
