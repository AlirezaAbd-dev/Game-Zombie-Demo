using System;
using System.Collections.Generic;

namespace TopDownDemo.Core;

public class GameSaveData
{
    public int SlotIndex { get; set; } = 0;
    public string PlayerName { get; set; } = "Sara Nazari";
    public StoryAct CurrentAct { get; set; } = StoryAct.Act1_Arrival;
    public string CurrentScenePath { get; set; } = "res://Scenes/Act1_Gate.tscn";
    public string CurrentSceneId { get; set; } = "scene_gate";
    public float PlayerHealth { get; set; } = 100.0f;
    public float PlayerMaxHealth { get; set; } = 100.0f;
    public float PlayerStamina { get; set; } = 100.0f;
    public int EquippedWeaponIndex { get; set; } = 0;
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Survivor;
    public float PlaytimeSeconds { get; set; } = 0.0f;
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;

    // Inventory items (Item Resource Path or ID -> Quantity)
    public Dictionary<string, int> InventoryItems { get; set; } = new();

    // Weapon Ammo state (WeaponId -> MagCount, AmmoId -> ReserveCount)
    public Dictionary<string, int> MagAmmo { get; set; } = new();
    public Dictionary<string, int> ReserveAmmo { get; set; } = new();

    // Player position in scene
    public float PlayerPosX { get; set; } = 0.0f;
    public float PlayerPosY { get; set; } = 0.0f;
    public bool HasSavedPosition { get; set; } = false;

    // Story flags
    public HashSet<string> Flags { get; set; } = new();

    // Major dialogue choices recorded (0 = unchosen, 1 = Option A, 2 = Option B, 3 = Option C)
    public int DialogueChoice_Sadeghi1 { get; set; } = 0;
    public int DialogueChoice_Leila { get; set; } = 0;
    public int DialogueChoice_Betrayal { get; set; } = 0;

    public bool FoundArchivist { get; set; } = false;
    public bool FoundGhorbaniFiles { get; set; } = false;
}
