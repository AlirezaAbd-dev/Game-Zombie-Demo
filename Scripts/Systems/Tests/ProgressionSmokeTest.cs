using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.Entities.Components;

namespace TopDownDemo.Systems.Tests;

public partial class ProgressionSmokeTest : Node
{
    private const string Opening = "res://Scenes/Act1_Gate.tscn";
    private int _checks;
    private SceneTree Tree => GetTree();
    private Player Actor => Tree.GetFirstNodeInGroup("player") as Player;
    private InventoryComponent Inventory => Actor.InventoryComponent;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try
        {
            Tree.CurrentScene = null;
            var saves = SaveSystem.EnsureInstance(this);
            foreach (DifficultyLevel difficulty in Enum.GetValues<DifficultyLevel>())
            {
                saves.CreateNewGame(0,difficulty,saveImmediately:false);
                var data = saves.ActiveSaveData;
                Check(data.UnlockedWeaponIds.SetEquals(new[] { "handgun" })
                    && data.MagAmmo.GetValueOrDefault("handgun") + data.ReserveAmmo.GetValueOrDefault("ammo_handgun") == saves.StartingLoadout.Rounds(difficulty)
                    && data.MagAmmo.GetValueOrDefault("shotgun") == 0 && data.MagAmmo.GetValueOrDefault("rifle") == 0,
                    "authored starting weapon and ammunition budget on " + difficulty);
            }
            saves.CreateNewGame(0,DifficultyLevel.Survivor,saveImmediately:false);
            saves.ActiveSaveData.Flags.Add("intro_seen_act1_gate");
            await Level();
            Check(Inventory.CurrentWeapon.WeaponId == "handgun" && Inventory.UnlockedWeaponIds.Count == 1,
                "fresh campaign equips only the handgun");
            int denied = 0;
            Inventory.WeaponSelectionDenied += _ => denied++;
            Inventory.SelectWeapon(1); Inventory.SelectWeapon(2);
            Check(denied == 2 && Inventory.CurrentWeaponIndex == 0,"unacquired weapon slots reject selection with feedback");
            Inventory.NextWeapon(); Inventory.PreviousWeapon();
            Check(Inventory.CurrentWeaponIndex == 0,"cycling skips every locked slot");
            Inventory.TryConsumeAmmo(); Check(Inventory.StartReload(),"handgun can reload before other weapons are acquired");
            Inventory.SelectWeapon(1);
            Check(Inventory.IsReloading && Inventory.CurrentWeaponIndex == 0,"rejected weapon switch preserves the active reload");
            Inventory.UpdateReload(10);
            Check(Inventory.CurrentAmmo + Inventory.CurrentReserve == saves.StartingLoadout.Rounds(DifficultyLevel.Survivor) - 1,
                "rejected switch cannot create or discard ammunition");

            Inventory.ClearInventory(); Inventory.AddItem(GD.Load<ItemData>("res://Resources/Items/Bandage.tres"),48);
            Inventory.AddReserveAmmo("ammo_shotgun",7);
            var shotgun = Pickup("res://Resources/Items/ShotgunPickup.tres");
            Check(shotgun.CollectByPlayer(Actor) && Inventory.IsWeaponUnlocked("shotgun") && Inventory.Slots.Sum(slot => slot.Count) == 48,
                "weapon acquisition bypasses a full consumable inventory");
            Inventory.SelectWeapon(1);
            Check(Inventory.CurrentAmmo == 6 && Inventory.CurrentReserve == 7,"acquisition loads the weapon without resetting collected reserve ammunition");
            Inventory.TryConsumeAmmo();
            Check(!shotgun.CollectByPlayer(Actor) && Inventory.AddItem(shotgun.Item) && Inventory.CurrentAmmo == 5,
                "duplicate weapon collection cannot refill its magazine");
            Inventory.NextWeapon(); Check(Inventory.CurrentWeaponIndex == 0,"cycling skips the locked rifle and wraps to handgun");
            Inventory.PreviousWeapon(); Check(Inventory.CurrentWeaponIndex == 1,"reverse cycling selects the acquired shotgun");
            var rifle = Pickup("res://Resources/Items/RiflePickup.tres");
            Check(rifle.CollectByPlayer(Actor) && Inventory.IsWeaponUnlocked("rifle"),"rifle acquisition unlocks its weapon slot");
            Check(saves.Save(0,Actor,Opening,StoryAct.Act1_Arrival) && saves.Load(0),"acquired weapons checkpoint roundtrips");
            await Level();
            Check(Inventory.UnlockedWeaponIds.Count == 3 && Inventory.CurrentWeaponIndex == 1 && Inventory.CurrentAmmo == 5,
                "checkpoint restores acquired weapons, equipped slot and spent ammunition");

            saves.ActiveSaveData.UnlockedWeaponIds = new HashSet<string> { "handgun" };
            saves.ActiveSaveData.EquippedWeaponIndex = 2;
            Check(saves.Save(0,currentScenePath:Opening,currentAct:StoryAct.Act1_Arrival) && saves.Load(0),"locked equipped-slot fixture roundtrips");
            await Level();
            Check(Inventory.CurrentWeaponIndex == 0 && Inventory.UnlockedWeaponIds.Count == 1,
                "a stale equipped slot falls back to an acquired weapon");

            saves.ActiveSaveData.UnlockedWeaponIds = null;
            saves.ActiveSaveData.EquippedWeaponIndex = 2;
            saves.ActiveSaveData.MagAmmo["rifle"] = 3;
            saves.ActiveSaveData.ReserveAmmo["ammo_rifle"] = 8;
            saves.ActiveSaveData.WorldDrops["legacy_earned_ammo"] = new WorldDropData
            { ScenePath = Opening, ItemPath = "res://Resources/Items/AmmoPack.tres", X = 2600, Y = 2800 };
            Check(saves.Save(0,currentScenePath:Opening,currentAct:StoryAct.Act1_Arrival) && saves.Load(0),"prototype weapon and earned-loot fixture roundtrips");
            await Level();
            Check(Inventory.UnlockedWeaponIds.Count == 3 && Inventory.CurrentWeaponIndex == 2
                && Inventory.CurrentAmmo == 3 && Inventory.CurrentReserve == 8,
                "prototype save retains all original weapons and their ammunition");
            var legacyDrop = Tree.GetNodesInGroup("pickups").OfType<Pickup>()
                .Single(pickup => pickup.GetChildren().OfType<PersistentDropComponent>().Any(state => state.DropId == "legacy_earned_ammo"));
            Check(legacyDrop.RemainingAmmoRounds == 30,"smaller new ammo boxes do not reduce loot earned in an older save");
            Check(saves.Save(0,Actor,Opening,StoryAct.Act1_Arrival) && saves.Load(0),"migrated ownership and earned ammunition save");
            await Level();
            Check(Inventory.UnlockedWeaponIds.Count == 3 && saves.ActiveSaveData.WorldDrops["legacy_earned_ammo"].AmmoRoundsRemaining == 30,
                "migration persists historical weapon ownership and original loot quantity");

            saves.CreateNewGame(0,DifficultyLevel.Survivor,saveImmediately:false);
            saves.ActiveSaveData.Flags.Add("intro_seen_act1_gate"); await Level();
            Check(Inventory.UnlockedWeaponIds.Count == 1 && !Inventory.IsWeaponUnlocked("rifle"),"new game clears weapons acquired by the preceding session");
            saves.ActiveSaveData.Flags.Add("intro_seen_act2_checkpoint");
            await VerifyAuthoredReward("res://Scenes/Act2_Checkpoint.tscn",new Vector2(2450,690),"shotgun","armory-weapon");
            await VerifyAuthoredReward("res://Scenes/Act4_SubBasement.tscn",new Vector2(2350,1590),"rifle","vault-weapon");
            GD.Print($"PROGRESSION SMOKE PASS: {_checks} checks");
            Tree.CurrentScene.QueueFree(); await ToSignal(Tree.CreateTimer(0.3),SceneTreeTimer.SignalName.Timeout); Tree.Quit();
        }
        catch (Exception error) { GD.PushError("PROGRESSION SMOKE FAIL: " + error); Tree.Quit(1); }
    }
    private Pickup Pickup(string path)
    {
        var item = GD.Load<PackedScene>("res://Prefabs/Pickup.tscn").Instantiate<Pickup>();
        item.Item = GD.Load<ItemData>(path); item.Position = new Vector2(2800,2800); Tree.CurrentScene.AddChild(item); return item;
    }
    private async Task VerifyAuthoredReward(string scene,Vector2 position,string weapon,string capture)
    {
        await Level(scene);
        foreach (Node node in Tree.GetNodesInGroup("zombies")) node.SetPhysicsProcess(false);
        Actor.GlobalPosition = position; Actor.GlobalRotation = Mathf.Pi / 2;
        Actor.Camera.PositionSmoothingEnabled = false; Actor.Camera.Offset = Vector2.Zero; Actor.Camera.Zoom = Vector2.One;
        Actor.Camera.ResetSmoothing(); Actor.Camera.ForceUpdateScroll();
        for (int frame = 0; frame < 5; frame++)
        { await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame); await ToSignal(Tree,SceneTree.SignalName.ProcessFrame); }
        if (OS.GetCmdlineUserArgs().Contains("--capture-playtest"))
        {
            RenderingServer.ForceDraw(false);
            using var image = GetViewport().GetTexture().GetImage();
            Check(image.SavePng("/tmp/foxtrot-" + capture + ".png") == Error.Ok,"capture " + capture);
        }
        Check(Actor.InteractionComponent.TryTriggerInteraction(),"queued interaction reaches authored " + weapon + " reward");
        for (int frame = 0; frame < 5; frame++)
        { await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame); await ToSignal(Tree,SceneTree.SignalName.ProcessFrame); }
        Check(Inventory.IsWeaponUnlocked(weapon),"physics collection unlocks authored " + weapon + " reward");
    }
    private async Task Level(string scene = Opening)
    {
        Check(Tree.ChangeSceneToFile(scene) == Error.Ok,"progression fixture loads");
        for (int frame = 0; frame < 5; frame++) await ToSignal(Tree,SceneTree.SignalName.ProcessFrame);
        Actor.IsInputLocked = true; Actor.IsInvulnerable = true; Actor.SetProcess(false);
        GC.Collect(); GC.WaitForPendingFinalizers();
    }
    private void Check(bool condition,string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("PASS " + message);
    }
}
