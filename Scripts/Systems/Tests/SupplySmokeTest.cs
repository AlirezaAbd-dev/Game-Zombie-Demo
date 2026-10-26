using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.Entities.Components;

namespace TopDownDemo.Systems.Tests;

public partial class SupplySmokeTest : Node
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
            saves.CreateNewGame(0,DifficultyLevel.Survivor);
            saves.ActiveSaveData.UnlockedWeaponIds = new HashSet<string> { "handgun", "shotgun", "rifle" };
            saves.ActiveSaveData.Flags.Add("intro_seen_act1_gate");
            await Level();
            VerifyRemedies();
            VerifyAmmunitionCommands();
            await VerifyPickupReach();
            await VerifyAuthoredBox(saves);
            await VerifyGeneratedBox(saves);
            await VerifyLegacyAmmoOverflow(saves);
            Inventory.ClearInventory();
            Inventory.AddItem(GD.Load<ItemData>("res://Resources/Items/Bandage.tres"));
            Actor.HealthComponent.SetCurrentHealth(0);
            Check(!Inventory.UseSlot(0,Actor) && Inventory.HasItem("bandage"),"dead player cannot consume remedies without an effect");
            GD.Print($"SUPPLY SMOKE PASS: {_checks} checks");
            Tree.CurrentScene.QueueFree();
            await ToSignal(Tree.CreateTimer(0.25),SceneTreeTimer.SignalName.Timeout);
            Tree.Quit();
        }
        catch (Exception error) { GD.PushError("SUPPLY SMOKE FAIL: " + error); Tree.Quit(1); }
    }

    private void VerifyRemedies()
    {
        var blue = GD.Load<ItemData>("res://Resources/Items/Herb_Blue.tres");
        var mixed = GD.Load<ItemData>("res://Resources/Items/Herb_Mixed.tres");
        var bandage = GD.Load<ItemData>("res://Resources/Items/Bandage.tres");
        Inventory.ClearInventory(); Inventory.AddItem(blue,2);
        Actor.HealthComponent.SetCurrentHealth(50); Actor.StaminaComponent.SetCurrentStamina(20);
        Check(Inventory.UseSlot(0,Actor) && Actor.HealthComponent.CurrentHealth == 50 && Actor.StaminaComponent.CurrentStamina == 70,
            "blue herb restores exactly 50 stamina without healing wounds");
        Actor.StaminaComponent.SetCurrentStamina(100);
        Check(!Inventory.UseSlot(0,Actor) && Inventory.Slots[0].Count == 1,"blue herb is retained when stamina is full, even if wounded");
        Check(!Inventory.QuickHeal(Actor) && Inventory.HasItem(blue.Id),"quick heal skips stamina-only herbs");
        Inventory.AddItem(bandage);
        Check(Inventory.QuickHeal(Actor) && Actor.HealthComponent.CurrentHealth == 50 + bandage.Value && Inventory.HasItem(blue.Id),
            "quick heal selects a health remedy while preserving stamina supplies");
        Inventory.ClearInventory(); Inventory.AddItem(mixed,2);
        Actor.HealthComponent.SetCurrentHealth(50); Actor.StaminaComponent.SetCurrentStamina(10);
        Check(Inventory.UseSlot(0,Actor) && Actor.HealthComponent.CurrentHealth == 100 && Actor.StaminaComponent.CurrentStamina == 100,
            "mixed herb applies its authored health and stamina effects together");
        Check(!Inventory.UseSlot(0,Actor) && Inventory.Slots[0].Count == 1,"full health and stamina preserve unused mixed herbs");
        Actor.StaminaComponent.SetCurrentStamina(70);
        Check(Inventory.UseSlot(0,Actor) && Actor.StaminaComponent.CurrentStamina == 100,"mixed herb remains usable for stamina at full health");
        Inventory.ClearInventory(); Inventory.AddItem(bandage,48);
        Actor.HealthComponent.SetCurrentHealth(50); Actor.StaminaComponent.SetCurrentStamina(100);
        var pickup = NewPickup(blue);
        Check(!pickup.CollectByPlayer(Actor) && !pickup.IsQueuedForDeletion(),"full inventory cannot auto-consume a useless stamina herb");
        Actor.StaminaComponent.SetCurrentStamina(20);
        Check(pickup.CollectByPlayer(Actor) && Actor.StaminaComponent.CurrentStamina == 70 && Actor.HealthComponent.CurrentHealth == 50,
            "ground-use fallback applies the same precise stamina effect as inventory use");
        Check(!Actor.StaminaComponent.TryConsume(-10) && !Actor.StaminaComponent.TryConsume(float.NaN)
            && Actor.StaminaComponent.Restore(-10) == 0 && Actor.StaminaComponent.CurrentStamina == 70,
            "invalid stamina commands cannot create or remove energy");
        Actor.HealthComponent.SetCurrentHealth(100);
    }

    private void VerifyAmmunitionCommands()
    {
        var weapon = Inventory.Weapons[0];
        var ammo = GD.Load<ItemData>("res://Resources/Items/AmmoPack.tres");
        SetReserve(weapon,weapon.MaxReserve - 3);
        Check(!Inventory.AddItem(ammo) && Inventory.CurrentReserve == weapon.MaxReserve - 3,
            "atomic box addition preserves rounds when the whole box cannot fit");
        Check(Inventory.AddReserveAmmo(weapon.AmmoId.ToUpperInvariant(),int.MaxValue) == 3 && Inventory.CurrentReserve == weapon.MaxReserve,
            "loose-round transfer reports accepted quantity without integer overflow");
        Check(Inventory.AddReserveAmmo("unknown_ammunition",10) == 0 && !Inventory.GetReserveAmmo().ContainsKey("unknown_ammunition"),
            "unknown ammunition cannot disappear into an unusable reserve");
    }

    private async Task VerifyAuthoredBox(SaveSystem saves)
    {
        var weapon = Inventory.Weapons[0];
        SetReserve(weapon,weapon.MaxReserve - 3);
        var box = Tree.CurrentScene.GetNode<Pickup>("ResponseAmmo");
        string key = Opening + "::ResponseAmmo";
        int full = box.RemainingAmmoRounds;
        Check(box.CollectByPlayer(Actor) && box.RemainingAmmoRounds == full - 3 && !box.IsQueuedForDeletion(),
            "authored box transfers only available reserve space and leaves the remainder");
        Check(saves.ActiveSaveData.PickupAmmoRemaining[key] == full - 3 && !saves.ActiveSaveData.CompletedWorldObjects.Contains(key),
            "partial transfer records remaining rounds without completing the pickup");
        Check(!box.CollectByPlayer(Actor),"full reserve cannot take the remaining box twice");
        Check(saves.Save(0,Actor,Opening,StoryAct.Act1_Arrival) && saves.Load(0),"partial authored ammunition checkpoint roundtrips");
        await Level(); box = Tree.CurrentScene.GetNode<Pickup>("ResponseAmmo");
        Check(box.RemainingAmmoRounds == full - 3 && Inventory.CurrentReserve == weapon.MaxReserve,
            "reload conserves both carried rounds and authored box remainder");
        Check(box.GetChildren().OfType<Label>().Any(label => label.Text == $"{full - 3} ROUNDS LEFT"),
            "restored partial box displays its remaining quantity");
        await Capture("partial-ammo-box");
        SetReserve(weapon,weapon.MaxReserve - 5);
        Check(box.CollectByPlayer(Actor) && saves.ActiveSaveData.PickupAmmoRemaining[key] == full - 8,
            "subsequent transfer updates the current save draft after checkpoint replacement");
        Check(saves.Save(0,Actor,Opening,StoryAct.Act1_Arrival) && saves.Load(0),"second partial box checkpoint roundtrips");
        await Level(); box = Tree.CurrentScene.GetNode<Pickup>("ResponseAmmo");
        Check(box.RemainingAmmoRounds == full - 8,"multiple reloads cannot refill a partially used box");
        SetReserve(weapon,0);
        Check(box.CollectByPlayer(Actor) && Inventory.CurrentReserve == full - 8 && box.IsQueuedForDeletion()
            && saves.ActiveSaveData.CompletedWorldObjects.Contains(key),"last transfer empties box and completes its stable world state");
        Check(saves.Save(0,Actor,Opening,StoryAct.Act1_Arrival) && saves.Load(0),"empty authored box checkpoint roundtrips");
        await Level();
        Check(Tree.CurrentScene.GetNodeOrNull<Pickup>("ResponseAmmo") == null,"empty authored box stays absent after reload");
    }

    private async Task VerifyPickupReach()
    {
        var key = Tree.CurrentScene.GetNode<Pickup>("MaintenanceCredential");
        var ammo = Tree.CurrentScene.GetNode<Pickup>("MaintenanceAmmo");
        float keyRadius = ((CircleShape2D)key.GetChildren().OfType<CollisionShape2D>().First().Shape).Radius;
        float ammoRadius = ((CircleShape2D)ammo.GetChildren().OfType<CollisionShape2D>().First().Shape).Radius;
        Check(keyRadius == 80 && ammoRadius == 36,"mission pickup range cannot alter shared ammo and healing collision shapes");
        Inventory.ClearInventory();
        var wall = new StaticBody2D { Position = new Vector2(1510,2600), CollisionLayer = 1 };
        wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(4,140) } });
        Tree.CurrentScene.AddChild(wall);
        var box = NewPickup(GD.Load<ItemData>("res://Resources/Items/Bandage.tres"));
        box.GlobalPosition = new Vector2(1535,2600);
        Actor.GlobalPosition = new Vector2(1485,2600);
        for (int frame = 0; frame < 5; frame++)
        { await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame); await ToSignal(Tree,SceneTree.SignalName.ProcessFrame); }
        Check(!box.IsQueuedForDeletion() && !Inventory.HasItem("bandage"),"automatic pickup cannot reach through a thin wall");
        Check(Actor.InteractionComponent.TryTriggerInteraction(),"manual pickup enters the physics command queue");
        for (int frame = 0; frame < 3; frame++)
        { await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame); await ToSignal(Tree,SceneTree.SignalName.ProcessFrame); }
        Check(!box.IsQueuedForDeletion() && !Inventory.HasItem("bandage"),"manual pickup cannot bypass the same wall with interaction input");
        wall.QueueFree();
        for (int frame = 0; frame < 5; frame++)
        { await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame); await ToSignal(Tree,SceneTree.SignalName.ProcessFrame); }
        Check(Inventory.HasItem("bandage"),"pickup becomes reachable when cover is removed without leaving the overlap");
        box = NewPickup(GD.Load<ItemData>("res://Resources/Items/Bandage.tres"));
        box.GlobalPosition = new Vector2(1580,2600);
        for (int frame = 0; frame < 3; frame++)
        { await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame); await ToSignal(Tree,SceneTree.SignalName.ProcessFrame); }
        Check(Inventory.Slots.Sum(slot => slot.Item?.Id == "bandage" ? slot.Count : 0) == 1,
            "a reachable item beyond automatic collection range stays on the ground");
        Check(Actor.InteractionComponent.TryTriggerInteraction(),"manual interaction requests the reachable item");
        for (int frame = 0; frame < 3; frame++)
        { await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame); await ToSignal(Tree,SceneTree.SignalName.ProcessFrame); }
        Check(Inventory.Slots.Sum(slot => slot.Item?.Id == "bandage" ? slot.Count : 0) == 2,
            "physics pickup request collects an unobstructed item within manual reach");
        Actor.GlobalPosition = new Vector2(500,2800);
    }

    private async Task VerifyGeneratedBox(SaveSystem saves)
    {
        var ammo = GD.Load<ItemData>("res://Resources/Items/AmmoShotgun.tres");
        var weapon = Inventory.Weapons[1];
        const string id = "supply_fixture::loot";
        CampaignLootDirector.Instance.SpawnEnemyDrop("supply_fixture",ammo,new Vector2(2600,2800));
        await Frames();
        SetReserve(weapon,weapon.MaxReserve - 1);
        var box = FindDrop(id);
        Check(box.CollectByPlayer(Actor) && box.RemainingAmmoRounds == (int)ammo.Value - 1
            && saves.ActiveSaveData.WorldDrops[id].AmmoRoundsRemaining == (int)ammo.Value - 1
            && !saves.ActiveSaveData.WorldDrops[id].Collected,"generated loot retains its partially transferred ammunition");
        Check(saves.Save(0,Actor,Opening,StoryAct.Act1_Arrival) && saves.Load(0),"partial generated loot checkpoint roundtrips");
        await Level(); box = FindDrop(id);
        Check(box.RemainingAmmoRounds == (int)ammo.Value - 1 && Inventory.CurrentReserve == weapon.MaxReserve,
            "generated box restores remaining rounds and equipped reserve");
        SetReserve(weapon,0);
        Check(box.CollectByPlayer(Actor) && Inventory.CurrentReserve == (int)ammo.Value - 1
            && saves.ActiveSaveData.WorldDrops[id].Collected,"final generated loot transfer marks the record collected");
        Check(saves.Save(0,Actor,Opening,StoryAct.Act1_Arrival) && saves.Load(0),"empty generated box checkpoint roundtrips");
        await Level(); Check(FindDrop(id) == null,"empty generated box stays absent after reload");
    }

    private void SetReserve(WeaponData weapon,int reserve)
    {
        int index = Inventory.Weapons.IndexOf(weapon);
        Inventory.SetAmmoState(new Dictionary<string,int> { [weapon.WeaponId] = weapon.MagCapacity },
            new Dictionary<string,int> { [weapon.AmmoId] = reserve },index);
    }

    private async Task VerifyLegacyAmmoOverflow(SaveSystem saves)
    {
        var weapon = Inventory.Weapons[2];
        int savedMagazine = weapon.MagCapacity + 7, savedReserve = weapon.MaxReserve + 25;
        saves.ActiveSaveData.MagAmmo[weapon.WeaponId] = savedMagazine;
        saves.ActiveSaveData.ReserveAmmo[weapon.AmmoId] = savedReserve;
        Check(saves.Save(0,currentScenePath:Opening,currentAct:StoryAct.Act1_Arrival) && saves.Load(0),
            "legacy over-capacity ammunition fixture saves and reloads");
        await Level();
        string id = Opening + "::restore-ammo-overflow::" + weapon.AmmoId + "::0::loot";
        var box = FindDrop(id);
        Check(box != null && box.RemainingAmmoRounds == 32
            && Inventory.GetMagAmmo()[weapon.WeaponId] + Inventory.GetReserveAmmo()[weapon.AmmoId] + box.RemainingAmmoRounds == savedMagazine + savedReserve,
            "legacy magazine and reserve overflow becomes recoverable loot without destroying rounds");
        Check(!box.CollectByPlayer(Actor),"full restored reserve cannot consume legacy overflow");
        SetReserve(weapon,weapon.MaxReserve - 10);
        Check(box.CollectByPlayer(Actor) && box.RemainingAmmoRounds == 22,"legacy overflow supports precise partial transfers");
        Check(saves.Save(0,Actor,Opening,StoryAct.Act1_Arrival) && saves.Load(0),"migrated ammunition checkpoint roundtrips");
        await Level(); box = FindDrop(id);
        Check(box != null && box.RemainingAmmoRounds == 22
            && Tree.GetNodesInGroup("pickups").OfType<Pickup>().Count(pickup => pickup.GetChildren().OfType<PersistentDropComponent>().Any(state => state.DropId == id)) == 1,
            "migrated overflow retains its remaining rounds without duplicate spawns");
        SetReserve(weapon,0);
        Check(box.CollectByPlayer(Actor) && Inventory.CurrentReserve == 22,"legacy overflow can be fully recovered after making room");
        Check(saves.Save(0,Actor,Opening,StoryAct.Act1_Arrival) && saves.Load(0),"recovered legacy overflow checkpoint roundtrips");
        await Level(); Check(FindDrop(id) == null,"recovered legacy overflow cannot reappear after reload");
    }
    private Pickup NewPickup(ItemData item)
    {
        var pickup = GD.Load<PackedScene>("res://Prefabs/Pickup.tscn").Instantiate<Pickup>();
        pickup.Item = item; pickup.Position = new Vector2(2800,2800); Tree.CurrentScene.AddChild(pickup); return pickup;
    }
    private Pickup FindDrop(string id) => Tree.GetNodesInGroup("pickups").OfType<Pickup>()
        .FirstOrDefault(pickup => pickup.GetChildren().OfType<PersistentDropComponent>().Any(state => state.DropId == id));
    private async Task Level()
    {
        Check(Tree.ChangeSceneToFile(Opening) == Error.Ok,"supply fixture loads");
        await Frames(); Actor.IsInputLocked = true; Actor.IsInvulnerable = true; Actor.SetProcess(false);
        GC.Collect(); GC.WaitForPendingFinalizers();
    }
    private async Task Frames()
    { for (int frame = 0; frame < 5; frame++) await ToSignal(Tree,SceneTree.SignalName.ProcessFrame); }
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture-playtest")) return;
        RenderingServer.ForceDraw(false);
        using var screenshot = GetViewport().GetTexture().GetImage();
        Check(screenshot.SavePng($"/tmp/foxtrot-{name}.png") == Error.Ok,"capture " + name);
    }
    private void Check(bool condition,string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("PASS " + message);
    }
}
