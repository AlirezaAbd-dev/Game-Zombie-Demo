using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.Entities.Components;
using TopDownDemo.UI;

namespace TopDownDemo.Systems.Tests;

/// <summary>Inventory transactions and generated loot, run only with isolated XDG save directories.</summary>
public partial class PersistenceSmokeTest : Node
{
    private const string Opening = "res://Scenes/Act1_Gate.tscn";
    private int _checks;
    // Keep test-only cloned resources alive throughout deliberate GC stress.
    private readonly List<Resource> _fixtures = new();
    private SceneTree Tree => GetTree();
    private Player Actor => Tree.GetFirstNodeInGroup("player") as Player;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try
        {
            Tree.CurrentScene = null;
            var saves = SaveSystem.EnsureInstance(this);
            saves.CreateNewGame(0,DifficultyLevel.Survivor);
            var rifle = GD.Load<WeaponData>("res://Resources/Weapons/Rifle.tres");
            Check(saves.ActiveSaveData.MagAmmo.GetValueOrDefault(rifle.WeaponId) == 0
                && saves.ActiveSaveData.ReserveAmmo.GetValueOrDefault(rifle.AmmoId) == 0
                && saves.ActiveSaveData.UnlockedWeaponIds.SetEquals(new[] { "handgun" }),
                "new game starts with only its authored handgun loadout");
            saves.ActiveSaveData.UnlockedWeaponIds = new HashSet<string> { "handgun", "shotgun", "rifle" };
            saves.ActiveSaveData.Flags.Add("intro_seen_act1_gate");
            await Level();
            VerifyInventoryTransactions();
            VerifyAmmunition();
            await VerifyInventoryControls();
            await VerifyLegacyOverflow(saves);
            await VerifyEnemyLoot(saves,"Infected1",true);
            await VerifyEnemyLoot(saves,"Infected2",false);
            GD.Print($"PERSISTENCE SMOKE PASS: {_checks} checks");
            Tree.CurrentScene.QueueFree();
            await ToSignal(Tree.CreateTimer(0.2),SceneTreeTimer.SignalName.Timeout);
            Tree.Quit();
        }
        catch (Exception error) { GD.PushError("PERSISTENCE SMOKE FAIL: " + error); Tree.Quit(1); }
    }

    private void VerifyInventoryTransactions()
    {
        var inventory = Actor.InventoryComponent;
        var bandage = GD.Load<ItemData>("res://Resources/Items/Bandage.tres");
        inventory.ClearInventory();
        Check(inventory.AddItem(bandage,13),"bulk consumables can span several stacks");
        Check(inventory.Slots.Count(slot => slot.Item != null) == 3 && Count("bandage") == 13
            && inventory.Slots.All(slot => slot.Count <= bandage.MaxStack),"no stack exceeds its authored limit");
        Check(!inventory.ConsumeItem("bandage",0) && !inventory.ConsumeItem("bandage",-1) && Count("bandage") == 13,
            "invalid consumption cannot manufacture items");
        Check(inventory.ConsumeItem("BANDAGE",2) && Count("bandage") == 11,"consumption follows case-insensitive item lookup");
        inventory.ClearInventory();
        inventory.AddItem(bandage,bandage.MaxStack * InventoryComponent.MaxSlots);
        inventory.ConsumeItem("bandage");
        Check(!inventory.AddItem(bandage,2) && Count("bandage") == 47,"failed bulk addition changes no partial stack");
        var green = GD.Load<ItemData>("res://Resources/Items/Herb_Green.tres");
        inventory.ClearInventory();
        inventory.AddItem(green,green.MaxStack * 2);
        inventory.AddItem(bandage,bandage.MaxStack * 6);
        Check(!inventory.CombineSlots(0,1) && Count("herb_green") == green.MaxStack * 2,
            "crafting with no result space preserves both ingredient stacks");
        inventory.ConsumeItem("bandage",bandage.MaxStack);
        Check(inventory.CombineSlots(0,1) && Count("herb_green_double") == 1,"crafting succeeds once output space exists");
        Check(inventory.Slots[0].Count == green.MaxStack - 1 && inventory.Slots[1].Count == green.MaxStack - 1,
            "crafting consumes one unit from each selected stack");
        Check(inventory.CombineSlots(0,0) && Count("herb_green") == green.MaxStack * 2 - 4
            && Count("herb_green_double") == 2,"two herbs in one stack can supply both recipe ingredients");
        inventory.ClearInventory(); inventory.AddItem(green);
        Check(!inventory.CombineSlots(0,0) && Count("herb_green") == 1,"a single herb cannot supply two ingredients");
    }

    private void VerifyAmmunition()
    {
        var inventory = Actor.InventoryComponent;
        var handgun = inventory.Weapons[0];
        var shotgun = inventory.Weapons[1];
        inventory.SetAmmoState(new Dictionary<string,int> { [handgun.WeaponId] = -10, [shotgun.WeaponId] = 10000 },
            new Dictionary<string,int> { [handgun.AmmoId] = -30, [shotgun.AmmoId] = 10000 });
        Check(inventory.CurrentAmmo == 0 && inventory.CurrentReserve == 0
            && inventory.GetMagAmmo()[shotgun.WeaponId] == shotgun.MagCapacity
            && inventory.GetReserveAmmo()[shotgun.AmmoId] == shotgun.MaxReserve,
            "loaded ammunition respects zero, magazine and reserve limits for every weapon");
        inventory.SelectWeapon(0);
        inventory.AddReserveAmmo(shotgun.AmmoId,10000);
        Check(inventory.GetReserveAmmo()[shotgun.AmmoId] == shotgun.MaxReserve,
            "unequipped weapon pickup uses its own reserve capacity");
        var pickup = GD.Load<PackedScene>("res://Prefabs/Pickup.tscn").Instantiate<Pickup>();
        pickup.Item = GD.Load<ItemData>("res://Resources/Items/AmmoShotgun.tres");
        Tree.CurrentScene.AddChild(pickup);
        pickup.GlobalPosition = new Vector2(2800,2800);
        Check(!pickup.CollectByPlayer(Actor) && !pickup.IsQueuedForDeletion(),
            "full reserve leaves an ammo pickup on the ground");
        pickup.QueueFree();
        inventory.SetAmmoState(new Dictionary<string,int> { [handgun.WeaponId] = handgun.MagCapacity - 5 },
            new Dictionary<string,int> { [handgun.AmmoId] = 3 });
        Check(inventory.StartReload() && !inventory.TryConsumeAmmo(),"reload prevents firing before ammunition transfer");
        inventory.UpdateReload(0.01f);
        Check(inventory.CurrentAmmo == handgun.MagCapacity - 5 && inventory.CurrentReserve == 3,
            "reload does not transfer ammunition before completion");
        inventory.SelectWeapon(1);
        inventory.UpdateReload(100);
        inventory.SelectWeapon(0);
        Check(!inventory.IsReloading && inventory.CurrentAmmo == handgun.MagCapacity - 5 && inventory.CurrentReserve == 3,
            "weapon switch cancels reload without duplicating ammunition");
        inventory.StartReload();
        inventory.UpdateReload(handgun.ReloadDuration + 0.1f);
        Check(inventory.CurrentAmmo == handgun.MagCapacity - 2 && inventory.CurrentReserve == 0,
            "completed partial reload conserves magazine plus reserve totals");
        inventory.AddReserveAmmo(handgun.AmmoId,-10);
        Check(inventory.CurrentReserve == 0,"negative reserve addition cannot remove or create ammunition");
        inventory.SetAmmoState(new Dictionary<string,int> { [handgun.WeaponId] = handgun.MagCapacity + 5 },
            new Dictionary<string,int> { [handgun.AmmoId] = 3 });
        Check(inventory.CurrentAmmo == handgun.MagCapacity && inventory.CurrentReserve == 8,
            "legacy overfilled magazine transfers excess rounds to reserve without loss");
    }

    private async Task VerifyInventoryControls()
    {
        var inventory = Actor.InventoryComponent;
        inventory.ClearInventory();
        inventory.AddItem(GD.Load<ItemData>("res://Resources/Items/Herb_Green.tres"),2);
        inventory.AddItem(GD.Load<ItemData>("res://Resources/Items/Ghorbani_Files.tres"));
        inventory.AddItem(GD.Load<ItemData>("res://Resources/Items/ArchiveVaultKey.tres"));
        var ui = FindInventory(Tree.CurrentScene);
        ui.ToggleInventory();
        await ToSignal(Tree,SceneTree.SignalName.ProcessFrame);
        Check(ui.Visible && Tree.Paused && ui.HintLabel != null,"inventory opens with visible command feedback");
        ui.CombineButton.EmitSignal(Button.SignalName.Pressed);
        Check(ui.HintLabel.Text.Contains("STACK AGAIN"),"combine control explains how to use two herbs from one stack");
        if (Array.Exists(OS.GetCmdlineUserArgs(),arg => arg == "--capture-playtest") && DisplayServer.GetName() != "headless")
        {
            await ToSignal(Tree,SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false);
            Check(GetViewport().GetTexture().GetImage().SavePng("/tmp/foxtrot-inventory-crafting.png") == Error.Ok,
                "inventory controls render to a playtest capture");
        }
        ui.SlotGrid.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
        Check(Count("herb_green") == 0 && Count("herb_green_double") == 1 && ui.HintLabel.Text.Contains("SUCCESSFULLY"),
            "real inventory controls craft from a single stack and display success");
        Actor.HealthComponent.SetCurrentHealth(50);
        ui.UseButton.EmitSignal(Button.SignalName.Pressed);
        Check(Count("herb_green_double") == 0 && Mathf.IsEqualApprox(Actor.HealthComponent.CurrentHealth,100),
            "real use control consumes crafted remedy and heals player");
        ui.MissionItemList.EmitSignal(ItemList.SignalName.ItemSelected,0L);
        Check(!ui.UseButton.Visible && !ui.CombineButton.Visible && inventory.MissionItems.Count == 2,
            "keys and files display details without consumable actions");
        ui.CloseButton.EmitSignal(Button.SignalName.Pressed);
        Check(!ui.Visible && !Tree.Paused,"inventory close control resumes gameplay");
    }

    private async Task VerifyLegacyOverflow(SaveSystem saves)
    {
        var bandage = GD.Load<ItemData>("res://Resources/Items/Bandage.tres");
        saves.ActiveSaveData.InventoryItems.Clear();
        saves.ActiveSaveData.InventoryItems[bandage.ResourcePath] = bandage.MaxStack * InventoryComponent.MaxSlots + 2;
        Check(saves.Save(0,currentScenePath:Opening,currentAct:StoryAct.Act1_Arrival),"legacy oversized inventory fixture saves");
        saves.Load(0); await Level();
        Check(Count("bandage") == 48 && Actor.InventoryComponent.Slots.All(slot => slot.Count <= bandage.MaxStack),
            "legacy overstack distributes into bounded stacks");
        var overflow = Drops().Where(drop => drop.GetChildren().OfType<PersistentDropComponent>().Any()).ToList();
        Check(overflow.Count == 2,"legacy overflow becomes exactly two recoverable pickups");
        Check(!overflow[0].CollectByPlayer(Actor),"full healthy inventory leaves overflow on ground");
        Actor.InventoryComponent.ConsumeItem("bandage");
        Check(overflow[0].CollectByPlayer(Actor),"overflow remains collectible after making room");
        Check(saves.Save(0,Actor,Opening,StoryAct.Act1_Arrival),"migrated inventory checkpoint saves");
        saves.Load(0); await Level();
        Check(Count("bandage") == 48 && Drops().Count == 1,"reload preserves remaining overflow without duplicate deferred spawns");
    }

    private async Task VerifyEnemyLoot(SaveSystem saves,string enemyName,bool ammo)
    {
        var player = Actor;
        player.InventoryComponent.ClearInventory();
        var enemy = Tree.CurrentScene.GetNode<Zombie>(enemyName);
        var stats = (EnemyStats)enemy.Stats.Duplicate();
        _fixtures.Add(stats);
        stats.AmmoDropChance = ammo ? 1 : 0;
        stats.MedkitDropChance = ammo ? 0 : 1;
        enemy.Stats = stats;
        Vector2 position = enemy.GlobalPosition;
        string id = enemy.PersistentDropId + "::loot";
        int kills = StoryDirector.Instance.Stats.TotalKills;
        enemy.PerformDeath();
        enemy.PerformDeath();
        Check(enemy.IsDead && StoryDirector.Instance.Stats.TotalKills == kills + 1,"duplicate death calls count exactly one kill");
        Check(saves.ActiveSaveData.WorldDrops.ContainsKey(id),"enemy death records generated loot before deferred spawn");
        string itemPath = saves.ActiveSaveData.WorldDrops[id].ItemPath;
        Check(saves.Save(0,player,Opening,StoryAct.Act1_Arrival),"death-frame save includes pending generated loot");
        await Settle();
        Check(FindDrop(id) != null,"generated loot instantiates with stable collection state");
        saves.Load(0); await Level();
        Check(Tree.CurrentScene.GetNodeOrNull<Zombie>(enemyName) == null,"loot source stays defeated after reload");
        var pickup = FindDrop(id);
        Check(pickup != null && pickup.Item.ResourcePath == itemPath && pickup.GlobalPosition.DistanceTo(position) < 1,
            "earned loot restores with the same item and position");
        if (!ammo)
        {
            var bandage = GD.Load<ItemData>("res://Resources/Items/Bandage.tres");
            Actor.InventoryComponent.AddItem(bandage,48);
            Actor.HealthComponent.SetCurrentHealth(100);
            Check(!pickup.CollectByPlayer(Actor) && !saves.ActiveSaveData.WorldDrops[id].Collected,
                "failed healing-item collection does not mark persistent loot collected");
            Actor.InventoryComponent.ConsumeItem("bandage",bandage.MaxStack);
        }
        Check(pickup.CollectByPlayer(Actor) && !pickup.CollectByPlayer(Actor),"persistent loot collects only once");
        Check(saves.ActiveSaveData.WorldDrops[id].Collected,"loot collection commits its stable record");
        Check(saves.Save(0,Actor,Opening,StoryAct.Act1_Arrival),"collected loot checkpoint saves");
        saves.Load(0); await Level();
        Check(FindDrop(id) == null && saves.ActiveSaveData.WorldDrops[id].Collected,"collected generated loot cannot reappear after reload");
    }

    private static InventoryUI FindInventory(Node root)
    {
        if (root is InventoryUI ui) return ui;
        foreach (Node child in root.GetChildren())
            if (FindInventory(child) is { } found) return found;
        return null;
    }
    private int Count(string id) => Actor.InventoryComponent.Slots.Where(slot => slot.Item?.Id == id).Sum(slot => slot.Count);
    private List<Pickup> Drops() => Tree.GetNodesInGroup("pickups").OfType<Pickup>()
        .Where(pickup => pickup.GetChildren().OfType<PersistentDropComponent>().Any()).ToList();
    private Pickup FindDrop(string id) => Drops().Find(pickup => pickup.GetChildren().OfType<PersistentDropComponent>().Any(state => state.DropId == id));
    private async Task Level()
    {
        Check(Tree.ChangeSceneToFile(Opening) == Error.Ok,"opening fixture loads");
        for (int i = 0; i < 5; i++) await ToSignal(Tree,SceneTree.SignalName.ProcessFrame);
        Actor.IsInputLocked = true; Actor.IsInvulnerable = true;
        Actor.HealthComponent.SetCurrentHealth(100);
        await Settle();
        GC.Collect(); GC.WaitForPendingFinalizers();
    }
    private async Task Settle()
    {
        for (int i = 0; i < 2; i++)
        { await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame); await ToSignal(Tree,SceneTree.SignalName.ProcessFrame); }
    }
    private void Check(bool condition,string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("PASS " + message);
    }
}
