using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;

namespace TopDownDemo.Systems.Tests;

public partial class CombatSmokeTest : Node
{
    private const string Opening = "res://Scenes/Act1_Gate.tscn";
    private int _checks;
    private SceneTree Tree => GetTree();
    private Player Actor => Tree.GetFirstNodeInGroup("player") as Player;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try
        {
            Tree.CurrentScene = null;
            GD.Print("COMBAT INPUT display: " + DisplayServer.GetName());
            var saves = SaveSystem.EnsureInstance(this);
            saves.CreateNewGame(0,DifficultyLevel.Survivor);
            saves.ActiveSaveData.UnlockedWeaponIds = new HashSet<string> { "handgun", "shotgun", "rifle" };
            saves.ActiveSaveData.Flags.Add("intro_seen_act1_gate");
            await Level(Opening);
            Actor.SetProcess(false); Actor.SetPhysicsProcess(false);
            foreach (Node node in Tree.GetNodesInGroup("zombies")) node.QueueFree();
            await Frames();
            await VerifyAcousticReach();
            await VerifyBarrelSweep();
            await VerifyLiveReloadFight();
            await VerifyMeleeThreats();
            await VerifyStatistics(saves);
            GD.Print($"COMBAT SMOKE PASS: {_checks} checks");
            Tree.CurrentScene.QueueFree();
            await ToSignal(Tree.CreateTimer(0.3),SceneTreeTimer.SignalName.Timeout);
            Tree.Quit();
        }
        catch (Exception error)
        {
            Input.ActionRelease("shoot"); Input.ActionRelease("aim");
            GD.PushError("COMBAT SMOKE FAIL: " + error); Tree.Quit(1);
        }
    }

    private async Task VerifyBarrelSweep()
    {
        for (int index = 0; index < 3; index++)
        {
            var inventory = Actor.InventoryComponent;
            var weapon = inventory.Weapons[index];
            Equip(index);
            Actor.GlobalPosition = new Vector2(500,2600); Actor.GlobalRotation = 0;
            var target = SpawnWalker(new Vector2(650,2600),true);
            target.HealthComponent.Initialize(1000);
            var wall = new StaticBody2D { Position = new Vector2(535,2600), CollisionLayer = 1 };
            wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(4,120) } });
            Tree.CurrentScene.AddChild(wall);
            await PhysicsFrames();
            int shots = StoryDirector.Instance.Stats.ShotsFired;
            int hits = StoryDirector.Instance.Stats.ShotsHit;
            Actor.Shoot(); await PhysicsFrames();
            Check(target.HealthComponent.CurrentHealth == 1000,$"{weapon.WeaponId} cannot fire through cover between body and muzzle");
            Check(inventory.CurrentAmmo == weapon.MagCapacity - 1 && StoryDirector.Instance.Stats.ShotsFired == shots + 1
                && StoryDirector.Instance.Stats.ShotsHit == hits,$"{weapon.WeaponId} blocked shot consumes one round and records a miss");
            wall.QueueFree(); target.QueueFree(); await PhysicsFrames();
            target = SpawnWalker(new Vector2(550,2600),true);
            target.HealthComponent.Initialize(1000); await PhysicsFrames();
            hits = StoryDirector.Instance.Stats.ShotsHit;
            Actor.Shoot(); await PhysicsFrames();
            Check(target.HealthComponent.CurrentHealth < 1000,$"{weapon.WeaponId} hits a close target before the barrel tip");
            Check(StoryDirector.Instance.Stats.ShotsHit == hits + 1,$"{weapon.WeaponId} records one accuracy hit per trigger pull");
            Check(Actor.HealthComponent.CurrentHealth == 100,$"{weapon.WeaponId} first sweep excludes the shooter");
            target.QueueFree(); await PhysicsFrames();
        }
    }

    private async Task VerifyAcousticReach()
    {
        Actor.GlobalPosition = new Vector2(500,2600); Actor.GlobalRotation = -Mathf.Pi / 2;
        var listener = SpawnWalker(new Vector2(1100,2600),true);
        await PhysicsFrames();
        Equip(0); Actor.Shoot();
        Check(!listener.HasTargetAcquired,"handgun shot does not alert an infected beyond its acoustic range");
        Equip(1); Actor.Shoot();
        Check(listener.HasTargetAcquired,"shotgun report reaches the wider authored acoustic range");
        listener.QueueFree(); await PhysicsFrames();
        listener = SpawnWalker(new Vector2(1100,2600),true);
        await PhysicsFrames(); Equip(2); Actor.Shoot();
        Check(listener.HasTargetAcquired,"rifle report uses its authored acoustic range rather than the handgun fallback");
        listener.QueueFree(); await PhysicsFrames();
    }

    private async Task VerifyLiveReloadFight()
    {
        var inventory = Actor.InventoryComponent;
        var weapon = inventory.Weapons[0];
        inventory.SetAmmoState(new Dictionary<string,int> { [weapon.WeaponId] = 0 },
            new Dictionary<string,int> { [weapon.AmmoId] = 2 },0);
        PlayerSettings.Apply(new PlayerSettingsData { CameraShake = 0, CameraLookAhead = false });
        Actor.GlobalPosition = new Vector2(500,2600);
        Actor.MovementComponent.ResetMotion();
        Actor.IsInputLocked = false; Actor.IsInvulnerable = false;
        Actor.SetProcess(true); Actor.SetPhysicsProcess(true);
        // Let the preceding rifle's real fire cooldown elapse before this encounter.
        await ToSignal(Tree.CreateTimer(1.0),SceneTreeTimer.SignalName.Timeout);
        var target = SpawnWalker(new Vector2(750,2600),false);
        target.GlobalRotation = Mathf.Pi; target.AlertToPosition(Actor.GlobalPosition);
        bool dead = false; target.Died += () => dead = true;
        var startingPosition = target.GlobalPosition;
        int shots = StoryDirector.Instance.Stats.ShotsFired;
        int kills = StoryDirector.Instance.Stats.TotalKills;
        Input.ActionPress("aim"); Input.ActionPress("shoot");
        for (int frame = 0; frame < 240 && !dead && !Actor.IsDead; frame++)
        {
            if (IsInstanceValid(target)) AimAt(target.GlobalPosition);
            await PhysicsFrames(1);
            if (frame == 12)
            {
                Check(inventory.IsReloading && inventory.CurrentAmmo == 0,"holding fire on an empty gun starts deliberate reload");
                Check(target.GlobalPosition.DistanceTo(Actor.GlobalPosition) < startingPosition.DistanceTo(Actor.GlobalPosition) - 5,
                    "live infected continues approaching during reload");
                Check(!Actor.IsInvulnerable && !Actor.IsInputLocked,"reload fight runs with a vulnerable player and normal input");
                await Capture("live-reload-fight");
            }
        }
        Input.ActionRelease("shoot"); Input.ActionRelease("aim");
        Check(dead && !Actor.IsDead,$"aim and fire input defeats an approaching walker after reloading (dead={dead}, playerHP={Actor.HealthComponent.CurrentHealth}, targetHP={(IsInstanceValid(target) ? target.HealthComponent.CurrentHealth : 0)}, shots={StoryDirector.Instance.Stats.ShotsFired - shots}, ammo={inventory.CurrentAmmo}/{inventory.CurrentReserve}, rotation={Actor.GlobalRotation}, player={Actor.GlobalPosition}, mouse={Actor.GetGlobalMousePosition()}, target={(IsInstanceValid(target) ? target.GlobalPosition : Vector2.Zero)})");
        Check(StoryDirector.Instance.Stats.ShotsFired == shots + 2 && inventory.CurrentAmmo == 0 && inventory.CurrentReserve == 0,
            "live fight spends exactly its two-round ammunition budget");
        Check(StoryDirector.Instance.Stats.TotalKills == kills + 1,"live projectile kill is counted once");
        Check(StoryDirector.Instance.Stats.ShotsHit <= StoryDirector.Instance.Stats.ShotsFired
            && StoryDirector.Instance.Stats.AccuracyPercentage <= 100,"shotgun pellets cannot inflate mission accuracy beyond 100 percent");
        await PhysicsFrames();
        Actor.SetProcess(false); Actor.SetPhysicsProcess(false);
    }

    private async Task VerifyStatistics(SaveSystem saves)
    {
        var stats = StoryDirector.Instance.Stats;
        stats.DocumentsFound = 2;
        int shots = stats.ShotsFired, hits = stats.ShotsHit, kills = stats.TotalKills;
        Check(saves.Save(0,Actor,Opening,StoryAct.Act1_Arrival),"combat statistics checkpoint saves");
        StoryDirector.Instance.RecordShotFired(); StoryDirector.Instance.RecordKill();
        Check(saves.Load(0),"combat checkpoint reloads"); await Level(Opening);
        stats = StoryDirector.Instance.Stats;
        Check(stats.ShotsFired == shots && stats.ShotsHit == hits && stats.TotalKills == kills && stats.DocumentsFound == 2,
            "checkpoint restores mission counters and rolls back unsaved combat");
        const string bunker = "res://Scenes/Act2_Checkpoint.tscn";
        saves.ActiveSaveData.Flags.Add("intro_seen_act2_checkpoint");
        Check(saves.Save(0,Actor,bunker,StoryAct.Act2_Bunker),"combat counters save with chapter departure");
        await Level(bunker);
        stats = StoryDirector.Instance.Stats;
        Check(stats.ShotsFired == shots && stats.ShotsHit == hits && stats.TotalKills == kills && stats.DocumentsFound == 2,
            "chapter transition preserves campaign-wide mission counters");
        GC.Collect(); GC.WaitForPendingFinalizers();
    }

    private async Task VerifyMeleeThreats()
    {
        Actor.IsInputLocked = true; Actor.IsInvulnerable = false;
        Actor.SetProcess(true); Actor.SetPhysicsProcess(true);
        foreach (string type in new[] { "Walker", "Runner", "Brute" })
        foreach (float facing in new[] { 0f, Mathf.Pi })
        {
            Actor.GlobalPosition = new Vector2(500,2600); Actor.GlobalRotation = facing;
            Actor.MovementComponent.ResetMotion(); Actor.HealthComponent.SetCurrentHealth(100);
            var target = SpawnWalker(new Vector2(640,2600),false);
            target.ApplyStats(GD.Load<EnemyStats>($"res://Resources/Enemies/Zombie_{type}.tres"));
            target.GlobalRotation = Mathf.Pi; target.AlertToPosition(Actor.GlobalPosition);
            for (int frame = 0; frame < 240 && Actor.HealthComponent.CurrentHealth == 100; frame++) await PhysicsFrames(1);
            Check(Actor.HealthComponent.CurrentHealth < 100,
                $"live {type} enters melee at physical reach and damages the player (facing={facing}, state={target.StateMachine.CurrentStateName}, distance={target.GlobalPosition.DistanceTo(Actor.GlobalPosition)}, playerHP={Actor.HealthComponent.CurrentHealth})");
            Check(Mathf.IsEqualApprox(Actor.HealthComponent.CurrentHealth,100 - target.Stats.AttackDamage),
                $"live {type} applies one authored hit after its attack windup");
            target.QueueFree(); await PhysicsFrames();
        }
        Actor.GlobalPosition = new Vector2(500,2600); Actor.GlobalRotation = 0;
        Actor.MovementComponent.ResetMotion(); Actor.HealthComponent.SetCurrentHealth(100);
        Actor.SetPhysicsProcess(false);
        var blocked = SpawnWalker(new Vector2(550,2600),true); blocked.GlobalRotation = Mathf.Pi;
        var wall = new StaticBody2D { Position = new Vector2(535,2600), CollisionLayer = 1 };
        wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(4,140) } });
        Tree.CurrentScene.AddChild(wall); await PhysicsFrames();
        blocked.PerformAttack();
        Check(Actor.HealthComponent.CurrentHealth == 100,"melee contact area cannot damage through a wall");
        wall.QueueFree(); await PhysicsFrames(); blocked.PerformAttack();
        Check(Actor.HealthComponent.CurrentHealth == 100 - blocked.Stats.AttackDamage,"same melee contact damages after cover is removed");
        blocked.QueueFree(); await PhysicsFrames();
        Actor.SetProcess(false);
    }

    private void Equip(int index)
    {
        var weapon = Actor.InventoryComponent.Weapons[index];
        Actor.InventoryComponent.SetAmmoState(new Dictionary<string,int> { [weapon.WeaponId] = weapon.MagCapacity },
            new Dictionary<string,int>(),index);
    }
    private Zombie SpawnWalker(Vector2 position,bool frozen)
    {
        var target = GD.Load<PackedScene>("res://Prefabs/Zombie.tscn").Instantiate<Zombie>();
        target.Stats = GD.Load<EnemyStats>("res://Resources/Enemies/Zombie_Walker.tres");
        target.Position = position; Tree.CurrentScene.AddChild(target);
        if (frozen) { target.SetProcess(false); target.SetPhysicsProcess(false); }
        return target;
    }
    private void AimAt(Vector2 point)
    {
        // ParseInputEvent consumes coordinates inside the native window, including
        // viewport stretch but excluding the window's position on the desktop.
        Vector2 viewportPosition = Actor.GetCanvasTransform() * point;
        if (DisplayServer.GetName() != "headless") GetViewport().WarpMouse(viewportPosition);
        Vector2 position = GetViewport().GetFinalTransform() * viewportPosition;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = position, GlobalPosition = position });
    }
    private async Task Level(string path)
    {
        Check(Tree.ChangeSceneToFile(path) == Error.Ok,"combat fixture loads: " + path);
        await Frames();
    }
    private async Task Frames()
    { for (int frame = 0; frame < 5; frame++) await ToSignal(Tree,SceneTree.SignalName.ProcessFrame); }
    private async Task PhysicsFrames(int count = 5)
    {
        for (int frame = 0; frame < count; frame++)
        { await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame); await ToSignal(Tree,SceneTree.SignalName.ProcessFrame); }
    }
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
