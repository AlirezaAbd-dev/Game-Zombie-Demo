using System;
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
            if (!GodotObject.IsInstanceValid(_instance))
            {
                _instance = new SaveSystem();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    public const int MaxSlots = 3;
    public int CurrentSlot { get; set; } = 0;
    public GameSaveData ActiveSaveData { get; private set; } = new();

    private static string GetSaveDirectory()
    {
        string userPath = ProjectSettings.GlobalizePath("user://saves");
        if (!Directory.Exists(userPath))
        {
            Directory.CreateDirectory(userPath);
        }
        return userPath;
    }

    private static string GetSlotFilePath(int slot)
    {
        return Path.Combine(GetSaveDirectory(), $"slot_{slot}.json");
    }

    public override void _EnterTree()
    {
        Instance = this;
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

    public void CreateNewGame(int slot, DifficultyLevel difficulty)
    {
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

        // Initialize weapon ammo scaled by difficulty
        int pistolReserve = difficulty == DifficultyLevel.Survivor ? 45 : (difficulty == DifficultyLevel.Nightmare ? 25 : 12);
        int shotgunReserve = difficulty == DifficultyLevel.Survivor ? 12 : (difficulty == DifficultyLevel.Nightmare ? 6 : 4);
        int rifleReserve = difficulty == DifficultyLevel.Survivor ? 10 : (difficulty == DifficultyLevel.Nightmare ? 5 : 2);

        ActiveSaveData.MagAmmo["handgun"] = 15;
        ActiveSaveData.MagAmmo["shotgun"] = 6;
        ActiveSaveData.MagAmmo["rifle"] = 10;

        ActiveSaveData.ReserveAmmo["ammo_handgun"] = pistolReserve;
        ActiveSaveData.ReserveAmmo["ammo_shotgun"] = shotgunReserve;
        ActiveSaveData.ReserveAmmo["ammo_rifle"] = rifleReserve;

        ActiveSaveData.InventoryItems["res://Resources/Items/Bandage.tres"] = difficulty == DifficultyLevel.Survivor ? 2 : 1;

        if (StoryDirector.Instance != null)
        {
            StoryDirector.Instance.Difficulty = difficulty;
            StoryDirector.Instance.CurrentAct = StoryAct.Act1_Arrival;
        }

        Save(slot);
    }

    public bool Save(int slot, Player player = null, string currentScenePath = null, StoryAct currentAct = StoryAct.None)
    {
        if (slot < 0 || slot >= MaxSlots) return false;
        CurrentSlot = slot;

        ActiveSaveData ??= new GameSaveData();
        ActiveSaveData.SlotIndex = slot;
        ActiveSaveData.SavedAt = DateTime.UtcNow;

        bool isSceneTransition = !string.IsNullOrEmpty(currentScenePath);

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
            ActiveSaveData.Flags = new System.Collections.Generic.HashSet<string>(StoryDirector.Instance.Flags);
        }

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(ActiveSaveData, options);
            File.WriteAllText(GetSlotFilePath(slot), json);
            GD.Print($"[SaveSystem] Successfully saved game to slot {slot}.");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SaveSystem] Failed to save game to slot {slot}: {ex.Message}");
            return false;
        }
    }

    public bool Load(int slot)
    {
        var data = GetSlotInfo(slot);
        if (data == null) return false;

        CurrentSlot = slot;
        ActiveSaveData = data;

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
        if (ActiveSaveData == null) return;

        if (StoryDirector.Instance != null && GodotObject.IsInstanceValid(StoryDirector.Instance))
        {
            StoryDirector.Instance.Difficulty = ActiveSaveData.Difficulty;
            StoryDirector.Instance.CurrentAct = ActiveSaveData.CurrentAct;
            StoryDirector.Instance.Stats.PlaytimeSeconds = ActiveSaveData.PlaytimeSeconds;
            StoryDirector.Instance.Flags.Clear();
            foreach (var flag in ActiveSaveData.Flags)
            {
                StoryDirector.Instance.Flags.Add(flag);
            }
        }

        if (player != null && GodotObject.IsInstanceValid(player))
        {
            // Reset stealth & locker status so player is visible and has collisions
            player.IsHiddenInLocker = false;
            player.Visible = true;
            player.CollisionLayer = 1;
            player.CollisionMask = 1 | 2;

            if (ActiveSaveData.HasSavedPosition)
            {
                player.GlobalPosition = new Vector2(ActiveSaveData.PlayerPosX, ActiveSaveData.PlayerPosY);
            }

            if (player.HealthComponent != null)
            {
                player.HealthComponent.SetCurrentHealth(ActiveSaveData.PlayerHealth);
            }
            if (player.StaminaComponent != null)
            {
                player.StaminaComponent.SetCurrentStamina(ActiveSaveData.PlayerStamina);
            }

            // Restore ammo state & weapons
            if (player.InventoryComponent != null)
            {
                player.InventoryComponent.SetAmmoState(ActiveSaveData.MagAmmo, ActiveSaveData.ReserveAmmo, ActiveSaveData.EquippedWeaponIndex);

                // Populate inventory items
                if (ActiveSaveData.InventoryItems.Count > 0)
                {
                    player.InventoryComponent.ClearInventory();
                    foreach (var kvp in ActiveSaveData.InventoryItems)
                    {
                        if (ResourceLoader.Exists(kvp.Key))
                        {
                            var itemRes = GD.Load<ItemData>(kvp.Key);
                            if (itemRes != null)
                            {
                                player.InventoryComponent.AddItem(itemRes, kvp.Value);
                            }
                        }
                    }
                }
            }
        }
    }
}
