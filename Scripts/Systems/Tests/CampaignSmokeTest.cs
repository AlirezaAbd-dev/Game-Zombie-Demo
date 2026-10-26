using System;
using System.Threading.Tasks;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.Entities.Components;
using TopDownDemo.Entities.States;
using TopDownDemo.UI;

namespace TopDownDemo.Systems.Tests;

/// <summary>Run with an isolated XDG_DATA_HOME; never touches normal player saves.</summary>
public partial class CampaignSmokeTest : Node
{
    private int _checks;
    private const string Opening = "res://Scenes/Act1_Gate.tscn";
    private SceneTree Tree => GetTree();

    public override void _Ready() => Callable.From(Run).CallDeferred();

    private async void Run()
    {
        try
        {
            Tree.CurrentScene = null;
            var saves = SaveSystem.EnsureInstance(this);
            saves.CreateNewGame(0, DifficultyLevel.Survivor);
            saves.ActiveSaveData.Flags.Add("intro_seen_act1_gate");
            if (Array.Exists(OS.GetCmdlineUserArgs(),arg => arg == "--landing-check"))
            {
                saves.ActiveSaveData.Flags.Add(StoryFlags.UNIT9_DEFEATED);
                saves.ActiveSaveData.Flags.Add("evac_beacon_online");
                await Level("res://Scenes/Act5_Arena.tscn");
                var actor = Player(); actor.IsInvulnerable = true; actor.IsInputLocked = true;
                actor.GlobalPosition = new Vector2(1500,350);
                foreach (Node node in Tree.GetNodesInGroup("zombies")) if (node is Zombie z) z.SetPhysicsProcess(false);
                var flight = Scene().GetNode<HelicopterExtractionController>("Extraction");
                await ToSignal(Tree.CreateTimer(flight.Data.ApproachSeconds + 0.2),SceneTreeTimer.SignalName.Timeout);
                Check(flight.State == ExtractionState.Holding,"landing check holds for infected");
                actor.GlobalPosition = new Vector2(1000,350);
                Scene().GetNode<Zombie>("RoofRunner").TakeDamage(999,Vector2.Zero);
                await SettlePhysics();
                Check(flight.State == ExtractionState.Holding,$"landing check holds for player: {flight.State}, {actor.GlobalPosition}");
                actor.GlobalPosition = new Vector2(1500,350); await SettlePhysics();
                Check(flight.State == ExtractionState.Landed,"landing check clears after player leaves");
                GD.Print($"LANDING SMOKE PASS: {_checks} checks");
                Tree.CurrentScene.QueueFree();
                await ToSignal(Tree.CreateTimer(0.2),SceneTreeTimer.SignalName.Timeout);
                Tree.Quit(); return;
            }
            if (Array.Exists(OS.GetCmdlineUserArgs(),arg => arg == "--extraction-only"))
            {
                await Level("res://Scenes/Act5_Arena.tscn");
                await VerifyExtraction(saves);
                GD.Print($"EXTRACTION SMOKE PASS: {_checks} checks");
                Tree.CurrentScene.QueueFree();
                await ToSignal(Tree.CreateTimer(0.2),SceneTreeTimer.SignalName.Timeout);
                Tree.Quit();
                return;
            }
            await Level(Opening);
            var player = Player();
            player.IsInputLocked = true;
            Check(CampaignActDirector.Instance.ActiveObjective?.CompletionFlag == "gate_log_read", "opening objective replaces old demo");
            Check(StoryDirector.Instance.IsCampaign, "campaign mode enabled");
            Check(saves.GetParent() == Tree.Root, "save service belongs to session root");
            var gate = Scene().GetNode<BlastDoor>("NorthGateLock");
            gate.OpenDoor();
            Check(!gate.IsOpen, "unpowered gate cannot be bypassed");
            Check(HasRoute(new Vector2(500, 2800), new Vector2(300, 1600), player), "approach connects to gatehouse");
            Check(HasRoute(new Vector2(300, 1600), new Vector2(2700, 1600), player), "gatehouse connects to maintenance");
            var socket = Scene().GetNode<CampaignInteraction>("FuseSocket");
            Check(!socket.Interactable.TryInteract(player), "missing fuse blocks installation");
            Check(!StoryDirector.Instance.HasFlag("auxiliary_fuse_installed"), "failed interaction does not set completion");
            Scene().GetNode<CampaignInteraction>("GateLog").Interactable.TryInteract(player);
            Check(CampaignActDirector.Instance.ActiveObjective?.CompletionFlag == "service_key_acquired", "objective advances after investigation");
            player.InventoryComponent.ClearInventory();
            for (int i = 0; i < InventoryComponent.MaxSlots; i++)
                player.InventoryComponent.AddItem(new ItemData { Id = "test_" + i, MaxStack = 1 });
            var credential = Scene().GetNode<Pickup>("MaintenanceCredential");
            Check(credential.CollectByPlayer(player), "mission credential collects with full consumable inventory");
            Check(!credential.CollectByPlayer(player), "same pickup cannot be collected twice in one frame");
            Check(Scene().GetNode<Pickup>("SpareFuse").CollectByPlayer(player), "essential fuse bypasses inventory capacity");
            Scene().GetNode<BlastDoor>("GeneratorAccess").Interactable.TryInteract(player);
            await SettlePhysics();
            Check(HasRoute(new Vector2(2700, 1600), new Vector2(2600, 250), player), "credential opens a traversable generator route");
            Check(HasRoute(new Vector2(2600, 250), new Vector2(1400, 300), player), "generator connects to relay panel");
            Check(socket.Interactable.TryInteract(player), "fuse installs after collection");
            Check(CampaignActDirector.Instance.ActiveObjective?.CompletionFlag == StoryFlags.FACILITY_POWER_RESTORED, "repair leads to routing objective");
            var panel = Scene().GetNode<PowerRoutingPanel>("PowerRoutingPanel");
            Check(panel.Interactable.TryInteract(player), "routing panel opens after repair");
            var view = Find<PowerRoutingUI>(panel)[0];
            Check(Tree.Paused, "puzzle pauses danger");
            var switches = Find<CheckButton>(view);
            var apply = Find<Button>(view).Find(button => button.Text == "APPLY ROUTING");
            apply.EmitSignal(Button.SignalName.Pressed);
            Check(!StoryDirector.Instance.HasFlag(StoryFlags.FACILITY_POWER_RESTORED), "incorrect routing cannot restore power");
            switches[0].ButtonPressed = true;
            switches[1].ButtonPressed = true;
            apply.EmitSignal(Button.SignalName.Pressed);
            Check(StoryDirector.Instance.HasFlag(StoryFlags.FACILITY_POWER_RESTORED) && !Tree.Paused, "correct circuits restore power and resume play");
            gate.OpenDoor();
            await SettlePhysics();
            Check(gate.IsOpen, "power unlocks gate");
            Check(HasRoute(new Vector2(500, 1500), new Vector2(500, 100), player), "powered gate provides a traversable bunker exit");
            Scene().GetNode<CampaignInteraction>("OverrideConsole").Interactable.TryInteract(player);
            Check(StoryDirector.Instance.HasFlag(StoryFlags.GATE_OVERRIDDEN), "bunker override completes opening");
            var deadEnemy = Scene().GetNode<Zombie>("Infected1");
            deadEnemy.PerformDeath();
            var woundedEnemy = Scene().GetNode<Zombie>("Infected2");
            woundedEnemy.HealthComponent.SetCurrentHealth(23);
            player.GlobalPosition = new Vector2(350, 700);
            player.HealthComponent.SetCurrentHealth(61);
            Check(saves.Save(0, player, Opening, StoryAct.Act1_Arrival), "manual save succeeds");
            Check(saves.ActiveSaveData.HasSavedPosition, "same-scene save retains position");
            Check(saves.ActiveSaveData.CompletedWorldObjects.Contains(Opening + "::infected_1"), "defeated enemy recorded");
            Check(saves.Load(0), "save reload succeeds");
            await Level(Opening);
            player = Player();
            player.IsInputLocked = true;
            Check(player.GlobalPosition.DistanceTo(new Vector2(350, 700)) < 5, "saved position restored");
            Check(Mathf.IsEqualApprox(player.HealthComponent.CurrentHealth, 61), "health restored");
            Check(player.InventoryComponent.HasItem("auxiliary_fuse"), "mission items survive disk roundtrip");
            Check(Scene().GetNodeOrNull<Pickup>("SpareFuse") == null, "collected fuse remains absent");
            Check(Scene().GetNodeOrNull<Zombie>("Infected1") == null, "defeated enemy stays defeated");
            Check(Mathf.IsEqualApprox(Scene().GetNode<Zombie>("Infected2").HealthComponent.CurrentHealth, 23), "wounded enemy retains health");
            Check(Scene().GetNode<BlastDoor>("NorthGateLock").IsOpen, "open door restored without animation");
            Check(CampaignActDirector.Instance.ActiveObjective == null, "completed objectives stay completed");
            player.InventoryComponent.ClearInventory();
            saves.Save(0, player, Opening, StoryAct.Act1_Arrival);
            saves.Load(0);
            await Level(Opening);
            Check(Player().InventoryComponent.MissionItems.Count == 0, "empty inventory restores as empty");
            Scene().GetNode<ActTransitionTrigger>("BunkerExit").ExitInteractable.TryInteract(Player());
            Check(!saves.ActiveSaveData.HasSavedPosition, "next scene uses its own entrance");
            await ToSignal(Tree.CreateTimer(4), SceneTreeTimer.SignalName.Timeout);
            Check(Scene().SceneFilePath == "res://Scenes/Act2_Checkpoint.tscn", "bunker exit transitions to next chapter");
            Check(SaveSystem.Instance == saves && saves.IsInsideTree(), "same save session survives scene change");
            Check(StoryDirector.Instance.HasFlag(StoryFlags.GATE_OVERRIDDEN), "story flags carry across chapters");
            Check(StoryDirector.Instance.CurrentAct == StoryAct.Act2_Bunker, "chapter identity restored");
            await VerifyBunker(saves);
            await VerifyDecontamination(saves);
            await VerifyLab(saves);
            await VerifyArchives(saves);
            await VerifyExtraction(saves);
            await Level("res://Scenes/MainMenu.tscn");
            Check(SaveSystem.Instance == saves, "return to menu does not replace save session");
            GD.Print($"CAMPAIGN SMOKE PASS: {_checks} checks");
            Tree.CurrentScene.QueueFree();
            await ToSignal(Tree.CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            Tree.Quit();
        }
        catch (Exception error)
        {
            GD.PushError("CAMPAIGN SMOKE FAIL: " + error);
            Tree.Quit(1);
        }
    }

    private async Task VerifyBunker(SaveSystem saves)
    {
        const string bunker = "res://Scenes/Act2_Checkpoint.tscn";
        await VerifyDialogueSafety();
        var player = Player();
        player.IsInputLocked = true;
        player.IsInvulnerable = true;
        Check(CampaignActDirector.Instance.Rooms.Count == 9, "bunker has nine mapped rooms");
        Check(StoryDirector.Instance.HasFlag(StoryFlags.FACILITY_POWER_RESTORED), "gate power persists into bunker");
        var panel = Scene().GetNode<PowerRoutingPanel>("SecurityRoutingPanel");
        Check(panel.Interactable.IsActive, "gate power does not solve checkpoint puzzle");
        Check(CampaignActDirector.Instance.ActiveObjective?.CompletionFlag == "guard_credential_found", "bunker begins with credential objective");
        Check(HasRoute(new Vector2(500, 2800), new Vector2(1650, 2600), player), "entry connects to guard credential");
        Check(HasRoute(new Vector2(500, 2800), new Vector2(2600, 2600), player), "infirmary offers alternate exploration route");
        Check(!HasRoute(new Vector2(500, 2800), new Vector2(300, 1350), player), "all office entrances require credential");
        Check(!HasRoute(new Vector2(500, 2800), new Vector2(1500, 250), player), "records cannot be reached before security power");
        var report = Scene().GetNode<CampaignInteraction>("IncidentReport");
        Check(!report.Interactable.TryInteract(player), "incident report requires credential");
        Check(!panel.Interactable.TryInteract(player), "security routing requires investigation");
        var runner = Scene().GetNode<Zombie>("VentRunner");
        Check(!runner.Visible && runner.CollisionLayer == 0 && runner.ProcessMode == ProcessModeEnum.Disabled,
            "vent runner is dormant before grid restoration");
        Scene().GetNode<Pickup>("GuardCredential").CollectByPlayer(player);
        Check(report.Interactable.TryInteract(player), "credential allows security report access");
        Scene().GetNode<BlastDoor>("SecuritySouthDoor").Interactable.TryInteract(player);
        Scene().GetNode<BlastDoor>("SecurityEastDoor").Interactable.TryInteract(player);
        await SettlePhysics();
        Check(HasRoute(new Vector2(500, 2800), new Vector2(300, 1350), player), "credential opens a physical office route");
        player.GlobalPosition = new Vector2(400, 2500);
        Scene().GetNode<CampaignInteraction>("EntryRecorder").Interactable.TryInteract(player);
        Check(saves.GetSlotInfo(0).Flags.Contains("checkpoint_report_read"), "field recorder saves bunker investigation");
        saves.Load(0);
        await Level(bunker);
        player = Player();
        player.IsInputLocked = true;
        player.IsInvulnerable = true;
        runner = Scene().GetNode<Zombie>("VentRunner");
        Check(!runner.Visible && runner.CollisionLayer == 0, "dormant enemy remains dormant after reload");
        Check(player.InventoryComponent.HasItem("guard_credential"), "guard credential survives checkpoint reload");
        panel = Scene().GetNode<PowerRoutingPanel>("SecurityRoutingPanel");
        panel.Interactable.TryInteract(player);
        var view = Find<PowerRoutingUI>(panel)[0];
        var switches = Find<CheckButton>(view);
        var apply = Find<Button>(view).Find(button => button.Text == "APPLY ROUTING");
        Check(switches.Count == 4, "checkpoint puzzle exposes four circuits");
        switches[0].ButtonPressed = true;
        switches[1].ButtonPressed = true;
        apply.EmitSignal(Button.SignalName.Pressed);
        Check(!StoryDirector.Instance.HasFlag("security_grid_routed") && Tree.Paused, "gate puzzle solution cannot solve bunker puzzle");
        switches[3].ButtonPressed = true;
        apply.EmitSignal(Button.SignalName.Pressed);
        Check(StoryDirector.Instance.HasFlag("security_grid_routed") && !Tree.Paused, "correct bunker routing completes local objective");
        Check(runner.Visible && runner.CollisionLayer == 2 && runner.ProcessMode != ProcessModeEnum.Disabled,
            "power activates authored vent encounter");
        Check(!panel.Interactable.IsActive, "completed checkpoint panel cannot be repeated");
        foreach (string name in new[] { "RecordsSouthDoor", "LiftSouthDoor", "LiftRecordsDoor" })
            Scene().GetNode<BlastDoor>(name).Interactable.TryInteract(player);
        await SettlePhysics();
        Check(HasRoute(new Vector2(1450, 1250), new Vector2(1500, 250), player), "security routing opens northern records route");
        Check(!HasRoute(new Vector2(1500, 250), new Vector2(2500, 550), player), "optional armory remains locked on both entrances");
        Check(!Scene().GetNode<CampaignInteraction>("LiftConsole").Interactable.TryInteract(player), "lift authorization requires actual transfer orders");
        Check(!StoryDirector.Instance.HasFlag("lab_access_authorized"), "lab cannot be authorized prematurely");
        Scene().GetNode<Pickup>("TransferOrders").CollectByPlayer(player);
        Check(Scene().GetNode<CampaignInteraction>("LiftConsole").Interactable.TryInteract(player), "transfer orders authorize laboratory lift");
        Scene().GetNode<Pickup>("ArmoryKey").CollectByPlayer(player);
        Check(!player.InventoryComponent.IsWeaponUnlocked("shotgun"),"shotgun remains unavailable before entering its optional armory");
        Scene().GetNode<BlastDoor>("ArmoryWestDoor").Interactable.TryInteract(player);
        Scene().GetNode<BlastDoor>("ArmorySouthDoor").Interactable.TryInteract(player);
        await SettlePhysics();
        Check(HasRoute(new Vector2(1500, 250), new Vector2(2400, 350), player), "infirmary key opens optional armory cache");
        Check(Scene().GetNode<Pickup>("ArmoryShotgun").CollectByPlayer(player)
            && player.InventoryComponent.IsWeaponUnlocked("shotgun"),"optional armory grants its actual shotgun");
        Scene().GetNode<Pickup>("ArmoryShells1").CollectByPlayer(player);
        runner.PerformDeath();
        Scene().GetNode<Zombie>("ReturnRunner").HealthComponent.SetCurrentHealth(17);
        player.GlobalPosition = new Vector2(400, 700);
        Scene().GetNode<CampaignInteraction>("LiftRecorder").Interactable.TryInteract(player);
        saves.Load(0);
        await Level(bunker);
        Check(Scene().GetNodeOrNull<Zombie>("VentRunner") == null, "defeated vent encounter stays defeated");
        Check(Scene().GetNode<Zombie>("ReturnRunner").Visible, "remaining activated encounter stays active");
        Check(Mathf.IsEqualApprox(Scene().GetNode<Zombie>("ReturnRunner").HealthComponent.CurrentHealth, 17), "return encounter retains damage");
        Check(Scene().GetNodeOrNull<Pickup>("ArmoryShells1") == null, "optional cache cannot be duplicated by reloading");
        Check(Player().InventoryComponent.IsWeaponUnlocked("shotgun") && Scene().GetNodeOrNull<Pickup>("ArmoryShotgun") == null,
            "armory weapon ownership and collection survive checkpoint reload");
        Check(!Scene().GetNode<PowerRoutingPanel>("SecurityRoutingPanel").Interactable.IsActive, "completed bunker puzzle stays completed after reload");
        Check(CampaignActDirector.Instance.ActiveObjective == null, "bunker objectives remain complete after reload");
        Check(Player().GlobalPosition.DistanceTo(new Vector2(400, 700)) < 5, "lift checkpoint restores its saved position");
        saves.ActiveSaveData.SceneLayoutVersions.Remove(bunker);
        await Level(bunker);
        Check(Player().GlobalPosition.DistanceTo(new Vector2(500, 2800)) < 5, "old layout saves resume at the new entrance");
        var transitionDialogue = new DialogueSequence();
        transitionDialogue.Lines.Add(new DialogueLineResource { Text = "Interrupted conversation." });
        DialogueManager.Instance.StartDialogue(transitionDialogue);
        Scene().GetNode<ActTransitionTrigger>("LabExit").ExitInteractable.TryInteract(Player());
        await ToSignal(Tree.CreateTimer(4), SceneTreeTimer.SignalName.Timeout);
        Check(Scene().SceneFilePath == "res://Scenes/Transit_Decontamination.tscn", "bunker access leads into playable transfer corridor");
        Check(!Tree.Paused && !Player().IsInputLocked, "transition cancels conversation without carrying a frozen state");
        Check(Player().InventoryComponent.HasItem("lab_transfer_orders"), "transfer evidence carries into laboratory");
    }

    private async Task VerifyDecontamination(SaveSystem saves)
    {
        const string transit = "res://Scenes/Transit_Decontamination.tscn";
        var player = Player();
        player.IsInputLocked = true;
        player.IsInvulnerable = true;
        var controller = Scene().GetNode<AirlockCycleController>("AirlockController");
        var exit = Scene().GetNode<ActTransitionTrigger>("LabAccess");
        Check(CampaignActDirector.Instance.Rooms.Count == 5, "transfer corridor has five mapped connected spaces");
        Check(controller.State == AirlockState.Idle && controller.OuterDoor.IsOpen && !controller.InnerDoor.IsOpen,
            "airlock begins with accessible return route and sealed inner door");
        Check(exit.RequireInteraction && exit.ExitInteractable != null, "lab entry requires deliberate interaction");
        exit.EmitSignal(Area2D.SignalName.BodyEntered, player);
        Check(Scene().SceneFilePath == transit && !Tree.Paused, "walking onto access trigger cannot skip airlock");
        Check(!exit.TryTransition(player), "uncleared transfer cannot enter laboratory");
        Check(Find<MiniMapUI>(Scene())[0].RadarPanel != null && Find<MiniMapUI>(Scene())[0].MapCanvas != null,
            "facility map has a rendered panel and canvas");
        Check(HasRoute(new Vector2(700,2350), new Vector2(240,1450), player), "checkpoint physically connects to exhaust utility");
        Check(HasRoute(new Vector2(240,1450), new Vector2(1170,1450), player), "utility and maintenance bays connect through corridor");
        Check(!HasRoute(new Vector2(700,1400), new Vector2(700,400), player), "sealed inner bulkhead blocks northern access");
        controller.Controls.TryInteract(player);
        await SettlePhysics();
        Check(controller.State == AirlockState.Idle && controller.Message.Contains("INTERLOCK"), "missing repairs prevent unsafe cycle");
        Scene().GetNode<CampaignInteraction>("ExhaustBreaker").Interactable.TryInteract(player);
        Scene().GetNode<CampaignInteraction>("CoolantValve").Interactable.TryInteract(player);
        controller.Controls.TryInteract(player);
        await SettlePhysics();
        Check(controller.State == AirlockState.Idle && controller.Message.Contains("STAND INSIDE"), "cycle cannot trap player outside chamber");
        player.GlobalPosition = new Vector2(700,1400);
        await SettlePhysics();
        controller.Controls.TryInteract(player);
        await SettlePhysics();
        Check(controller.State == AirlockState.Idle && controller.Message.Contains("CONTAMINATION"), "infected occupancy blocks sealing");
        Scene().GetNode<Zombie>("ChamberWalker").TakeDamage(999,Vector2.Zero);
        var obstruction = Scene().GetNode<Zombie>("MaintenanceRunner");
        obstruction.SetPhysicsProcess(false);
        obstruction.Rotation = 0;
        obstruction.GlobalPosition = new Vector2(945,1450);
        await SettlePhysics();
        controller.Controls.TryInteract(player);
        await SettlePhysics();
        Check(controller.State == AirlockState.Idle && controller.Message.Contains("DOORWAY OBSTRUCTED"), "bulkhead refuses to close on an occupied threshold: " + controller.Message);
        obstruction.TakeDamage(999,Vector2.Zero);
        await SettlePhysics();
        controller.Controls.TryInteract(player);
        await SettlePhysics();
        Check(controller.IsCycling && !controller.OuterDoor.IsOpen && !controller.InnerDoor.IsOpen,
            "clear chamber seals both main bulkheads before pressure cycle");
        controller.Controls.TryInteract(player);
        await SettlePhysics();
        Check(controller.State == AirlockState.Idle && controller.OuterDoor.IsOpen && !controller.InnerDoor.IsOpen,
            "abort reopens escape route without granting lab clearance");
        Check(!StoryDirector.Instance.HasFlag("decon_cycle_complete"), "aborted cycle remains incomplete");
        controller.Controls.TryInteract(player);
        await ToSignal(Tree.CreateTimer(1.3),SceneTreeTimer.SignalName.Timeout);
        Check(controller.State == AirlockState.Equalizing && controller.Progress > 0, "pressure increases only after sealing");
        float progress = controller.Progress;
        Tree.Paused = true;
        await ToSignal(Tree.CreateTimer(0.3),SceneTreeTimer.SignalName.Timeout);
        Check(Mathf.IsEqualApprox(controller.Progress,progress), "pause stops airlock simulation");
        Tree.Paused = false;
        Check(saves.Save(0,player,transit,StoryAct.Act2_Bunker), "mid-cycle checkpoint can be saved");
        saves.Load(0);
        await Level(transit);
        controller = Scene().GetNode<AirlockCycleController>("AirlockController");
        player = Player();
        player.IsInvulnerable = true;
        Check(controller.State == AirlockState.Idle && controller.OuterDoor.IsOpen && !controller.InnerDoor.IsOpen,
            "unfinished cycle reloads safely with return doors open");
        Check(StoryDirector.Instance.HasFlag("decon_exhaust_online") && StoryDirector.Instance.HasFlag("decon_leak_isolated"),
            "airlock repairs survive reload");
        Check(Scene().GetNodeOrNull<Zombie>("ChamberWalker") == null && Scene().GetNodeOrNull<Zombie>("MaintenanceRunner") == null,
            "transfer enemies remain defeated after reload");
        // Traverse the successful route using actual movement input, not position teleports.
        await WalkTo(new Vector2(700,2200));
        await WalkTo(new Vector2(700,1450));
        await WalkTo(new Vector2(240,1450));
        await WalkTo(new Vector2(700,1450));
        await WalkTo(new Vector2(1170,1450));
        await WalkTo(new Vector2(700,1400));
        await CapturePlaytest("decon-ready");
        await PressKey(Key.E);
        await SettlePhysics();
        Check(controller.IsCycling, "real interaction input starts the airlock at the panel");
        await CapturePlaytest("decon-cycle");
        await ToSignal(Tree.CreateTimer(controller.Data.SealSeconds + controller.Data.EqualizeSeconds + controller.Data.PurgeSeconds + 0.3),
            SceneTreeTimer.SignalName.Timeout);
        Check(controller.State == AirlockState.Complete && controller.InnerDoor.IsOpen && !controller.OuterDoor.IsOpen,
            "completed purge opens only the inner bulkhead");
        Check(StoryDirector.Instance.HasFlag("decon_cycle_complete"), "cycle completion becomes persistent clearance");
        Check(HasRoute(new Vector2(700,1400),new Vector2(700,400),player), "cleared chamber connects to lab access panel");
        var map = Find<MiniMapUI>(Scene())[0];
        map.ToggleFullscreenMap();
        await ToSignal(Tree,SceneTree.SignalName.ProcessFrame);
        Check(map.IsFullscreen && map.MapCanvas.Size.X > 300, "full facility map expands its visible canvas");
        var inventoryMenu = Find<InventoryUI>(Scene())[0];
        inventoryMenu.ToggleInventory();
        Check(!inventoryMenu.Visible && Tree.Paused, "inventory cannot replace the open map pause");
        await CapturePlaytest("decon-map");
        map.ToggleFullscreenMap();
        inventoryMenu.ToggleInventory();
        map.ToggleFullscreenMap();
        Check(inventoryMenu.Visible && !map.IsFullscreen && Tree.Paused, "map cannot replace the open inventory pause");
        inventoryMenu.ToggleInventory();
        Check(!Tree.Paused, "closing the active menu resumes airlock gameplay");
        saves.Save(0,player,transit,StoryAct.Act2_Bunker);
        saves.Load(0);
        await Level(transit);
        controller = Scene().GetNode<AirlockCycleController>("AirlockController");
        player = Player();
        Check(controller.State == AirlockState.Complete && controller.InnerDoor.IsOpen, "completed transfer remains cleared after reload");
        exit = Scene().GetNode<ActTransitionTrigger>("LabAccess");
        saves.CurrentSlot = 99;
        Check(!exit.TryTransition(player) && Scene().SceneFilePath == transit, "failed checkpoint cannot advance scene");
        Check(!StoryDirector.Instance.HasFlag("decon_transfer_finished"), "failed departure does not consume its completion flag");
        saves.CurrentSlot = 0;
        string savePath = ProjectSettings.GlobalizePath("user://saves/slot_0.json");
        string checkpointBeforeFailure = System.IO.File.ReadAllText(savePath);
        var activeBeforeFailure = saves.ActiveSaveData;
        // An occupied temporary-file path creates a real write failure without touching normal saves.
        System.IO.Directory.CreateDirectory(savePath + ".tmp");
        try
        {
            Check(!exit.TryTransition(player), "real checkpoint I/O failure prevents departure");
            Check(ReferenceEquals(saves.ActiveSaveData,activeBeforeFailure) && saves.ActiveSaveData.CurrentScenePath == transit
                && saves.ActiveSaveData.HasSavedPosition, "failed save preserves the active checkpoint and its position");
            Check(System.IO.File.ReadAllText(savePath) == checkpointBeforeFailure, "failed save leaves the last disk checkpoint intact");
            Check(!StoryDirector.Instance.HasFlag("decon_transfer_finished"), "I/O failure rolls back departure flag");
        }
        finally { System.IO.Directory.Delete(savePath + ".tmp"); }
        await WalkTo(new Vector2(700,400));
        await CapturePlaytest("decon-cleared");
        await PressKey(Key.E);
        await ToSignal(Tree.CreateTimer(4),SceneTreeTimer.SignalName.Timeout);
        Check(Scene().SceneFilePath == "res://Scenes/Act3_Lab.tscn", "physical crossing and explicit access input enter laboratory");
        Check(StoryDirector.Instance.HasFlag("decon_transfer_finished") && player != Player(), "laboratory receives transfer state and a new scene player");
        Check(Player().InventoryComponent.HasItem("lab_transfer_orders"), "transfer preserves carried evidence");
    }

    private async Task WalkTo(Vector2 target)
    {
        var player = Player();
        player.IsInputLocked = false;
        string[] actions = { "move_left","move_right","move_up","move_down" };
        try
        {
            int frames = 0;
            while (player.GlobalPosition.DistanceTo(target) > 18 && frames++ < 1200)
            {
                Vector2 offset = target - player.GlobalPosition;
                bool[] pressed = { offset.X < -10,offset.X > 10,offset.Y < -10,offset.Y > 10 };
                for (int i=0;i<4;i++)
                    if (pressed[i]) Input.ActionPress(actions[i]); else Input.ActionRelease(actions[i]);
                await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame);
                await ToSignal(Tree,SceneTree.SignalName.ProcessFrame);
            }
            Check(player.GlobalPosition.DistanceTo(target) <= 18, "physical movement reaches " + target);
        }
        finally { foreach (string action in actions) Input.ActionRelease(action); }
    }

    private async Task CapturePlaytest(string name)
    {
        if (!Array.Exists(OS.GetCmdlineUserArgs(),arg => arg == "--capture-playtest") || DisplayServer.GetName() == "headless") return;
        await ToSignal(Tree,SceneTree.SignalName.ProcessFrame);
        // A minimized window may stop automatic drawing while simulation keeps running.
        RenderingServer.ForceDraw(false);
        var pixels = GetViewport().GetTexture().GetImage();
        Check(pixels.SavePng("/tmp/foxtrot-" + name + ".png") == Error.Ok,"rendered playtest capture " + name);
    }

    private async Task VerifyLab(SaveSystem saves)
    {
        const string lab = "res://Scenes/Act3_Lab.tscn";
        await Level(lab);
        var player = Player();
        player.IsInputLocked = true;
        player.IsInvulnerable = true;
        Check(CampaignActDirector.Instance.Rooms.Count == 8, "lab has eight mapped spaces");
        Check(CampaignActDirector.Instance.ActiveObjective?.CompletionFlag == StoryFlags.LEILA_NOTE_FOUND,
            "lab begins with investigation before the rescue choice");
        Check(HasRoute(new Vector2(500, 2800), new Vector2(1400, 1500), player), "arrival connects to observation intercom");
        Check(HasRoute(new Vector2(500, 2800), new Vector2(2700, 2500), player), "air plant can be explored before deciding");
        Check(!HasRoute(new Vector2(500, 2800), new Vector2(1500, 300), player), "unrepaired isolation chamber is physically sealed");
        Check(!HasRoute(new Vector2(500, 2800), new Vector2(2500, 250), player), "both research-core entrances are initially sealed");
        Check(!HasRoute(new Vector2(500, 2800), new Vector2(400, 300), player), "archive access cannot bypass the research objective");
        Check(!Scene().GetNode<CampaignInteraction>("ObservationIntercom").Interactable.TryInteract(player), "intercom requires transfer-note investigation");
        Check(!Scene().GetNode<CampaignInteraction>("ResearchTerminal").Interactable.TryInteract(player), "research cannot be recovered before completing a route");
        Check(!Scene().GetNode<CampaignInteraction>("DrainValve").Interactable.TryInteract(player), "drain cannot be opened before pressure release");
        Scene().GetNode<CampaignInteraction>("TriageNotes").Interactable.TryInteract(player);
        Check(saves.Save(1, player, lab, StoryAct.Act3_Biocontainment), "pre-choice lab checkpoint saves");
        var intercom = Scene().GetNode<CampaignInteraction>("ObservationIntercom");
        intercom.Interactable.TryInteract(player);
        DialogueManager.Instance.CancelDialogue();
        Check(intercom.Interactable.IsActive && !StoryDirector.Instance.HasFlag("lab_route_decided"), "interrupted intercom remains usable without completing choice");
        intercom.Interactable.TryInteract(player);
        await CompleteCurrentDialogue(1);
        Check(StoryDirector.Instance.HasFlag(StoryFlags.LEILA_RESCUE_ACCEPTED)
            && !StoryDirector.Instance.HasFlag(StoryFlags.LEILA_ABANDONED), "rescue choice selects only rescue route");
        Check(!StoryDirector.Instance.HasFlag(StoryFlags.LEILA_RESCUED), "accepting rescue does not complete it");
        Check(CampaignActDirector.Instance.ActiveObjective?.CompletionFlag == StoryFlags.BIOLAB_KEYCARD_FOUND,
            "rescue route presents its own keycard objective");
        Check(!Scene().GetNode<CampaignInteraction>("PressureValve").Interactable.TryInteract(player), "rescue choice does not enable service bypass");
        Check(!Scene().GetNode<CampaignInteraction>("FilterSocket").Interactable.TryInteract(player), "cartridge installation requires the actual item");
        Scene().GetNode<Pickup>("BiolabKeycardPickup").CollectByPlayer(player);
        Check(!Scene().GetNode<BlastDoor>("IsolationSouthDoor").Interactable.TryInteract(player), "keycard alone cannot open unsafe isolation chamber");
        Scene().GetNode<Pickup>("FilterCartridge").CollectByPlayer(player);
        Scene().GetNode<CampaignInteraction>("FilterSocket").Interactable.TryInteract(player);
        var panel = Scene().GetNode<PowerRoutingPanel>("FiltrationPanel");
        panel.Interactable.TryInteract(player);
        var view = Find<PowerRoutingUI>(panel)[0];
        var switches = Find<CheckButton>(view);
        var apply = Find<Button>(view).Find(button => button.Text == "APPLY ROUTING");
        foreach (var toggle in switches) toggle.ButtonPressed = true;
        apply.EmitSignal(Button.SignalName.Pressed);
        Check(!StoryDirector.Instance.HasFlag("lab_filtration_online") && Tree.Paused, "UV purge is rejected while Leila occupies chamber");
        switches[2].ButtonPressed = false;
        apply.EmitSignal(Button.SignalName.Pressed);
        Check(StoryDirector.Instance.HasFlag("lab_filtration_online") && !Tree.Paused, "safe routing supplies clean air");
        Scene().GetNode<BlastDoor>("IsolationSouthDoor").Interactable.TryInteract(player);
        await SettlePhysics();
        Check(HasRoute(new Vector2(1400, 1500), new Vector2(1500, 300), player), "filter repair opens a traversable rescue route");
        Check(!StoryDirector.Instance.HasFlag(StoryFlags.LEILA_RESCUED), "opening door alone does not secure Leila");
        Scene().GetNode<CampaignInteraction>("LeilaRefuge").Interactable.TryInteract(player);
        Check(Tree.Paused, "refuge conversation pauses combat");
        await CompleteCurrentDialogue();
        Check(StoryDirector.Instance.HasFlag(StoryFlags.LEILA_RESCUED) && StoryDirector.Instance.HasFlag("lab_route_complete"), "reaching Leila completes playable rescue");
        Scene().GetNode<BlastDoor>("CoreRefugeDoor").Interactable.TryInteract(player);
        await SettlePhysics();
        Check(HasRoute(new Vector2(1500, 300), new Vector2(2500, 250), player), "rescued researcher grants physical core access");
        Check(!Scene().GetNode<Zombie>("TriageReturnRunner").Visible, "return encounter stays dormant before research download");
        Scene().GetNode<CampaignInteraction>("ResearchTerminal").Interactable.TryInteract(player);
        Check(player.InventoryComponent.HasItem("research_drive") && StoryDirector.Instance.HasFlag("research_downloaded"), "research terminal awards persistent telemetry drive");
        Check(Scene().GetNode<Zombie>("TriageReturnRunner").Visible, "telemetry retrieval changes return encounter");
        Check(!Scene().GetNode<CampaignInteraction>("ResearchTerminal").Interactable.TryInteract(player), "completed research terminal cannot award drive twice");
        Scene().GetNode<BlastDoor>("ArchiveLiftDoor").Interactable.TryInteract(player);
        await SettlePhysics();
        Check(HasRoute(new Vector2(2500, 250), new Vector2(400, 300), player), "rescue route returns to archive lift");
        Scene().GetNode<CampaignInteraction>("Archivist").Interactable.TryInteract(player);
        DialogueManager.Instance.CancelDialogue();
        Check(!player.InventoryComponent.HasItem("archivist_journal"), "cancelled Archivist conversation does not grant its reward");
        Scene().GetNode<CampaignInteraction>("Archivist").Interactable.TryInteract(player);
        await CompleteCurrentDialogue();
        Check(player.InventoryComponent.HasItem("archivist_journal") && StoryDirector.Instance.HasFlag(StoryFlags.FOUND_ARCHIVIST), "optional Archivist conversation awards journal");
        Scene().GetNode<CampaignInteraction>("ArchiveConsole").Interactable.TryInteract(player);
        player.GlobalPosition = new Vector2(400, 700);
        Check(saves.Save(0, player, lab, StoryAct.Act3_Biocontainment), "completed rescue route saves");
        saves.Load(0);
        await Level(lab);
        Check(StoryDirector.Instance.HasFlag(StoryFlags.LEILA_RESCUED) && !StoryDirector.Instance.HasFlag(StoryFlags.LEILA_ABANDONED), "rescue outcome survives reload");
        Check(!Scene().GetNode<CampaignInteraction>("LeilaRefuge").Interactable.IsActive, "Leila cannot be rescued repeatedly");
        Check(Scene().GetNodeOrNull<Pickup>("FilterCartridge") == null, "installed filter cannot be recollected after reload");
        Check(Player().InventoryComponent.HasItem("research_drive") && Player().InventoryComponent.MissionItems["archivist_journal"].Count == 1,
            "drive and single journal survive reload");
        Check(CampaignActDirector.Instance.ActiveObjective == null, "rescue objectives stay complete after reload");
        // Prototype saves recorded a rescue without the new plant/route flags.
        saves.Load(1);
        saves.ActiveSaveData.Flags.Add(StoryFlags.LEILA_RESCUED);
        saves.ActiveSaveData.Flags.Add(StoryFlags.LEILA_ABANDONED);
        saves.ActiveSaveData.SceneLayoutVersions[lab] = 1;
        saves.ActiveSaveData.HasSavedPosition = true;
        saves.ActiveSaveData.PlayerPosX = 500;
        saves.ActiveSaveData.PlayerPosY = 500;
        await Level(lab);
        Check(Player().GlobalPosition.DistanceTo(new Vector2(500, 2800)) < 5, "prototype lab save resumes at safe entrance");
        Check(StoryDirector.Instance.HasFlag("lab_route_complete") && StoryDirector.Instance.HasFlag("lab_filtration_online"),
            "prototype rescue save derives new refuge prerequisites");
        Check(!StoryDirector.Instance.HasFlag(StoryFlags.LEILA_ABANDONED), "rescued survivor cannot also be recorded as abandoned");
        Check(CampaignActDirector.Instance.ActiveObjective?.CompletionFlag == "research_downloaded", "prototype rescue does not repeat completed rescue errands");
        // Restore the checkpoint before the decision and prove the other route independently.
        saves.Load(1);
        await Level(lab);
        player = Player();
        player.IsInputLocked = true;
        player.IsInvulnerable = true;
        Scene().GetNode<CampaignInteraction>("ObservationIntercom").Interactable.TryInteract(player);
        await CompleteCurrentDialogue(2);
        Check(StoryDirector.Instance.HasFlag(StoryFlags.LEILA_ABANDONED)
            && !StoryDirector.Instance.HasFlag(StoryFlags.LEILA_RESCUE_ACCEPTED), "bypass choice excludes rescue route");
        Check(CampaignActDirector.Instance.ActiveObjective?.CompletionFlag == "lab_pressure_released", "bypass presents pressure objective rather than rescue errands");
        Check(!Scene().GetNode<CampaignInteraction>("FilterSocket").Interactable.TryInteract(player), "bypass choice cannot install rescue filter");
        Scene().GetNode<BlastDoor>("ServiceWestDoor").Interactable.TryInteract(player);
        Scene().GetNode<BlastDoor>("ServiceSouthDoor").Interactable.TryInteract(player);
        await SettlePhysics();
        Check(HasRoute(new Vector2(1400, 1500), new Vector2(2700, 1200), player), "bypass opens a physical service route");
        Check(!Scene().GetNode<CampaignInteraction>("DrainValve").Interactable.TryInteract(player), "bypass still requires pressure before draining");
        Scene().GetNode<CampaignInteraction>("PressureValve").Interactable.TryInteract(player);
        Scene().GetNode<CampaignInteraction>("DrainValve").Interactable.TryInteract(player);
        Scene().GetNode<BlastDoor>("CoreServiceDoor").Interactable.TryInteract(player);
        await SettlePhysics();
        Check(HasRoute(new Vector2(2700, 1200), new Vector2(2500, 250), player), "draining service lock opens research-core route");
        Check(!player.InventoryComponent.HasItem("biolab_keycard") && !player.InventoryComponent.HasItem("lab_filter_cartridge"), "bypass requires neither rescue item");
        Check(!StoryDirector.Instance.HasFlag(StoryFlags.LEILA_RESCUED), "service bypass does not falsely rescue Leila");
        Scene().GetNode<CampaignInteraction>("ResearchTerminal").Interactable.TryInteract(player);
        Scene().GetNode<BlastDoor>("ArchiveLiftDoor").Interactable.TryInteract(player);
        Scene().GetNode<CampaignInteraction>("ArchiveConsole").Interactable.TryInteract(player);
        Check(CampaignActDirector.Instance.ActiveObjective == null, "bypass can complete every applicable lab objective");
        Check(!Scene().GetNode<BlastDoor>("IsolationSouthDoor").IsOpen, "Leila's chamber remains sealed on bypass route");
        player.GlobalPosition = new Vector2(400, 700);
        saves.Save(0, player, lab, StoryAct.Act3_Biocontainment);
        saves.Load(0);
        await Level(lab);
        Check(StoryDirector.Instance.HasFlag(StoryFlags.LEILA_ABANDONED) && !StoryDirector.Instance.HasFlag(StoryFlags.LEILA_RESCUED), "bypass outcome survives reload without becoming rescue");
        Check(!Scene().GetNode<CampaignInteraction>("DrainValve").Interactable.IsActive, "completed service mechanism stays complete after reload");
        Scene().GetNode<ActTransitionTrigger>("ArchiveExit").ExitInteractable.TryInteract(Player());
        await ToSignal(Tree.CreateTimer(4), SceneTreeTimer.SignalName.Timeout);
        Check(Scene().SceneFilePath == "res://Scenes/Act4_SubBasement.tscn", "authorized lab descent transitions to archives");
        Check(Player().InventoryComponent.HasItem("research_drive") && StoryDirector.Instance.HasFlag(StoryFlags.LEILA_ABANDONED),
            "archive chapter receives evidence and actual survivor outcome");
    }

    private async Task VerifyExtraction(SaveSystem saves)
    {
        const string arena = "res://Scenes/Act5_Arena.tscn";
        var player = Player();
        player.IsInputLocked = true; player.IsInvulnerable = true;
        var boss = Scene().GetNode<Boss_Unit9>("BossUnit9");
        var trap = Scene().GetNode<CoolantTrapController>("CoolantValve");
        var evac = Scene().GetNode<EvacHelipad>("EvacHelipad");
        var extraction = Scene().GetNode<HelicopterExtractionController>("Extraction");
        Check(CampaignActDirector.Instance.Rooms.Count == 7, "extraction has seven mapped industrial spaces");
        Check(!boss.IsEncounterActive && !evac.Interactable.IsActive, "boss and boarding stay inactive before preparation");
        boss.TakeDamage(50,Vector2.Zero);
        Check(Mathf.IsEqualApprox(boss.HealthComponent.CurrentHealth,800), "dormant boss cannot be sniped before containment release");
        Check(!Scene().GetNode<CampaignInteraction>("BackupGenerator").Interactable.TryInteract(player), "backup requires procedure and starter battery");
        Check(!Scene().GetNode<CampaignInteraction>("ContainmentRelease").Interactable.TryInteract(player), "unpowered shutter cannot release encounter");
        Check(!Scene().GetNode<CampaignInteraction>("RadioBeacon").Interactable.TryInteract(player), "beacon cannot bypass Unit-9");
        Check(HasRoute(new Vector2(500,2800),new Vector2(2700,2500),player), "arrival connects physically to starter storage");
        Check(!HasRoute(new Vector2(500,2800),new Vector2(1000,1200),player), "access shutter blocks early arena entry");
        Scene().GetNode<CampaignInteraction>("EmergencyProcedure").Interactable.TryInteract(player);
        Scene().GetNode<Pickup>("StarterBattery").CollectByPlayer(player);
        Scene().GetNode<CampaignInteraction>("BackupGenerator").Interactable.TryInteract(player);
        Check(StoryDirector.Instance.HasFlag("silo_backup_online"), "starter installation powers silo backup");
        Scene().GetNode<CampaignInteraction>("ContainmentRelease").Interactable.TryInteract(player);
        Check(Tree.Paused && !boss.IsEncounterActive, "encounter briefing pauses danger before releasing access");
        await CompleteCurrentDialogue();
        Check(boss.IsEncounterActive && StoryDirector.Instance.HasFlag("unit9_released"), "finished briefing activates boss encounter");
        Scene().GetNode<BlastDoor>("SiloWestDoor").Interactable.TryInteract(player);
        Scene().GetNode<BlastDoor>("SiloEastDoor").Interactable.TryInteract(player);
        await SettlePhysics();
        Check(HasRoute(new Vector2(500,2800),new Vector2(1300,1200),player), "released shutter connects preparation to combat valve");
        await VerifyBossMelee();
        boss.SetPhysicsProcess(false);
        boss.TakeDamage(300,Vector2.Zero);
        Check(boss.CurrentPhase == 2 && Scene().GetNode<Zombie>("SiloReinforcementA").Visible,
            "boss mutation activates authored reinforcements");
        Scene().GetNode<Zombie>("SiloReinforcementA").TakeDamage(999,Vector2.Zero);
        Scene().GetNode<Zombie>("SiloReinforcementB").HealthComponent.SetCurrentHealth(17);
        player.GlobalPosition = new Vector2(1300,1200);
        boss.GlobalPosition = new Vector2(950,1200);
        await SettlePhysics();
        int handgunAmmo = player.InventoryComponent.GetMagAmmo()["handgun"];
        trap.Controls.TryInteract(player); await SettlePhysics();
        Check(Mathf.IsEqualApprox(boss.HealthComponent.CurrentHealth,300), "coolant burst damages boss inside marked ring");
        Check(trap.RemainingSeconds > 20, "coolant requires pressure recovery after discharge");
        trap.Controls.TryInteract(player); await SettlePhysics();
        Check(Mathf.IsEqualApprox(boss.HealthComponent.CurrentHealth,300), "coolant cannot be spammed during recharge");
        Check(saves.Save(0,player,arena,StoryAct.Act5_Extraction), "mutated boss checkpoint saves");
        player.HealthComponent.SetCurrentHealth(0);
        Check(player.IsDead, "combat death enters actual player death state");
        await PressKey(Key.R);
        await ToSignal(Tree.CreateTimer(4),SceneTreeTimer.SignalName.Timeout);
        player = Player(); boss = Scene().GetNode<Boss_Unit9>("BossUnit9"); trap = Scene().GetNode<CoolantTrapController>("CoolantValve");
        Check(!player.IsDead && !player.IsInputLocked && !Tree.Paused, "restart input restores live checkpoint without frozen input");
        player.IsInvulnerable = true; player.IsInputLocked = true;
        Check(Mathf.IsEqualApprox(boss.HealthComponent.CurrentHealth,300) && boss.CurrentPhase == 2, "checkpoint restores boss health and mutation phase");
        Check(Scene().GetNodeOrNull<Zombie>("SiloReinforcementA") == null
            && Mathf.IsEqualApprox(Scene().GetNode<Zombie>("SiloReinforcementB").HealthComponent.CurrentHealth,17),
            "authored boss reinforcements retain deaths and damage after restart");
        Check(trap.RemainingSeconds > 15, "coolant cooldown survives death and reload");
        foreach (Node node in Tree.GetNodesInGroup("zombies")) if (node is Zombie z) z.SetPhysicsProcess(false);
        boss.MovementComponent.BaseSpeed = 0;
        player.GlobalPosition = new Vector2(1250,1200);
        for (int frame = 0; frame < 600 && boss.Combat.Phase != BossAttackPhase.AcidWindup; frame++)
        {
            await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame);
            await ToSignal(Tree,SceneTree.SignalName.ProcessFrame);
        }
        Check(boss.Combat.Phase == BossAttackPhase.AcidWindup, "mutated boss telegraphs acid volley");
        await CapturePlaytest("unit9-acid");
        player.GlobalPosition = new Vector2(1250,1600);
        await ToSignal(Tree.CreateTimer(boss.Combat.Data.AcidWindup + 0.5),SceneTreeTimer.SignalName.Timeout);
        Check(boss.Combat.Phase != BossAttackPhase.AcidWindup, "acid windup resolves and releases boss into recovery");
        boss.SetPhysicsProcess(false);
        boss.TakeDamage(100,Vector2.Zero);
        Check(boss.CurrentPhase == 3 && Mathf.IsEqualApprox(boss.AttackDamage,47.25f), "berserk mutation scales melee once");
        boss.GlobalPosition = new Vector2(950,1200); player.GlobalPosition = new Vector2(1300,1200);
        await SettlePhysics();
        if (trap.RemainingSeconds > 0)
            await ToSignal(Tree.CreateTimer(trap.RemainingSeconds + 0.2),SceneTreeTimer.SignalName.Timeout);
        await SettlePhysics();
        player.IsInputLocked = false;
        await PressKey(Key.E); await SettlePhysics();
        Check(boss.IsDead && StoryDirector.Instance.HasFlag(StoryFlags.UNIT9_DEFEATED), "recharged coolant and actual interaction input can finish boss without ammunition");
        Check(player.InventoryComponent.GetMagAmmo()["handgun"] == handgunAmmo, "coolant route consumes no weapon ammunition");
        extraction = Scene().GetNode<HelicopterExtractionController>("Extraction");
        evac = Scene().GetNode<EvacHelipad>("EvacHelipad");
        Check(!evac.Interactable.IsActive && extraction.State == ExtractionState.WaitingForBeacon, "boss defeat alone cannot skip radio and helicopter arrival");
        Scene().GetNode<BlastDoor>("CommsDoor").Interactable.TryInteract(player);
        Scene().GetNode<BlastDoor>("GeneratorNorthDoor").Interactable.TryInteract(player);
        Scene().GetNode<CampaignInteraction>("RadioBeacon").Interactable.TryInteract(player);
        Check(extraction.State == ExtractionState.Approaching && !evac.Interactable.IsActive, "radio beacon requests a real approach before boarding");
        await PressKey(Key.M);
        float approachProgress = extraction.Progress;
        await ToSignal(Tree.CreateTimer(0.3),SceneTreeTimer.SignalName.Timeout);
        Check(Tree.Paused && Mathf.IsEqualApprox(approachProgress,extraction.Progress), "map pause stops helicopter approach simulation");
        Check(!Find<PanelContainer>(Find<ExtractionStatusUI>(Scene())[0])[0].Visible,
            "extraction overlay hides while another menu owns pause");
        await PressKey(Key.M);
        Check(!Tree.Paused, "closing approach map resumes extraction simulation");
        Scene().GetNode<BlastDoor>("RoofDoor").Interactable.TryInteract(player);
        Scene().GetNode<BlastDoor>("PadDoor").Interactable.TryInteract(player);
        await SettlePhysics();
        await WalkTo(new Vector2(1700,1500));
        await WalkTo(new Vector2(2250,1500));
        await WalkTo(new Vector2(2750,1500));
        await WalkTo(new Vector2(2750,1200));
        await WalkTo(new Vector2(2500,1200));
        await WalkTo(new Vector2(2500,700));
        await WalkTo(new Vector2(2500,350));
        await WalkTo(new Vector2(1900,350));
        await WalkTo(new Vector2(1500,350));
        if (extraction.State == ExtractionState.Approaching)
            await ToSignal(Tree.CreateTimer(extraction.Data.ApproachSeconds + 0.2),SceneTreeTimer.SignalName.Timeout);
        Check(extraction.State == ExtractionState.Holding && !evac.Interactable.IsActive, "infected landing zone forces pilot to hold");
        await CapturePlaytest("helicopter-holding");
        player.GlobalPosition = new Vector2(1000,350);
        Scene().GetNode<Zombie>("RoofRunner").TakeDamage(999,Vector2.Zero);
        await SettlePhysics();
        Check(extraction.State == ExtractionState.Holding,
            $"pilot refuses to land on player occupying the pad (state={extraction.State}, player={player.GlobalPosition}, layer={player.CollisionLayer}, dead={player.IsDead})");
        player.GlobalPosition = new Vector2(1500,350); await SettlePhysics();
        Check(extraction.State == ExtractionState.Landed && evac.Interactable.IsActive && !extraction.HullCollision.Disabled,
            "cleared pad permits landing and enables real boarding door");
        Check(extraction.RotorAudio?.Playing == true && extraction.RotorAudio.Stream is AudioStreamWav { LoopMode: AudioStreamWav.LoopModeEnum.Forward },
            "landed helicopter has a running looping rotor sound");
        foreach (Node node in Tree.GetNodesInGroup("zombies")) if (node is Zombie z && !z.IsDead) z.TakeDamage(999,Vector2.Zero);
        await SettlePhysics();
        Check(saves.Save(0,player,arena,StoryAct.Act5_Extraction), "landed helicopter checkpoint saves");
        saves.Load(0); await Level(arena);
        player = Player(); extraction = Scene().GetNode<HelicopterExtractionController>("Extraction");
        evac = Scene().GetNode<EvacHelipad>("EvacHelipad");
        Check(Scene().GetNodeOrNull<Boss_Unit9>("BossUnit9") == null, "boss remains defeated after landed checkpoint reload");
        Check(extraction.State == ExtractionState.Landed && extraction.Helicopter.Visible, "landed aircraft restores without replaying approach");
        extraction.RequestBoard(player); await SettlePhysics();
        Check(extraction.State == ExtractionState.Landed && !player.IsInputLocked, "remote boarding request cannot teleport player into aircraft");
        await WalkTo(new Vector2(1160,350));
        await CapturePlaytest("helicopter-landed");
        var obstruction = new StaticBody2D { Position = new Vector2(1135,350), CollisionLayer = 1 };
        obstruction.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(4,100) } });
        Scene().AddChild(obstruction); await SettlePhysics();
        player.IsInvulnerable = false;
        await PressKey(Key.E);
        await ToSignal(Tree.CreateTimer(extraction.Data.BoardingSeconds + 0.2),SceneTreeTimer.SignalName.Timeout);
        Check(extraction.State == ExtractionState.Landed && !player.IsInputLocked && !player.IsInvulnerable && player.Visible,
            "obstructed boarding aborts safely and restores player state");
        obstruction.QueueFree(); await SettlePhysics();
        await WalkTo(new Vector2(1160,350));
        saves.CurrentSlot = 99;
        await PressKey(Key.E); await SettlePhysics();
        Check(extraction.State == ExtractionState.Boarding && player.IsInputLocked, "boarding uses a controlled physical approach");
        await PressKey(Key.M); await PressKey(Key.Escape);
        Check(!Tree.Paused && !Find<MiniMapUI>(Scene())[0].IsFullscreen, "menus cannot interrupt committed boarding flight");
        await ToSignal(Tree.CreateTimer(extraction.Data.BoardingSeconds + extraction.Data.DepartureSeconds + 0.3),SceneTreeTimer.SignalName.Timeout);
        Check(extraction.State == ExtractionState.Landed && player.Visible && !player.IsInputLocked
            && player.CollisionLayer != 0 && !player.IsInvulnerable, "failed extraction checkpoint returns aircraft and restores playable actor");
        Check(!StoryDirector.Instance.HasFlag(StoryFlags.ENDING_TRIGGERED), "failed departure cannot consume ending flag");
        saves.CurrentSlot = 0;
        await PressKey(Key.E);
        await ToSignal(Tree.CreateTimer(extraction.Data.BoardingSeconds + extraction.Data.DepartureSeconds + 4.2),SceneTreeTimer.SignalName.Timeout);
        Check(Scene().SceneFilePath == "res://Scenes/Credits.tscn", "successful boarding and takeoff transition to actual credits");
        Check(saves.ActiveSaveData.CurrentScenePath == "res://Scenes/Credits.tscn" && saves.ActiveSaveData.CurrentAct == StoryAct.Credits,
            "ending checkpoint resumes credits rather than replaying boss arena");
        Check(saves.ActiveSaveData.Flags.Contains(StoryFlags.ENDING_TRIGGERED), "completed flight persists ending commitment");
        Check(Find<Label>(Scene()).Exists(label => label.Text.StartsWith("ENDING ")), "ending survives unloading gameplay scene");
        Check(saves.Load(0), "extraction ending checkpoint reloads from disk");
        await Level("res://Scenes/Credits.tscn");
        Check(Find<Label>(Scene()).Exists(label => label.Text.StartsWith("ENDING ")), "saved ending survives a second credits load");
        string[] endingNames = { "ENDING A:", "ENDING B:", "ENDING C:", "ENDING D:" };
        for (int ending = 0; ending < endingNames.Length; ending++)
        {
            var flags = new System.Collections.Generic.HashSet<string> { StoryFlags.UNIT9_DEFEATED, StoryFlags.ENDING_TRIGGERED };
            if (ending is 0 or 2) flags.Add(StoryFlags.SADEGHI_TRUSTED);
            if (ending == 0) flags.Add(StoryFlags.LEILA_RESCUED);
            if (ending == 1) flags.Add(StoryFlags.SADEGHI_DISTRUSTED);
            if (ending == 3) flags.Add(StoryFlags.SECRET_CONSPIRACY_UNLOCKED);
            saves.ActiveSaveData.Flags = flags;
            await Level("res://Scenes/Credits.tscn");
            Check(Find<Label>(Scene()).Exists(label => label.Text.StartsWith(endingNames[ending])),
                "actual credits resolve story outcome " + endingNames[ending]);
        }
        saves.Load(0);
        await Level("res://Scenes/Credits.tscn");
    }

    private async Task VerifyBossMelee()
    {
        await ToSignal(Tree.CreateTimer(0.8),SceneTreeTimer.SignalName.Timeout);
        if (DialogueManager.Instance.IsDialogueActive) await CompleteCurrentDialogue();
        var player = Player();
        var boss = Scene().GetNode<Boss_Unit9>("BossUnit9");
        Vector2 originalPosition = player.GlobalPosition;
        float originalHealth = player.HealthComponent.CurrentHealth;
        Vector2 bossStart = boss.GlobalPosition;
        float bossSpeed = boss.MovementComponent.BaseSpeed;
        boss.MovementComponent.BaseSpeed = 0;
        player.SetPhysicsProcess(false);
        player.IsInvulnerable = false;
        player.HealthComponent.SetCurrentHealth(100);
        boss.SetPhysicsProcess(false);
        boss.GlobalPosition = new Vector2(600,550);
        player.GlobalPosition = new Vector2(690,550);
        var wall = new StaticBody2D { Position = new Vector2(645,550), CollisionLayer = 1 };
        wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(4,120) } });
        Scene().AddChild(wall);
        await SettlePhysics();
        boss.SetPhysicsProcess(true);
        for (int frame = 0; frame < 90; frame++)
        {
            await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame);
            await ToSignal(Tree,SceneTree.SignalName.ProcessFrame);
        }
        Check(Mathf.IsEqualApprox(player.HealthComponent.CurrentHealth,100), "boss melee cannot hit player through a thin wall");
        wall.QueueFree(); await SettlePhysics();
        for (int frame = 0; frame < 60 && boss.Combat.Phase != BossAttackPhase.MeleeWindup; frame++)
        {
            await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame);
            await ToSignal(Tree,SceneTree.SignalName.ProcessFrame);
        }
        Check(boss.Combat.Phase == BossAttackPhase.MeleeWindup && Mathf.IsEqualApprox(player.HealthComponent.CurrentHealth,100),
            "boss announces sweep before applying damage");
        await CapturePlaytest("unit9-sweep");
        player.GlobalPosition = boss.GlobalPosition + new Vector2(0,-90);
        await ToSignal(Tree.CreateTimer(boss.Combat.Data.MeleeWindup + 0.1),SceneTreeTimer.SignalName.Timeout);
        Check(Mathf.IsEqualApprox(player.HealthComponent.CurrentHealth,100), "moving behind locked sweep direction evades melee");
        player.GlobalPosition = new Vector2(690,550);
        player.IsInvulnerable = true;
        for (int frame = 0; frame < 90; frame++)
        {
            await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame);
            await ToSignal(Tree,SceneTree.SignalName.ProcessFrame);
        }
        Check(Mathf.IsEqualApprox(player.HealthComponent.CurrentHealth,100), "boss melee respects player invulnerability");
        player.IsInvulnerable = false;
        for (int frame = 0; frame < 120 && Mathf.IsEqualApprox(player.HealthComponent.CurrentHealth,100); frame++)
        {
            await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame);
            await ToSignal(Tree,SceneTree.SignalName.ProcessFrame);
        }
        Check(Mathf.IsEqualApprox(player.HealthComponent.CurrentHealth,100 - boss.AttackDamage), "unobstructed boss melee applies expected damage");
        player.IsInvulnerable = true;
        player.GlobalPosition = originalPosition;
        player.HealthComponent.SetCurrentHealth(originalHealth);
        player.SetPhysicsProcess(true);
        boss.Combat.Cancel();
        boss.GlobalPosition = bossStart;
        boss.MovementComponent.BaseSpeed = bossSpeed;
    }

    private async Task VerifyDifficulty(SaveSystem saves, string scenePath)
    {
        int previousSlot = saves.CurrentSlot;
        Check(saves.Save(2,Player(),scenePath,StoryAct.Act4_Descent), "difficulty test preserves its starting checkpoint");
        float baseDamage = GD.Load<EnemyStats>("res://Resources/Enemies/Zombie_Walker.tres").AttackDamage;
        var difficulties = new[] { DifficultyLevel.Survivor, DifficultyLevel.Nightmare, DifficultyLevel.Researcher };
        float[] multipliers = { 1.0f, 1.35f, 1.8f };
        for (int i = 0; i < difficulties.Length; i++)
        {
            saves.Load(2);
            saves.ActiveSaveData.Difficulty = difficulties[i];
            await Level(scenePath);
            var player = Player();
            var enemy = Scene().GetNode<Zombie>("PaperWalker");
            player.SetPhysicsProcess(false);
            enemy.SetPhysicsProcess(false);
            player.IsInvulnerable = false;
            player.HealthComponent.SetCurrentHealth(100);
            enemy.GlobalPosition = player.GlobalPosition - new Vector2(40,0);
            enemy.Rotation = 0;
            await SettlePhysics();
            Check(Mathf.IsEqualApprox(enemy.Stats.AttackDamage,baseDamage), "shared enemy definition remains immutable on " + difficulties[i]);
            enemy.PerformAttack();
            Check(Mathf.IsEqualApprox(player.HealthComponent.CurrentHealth,100 - baseDamage * multipliers[i]),
                "actual melee damage uses actor difficulty on " + difficulties[i]);
        }
        saves.Load(2);
        await Level(scenePath);
        saves.CurrentSlot = previousSlot;
    }

    private async Task VerifyProjectiles()
    {
        var player = Player();
        Vector2 originalPosition = player.GlobalPosition;
        float originalHealth = player.HealthComponent.CurrentHealth;
        bool originalInvulnerability = player.IsInvulnerable;
        player.SetPhysicsProcess(false);
        player.GlobalPosition = new Vector2(500,2600);
        player.HealthComponent.SetCurrentHealth(100);
        var zombie = GD.Load<PackedScene>("res://Prefabs/Zombie.tscn").Instantiate<Zombie>();
        Scene().AddChild(zombie);
        zombie.SetPhysicsProcess(false);
        zombie.GlobalPosition = new Vector2(650,2600);
        float enemyHealth = zombie.HealthComponent.CurrentHealth;
        var wall = new StaticBody2D { Position = new Vector2(580,2600), CollisionLayer = 1 };
        wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(4,120) } });
        Scene().AddChild(wall);
        await SettlePhysics();
        var pool = ProjectilePool.Instance;
        var bullet = pool.Spawn(player.GlobalPosition,0,Scene(),25,18000);
        await SettlePhysics();
        Check(Mathf.IsEqualApprox(zombie.HealthComponent.CurrentHealth,enemyHealth), "high speed player shot cannot tunnel through thin wall behind shooter");
        Check(!bullet.IsActive && bullet.GetParent() == pool, "wall impact returns bullet to pool after physics signal");
        wall.QueueFree(); await SettlePhysics();
        bullet = pool.Spawn(player.GlobalPosition,0,Scene(),25,18000);
        await SettlePhysics();
        Check(Mathf.IsEqualApprox(zombie.HealthComponent.CurrentHealth,enemyHealth - 25), "continuous player shot ignores shooter then damages enemy exactly once");
        Check(!bullet.IsActive && bullet.GetParent() == pool, "enemy impact safely recycles projectile");
        zombie.GlobalPosition = new Vector2(350,2600);
        await SettlePhysics();
        player.IsInvulnerable = true;
        bullet = pool.Spawn(zombie.GlobalPosition,0,Scene(),10,18000,true);
        await SettlePhysics();
        Check(Mathf.IsEqualApprox(player.HealthComponent.CurrentHealth,100), "acid projectile respects player invulnerability");
        Check(!bullet.IsActive, "invulnerable player still absorbs incoming projectile");
        player.IsInvulnerable = false;
        bullet = pool.Spawn(zombie.GlobalPosition,0,Scene(),10,18000,true);
        await SettlePhysics();
        Check(Mathf.IsEqualApprox(player.HealthComponent.CurrentHealth,90), "acid shot skips friendly infected and damages player exactly once");
        Check(Mathf.IsEqualApprox(zombie.HealthComponent.CurrentHealth,enemyHealth - 25), "enemy projectiles cannot damage their shooter");
        zombie.QueueFree();
        player.GlobalPosition = originalPosition;
        player.HealthComponent.SetCurrentHealth(originalHealth);
        player.IsInvulnerable = originalInvulnerability;
        player.SetPhysicsProcess(true);
        await SettlePhysics();
    }

    private async Task VerifyArchives(SaveSystem saves)
    {
        const string archive = "res://Scenes/Act4_SubBasement.tscn";
        await VerifyDifficulty(saves,archive);
        var player = Player();
        player.IsInputLocked = true;
        player.IsInvulnerable = true;
        var story = StoryDirector.Instance;
        await VerifyProjectiles();
        Check(CampaignActDirector.Instance.Rooms.Count == 9, "archive has nine authored mapped spaces");
        Check(CampaignActDirector.Instance.ActiveObjective?.CompletionFlag == "archive_index_read", "archive begins with records investigation");
        Check(!Scene().GetNode<ActTransitionTrigger>("Act5Trigger").TryTransition(player), "surface exit cannot bypass lift repairs");
        Check(!Scene().GetNode<CampaignInteraction>("ProtocolTerminal").Interactable.TryInteract(player), "protocol requires index and research drive");
        Check(!Scene().GetNode<CampaignInteraction>("Sadeghi").Interactable.TryInteract(player), "confrontation requires actual evidence");
        Check(!Scene().GetNode<CampaignInteraction>("HydraulicPump").Interactable.TryInteract(player), "hydraulic return requires counterweight release");
        Check(HasRoute(new Vector2(500,2800), new Vector2(300,1450),player), "arrival physically connects to paper index");
        Check(!HasRoute(new Vector2(1500,1400),new Vector2(2500,400),player), "unprepared freight platform has no bypass route");
        Scene().GetNode<CampaignInteraction>("IndexDesk").Interactable.TryInteract(player);
        Scene().GetNode<BlastDoor>("RecordsDoor").Interactable.TryInteract(player);
        await SettlePhysics();
        Check(HasRoute(new Vector2(300,1450),new Vector2(1500,1400),player), "index opens central records route");
        Check(!Scene().GetNode<Zombie>("RecordsReturnRunner").Visible, "archive return encounter begins dormant");
        Scene().GetNode<CampaignInteraction>("ProtocolTerminal").Interactable.TryInteract(player);
        Check(story.HasFlag(StoryFlags.OUTBREAK_PROTOCOL_REVEALED), "research authentication reveals release protocol");
        Check(Scene().GetNode<Zombie>("RecordsReturnRunner").Visible, "protocol activates authored return encounter");
        Scene().GetNode<BlastDoor>("CommandDoor").Interactable.TryInteract(player);
        Scene().GetNode<BlastDoor>("ShelterDoor").Interactable.TryInteract(player);
        await SettlePhysics();
        Check(HasRoute(new Vector2(300,1450),new Vector2(350,350),player), "protocol grants command office access");
        Check(HasRoute(new Vector2(1500,1400),new Vector2(1500,350),player), "protocol grants shelter access");
        Scene().GetNode<BlastDoor>("VaultDoor").Interactable.TryInteract(player);
        Check(!Scene().GetNode<BlastDoor>("VaultDoor").IsOpen, "optional vault requires separate credential");
        Scene().GetNode<Pickup>("VaultCredential").CollectByPlayer(player);
        Scene().GetNode<BlastDoor>("VaultDoor").Interactable.TryInteract(player);
        await SettlePhysics();
        Check(HasRoute(new Vector2(1500,1400),new Vector2(2700,1300),player), "command credential opens optional evidence vault");
        Scene().GetNode<Pickup>("GhorbaniFilesPickup").CollectByPlayer(player);
        Check(Scene().GetNode<Pickup>("VaultRifle").CollectByPlayer(player)
            && player.InventoryComponent.IsWeaponUnlocked("rifle"),"optional command vault grants its actual rifle");
        Check(player.InventoryComponent.HasItem("ghorbani_files"), "black dossier is a persistent mission item");
        Check(!Scene().GetNode<CampaignInteraction>("EvidenceDesk").Interactable.TryInteract(player), "dossier alone cannot decode secret conspiracy");
        Scene().GetNode<Zombie>("PaperWalker").TakeDamage(999,Vector2.Zero);
        Scene().GetNode<Zombie>("RecordsWalker").HealthComponent.SetCurrentHealth(19);
        player.GlobalPosition = new Vector2(1500,750);
        Check(saves.Save(1,player,archive,StoryAct.Act4_Descent), "pre-confrontation checkpoint saves all exploration");
        var confrontation = Scene().GetNode<CampaignInteraction>("Sadeghi");
        confrontation.Interactable.TryInteract(player);
        await AdvanceToChoices();
        Check(DialogueManager.Instance.ChoicesContainer.GetChildCount() == 2, "secret answer remains hidden without decoded evidence");
        await PressKey(Key.Key2);
        Check(story.HasFlag(StoryFlags.SADEGHI_DISTRUSTED), "selected distrust answer is staged during conversation");
        DialogueManager.Instance.CancelDialogue();
        Check(!story.HasFlag(StoryFlags.SADEGHI_DISTRUSTED) && !story.HasFlag(StoryFlags.SADEGHI_CONFRONTED), "cancelling chosen answer rolls back flags and confrontation completion");
        Check(confrontation.Interactable.IsActive, "cancelled confrontation remains available");
        confrontation.Interactable.TryInteract(player);
        await CompleteCurrentDialogue(2);
        Check(story.HasFlag(StoryFlags.SADEGHI_DISTRUSTED) && !story.HasFlag(StoryFlags.SADEGHI_TRUSTED), "completed distrust branch has an exclusive outcome");
        Check(story.HasFlag(StoryFlags.SADEGHI_CONFRONTED), "finishing confrontation unlocks lift repair route");
        saves.Load(1);
        await Level(archive);
        player = Player(); player.IsInputLocked = true; player.IsInvulnerable = true; story = StoryDirector.Instance;
        Check(Scene().GetNodeOrNull<Zombie>("PaperWalker") == null, "archive defeated actor persists");
        Check(Mathf.IsEqualApprox(Scene().GetNode<Zombie>("RecordsWalker").HealthComponent.CurrentHealth,19), "archive wounded actor persists");
        Check(Scene().GetNodeOrNull<Pickup>("GhorbaniFilesPickup") == null && player.InventoryComponent.HasItem("ghorbani_files"), "vault evidence cannot duplicate after reload");
        Check(player.InventoryComponent.IsWeaponUnlocked("rifle") && Scene().GetNodeOrNull<Pickup>("VaultRifle") == null,
            "vault rifle ownership and collection survive checkpoint reload");
        player.InventoryComponent.AddItem(GD.Load<ItemData>("res://Resources/Items/Archivist_Journal.tres"));
        Scene().GetNode<CampaignInteraction>("EvidenceDesk").Interactable.TryInteract(player);
        Check(story.HasFlag("archive_correlated_evidence"), "journal and dossier unlock corroborated evidence");
        Scene().GetNode<CampaignInteraction>("Sadeghi").Interactable.TryInteract(player);
        await AdvanceToChoices();
        Check(DialogueManager.Instance.ChoicesContainer.GetChildCount() == 3, "corroborated evidence exposes secret answer");
        await CompleteCurrentDialogue(3);
        Check(story.HasFlag(StoryFlags.SECRET_CONSPIRACY_UNLOCKED) && !story.HasFlag(StoryFlags.SADEGHI_TRUSTED)
            && !story.HasFlag(StoryFlags.SADEGHI_DISTRUSTED), "secret confrontation branch excludes ordinary outcomes");
        saves.Load(1);
        await Level(archive);
        player = Player(); player.IsInputLocked = true; player.IsInvulnerable = true; story = StoryDirector.Instance;
        Scene().GetNode<CampaignInteraction>("Sadeghi").Interactable.TryInteract(player);
        await CompleteCurrentDialogue(1);
        Check(story.HasFlag(StoryFlags.SADEGHI_TRUSTED) && !story.HasFlag(StoryFlags.SADEGHI_DISTRUSTED)
            && !story.HasFlag(StoryFlags.SECRET_CONSPIRACY_UNLOCKED), "trust branch is independent of other checkpoint branches");
        Scene().GetNode<CampaignInteraction>("CounterweightLock").Interactable.TryInteract(player);
        Scene().GetNode<BlastDoor>("HydraulicDoor").Interactable.TryInteract(player);
        Scene().GetNode<CampaignInteraction>("HydraulicPump").Interactable.TryInteract(player);
        var startup = Scene().GetNode<SequencePanel>("LiftStartup");
        startup.Interactable.TryInteract(player);
        var view = Find<SequencePanelUI>(startup)[0];
        var buttons = Find<Button>(view);
        Check(Tree.Paused && view.IsOpen, "lift startup pauses combat while reading controls");
        await CapturePlaytest("archive-startup");
        await PressKey(Key.M);
        Check(Tree.Paused && view.IsOpen && !Find<MiniMapUI>(Scene())[0].IsFullscreen, "map input cannot replace startup panel pause");
        await PressKey(Key.Enter);
        Check(startup.AcceptedSteps == 0 && !story.HasFlag("archive_lift_ready"), "enabling drive first trips startup interlock");
        buttons[1].EmitSignal(Button.SignalName.Pressed);
        Check(startup.AcceptedSteps == 1, "pressurizing circuit is the first safe startup step");
        view.Close();
        Check(player.IsInputLocked && !Tree.Paused, "sequence view preserves existing player input lock");
        startup.Interactable.TryInteract(player);
        buttons = Find<Button>(view);
        Check(startup.AcceptedSteps == 0, "reopening startup clears an unfinished sequence");
        buttons[1].EmitSignal(Button.SignalName.Pressed);
        buttons[2].EmitSignal(Button.SignalName.Pressed);
        buttons[0].EmitSignal(Button.SignalName.Pressed);
        Check(story.HasFlag("archive_lift_ready") && !view.IsOpen && !Tree.Paused, "safe startup sequence enables freight platform");
        Scene().GetNode<BlastDoor>("FreightDoor").Interactable.TryInteract(player);
        foreach (Node node in Tree.GetNodesInGroup("zombies")) if (node is Zombie z && !z.IsDead) z.TakeDamage(999,Vector2.Zero);
        await SettlePhysics();
        Check(CampaignActDirector.Instance.ActiveObjective == null, "all mandatory archive objectives complete");
        Check(saves.Save(0,player,archive,StoryAct.Act4_Descent), "prepared archive lift saves");
        saves.Load(0); await Level(archive);
        player = Player(); player.IsInvulnerable = true;
        Check(!Scene().GetNode<SequencePanel>("LiftStartup").Interactable.IsActive, "completed lift startup remains complete after reload");
        Check(Scene().GetNode<BlastDoor>("FreightDoor").IsOpen, "freight access persists after reload");
        player.GlobalPosition = new Vector2(500,2800);
        await WalkTo(new Vector2(500,2500));
        await WalkTo(new Vector2(1400,2500));
        await WalkTo(new Vector2(2600,2500));
        await WalkTo(new Vector2(1500,2500));
        await WalkTo(new Vector2(1500,1500));
        await WalkTo(new Vector2(1400,1200));
        await WalkTo(new Vector2(1500,1200));
        await WalkTo(new Vector2(1500,700));
        await WalkTo(new Vector2(1700,700));
        await WalkTo(new Vector2(1700,500));
        await WalkTo(new Vector2(2200,500));
        await WalkTo(new Vector2(2500,400));
        await CapturePlaytest("archive-platform");
        await PressKey(Key.E);
        await ToSignal(Tree.CreateTimer(4),SceneTreeTimer.SignalName.Timeout);
        Check(Scene().SceneFilePath == "res://Scenes/Act5_Arena.tscn", "physical archive traversal and freight controls reach extraction");
        Check(Player().InventoryComponent.HasItem("research_drive") && StoryDirector.Instance.HasFlag(StoryFlags.SADEGHI_TRUSTED), "freight transition retains evidence and confrontation outcome");
    }

    private async Task AdvanceToChoices()
    {
        var dialogue = DialogueManager.Instance;
        for (int i = 0; i < 30 && dialogue.IsDialogueActive && !dialogue.ChoicesContainer.Visible; i++) await PressKey(Key.E);
        Check(dialogue.IsDialogueActive && dialogue.ChoicesContainer.Visible, "conversation reaches selectable answers");
    }

    private async Task CompleteCurrentDialogue(int option = 1)
    {
        var keys = new[] { Key.Key1, Key.Key2, Key.Key3, Key.Key4 };
        for (int i = 0; i < 50 && DialogueManager.Instance.IsDialogueActive; i++)
        {
            var dialogue = DialogueManager.Instance;
            bool choices = dialogue.ChoicesContainer.Visible && dialogue.ChoicesContainer.GetChildCount() > 0;
            await PressKey(choices ? keys[option - 1] : Key.E);
        }
        Check(!DialogueManager.Instance.IsDialogueActive && !Tree.Paused, "interaction dialogue completes without freezing gameplay");
    }

    private async Task VerifyDialogueSafety()
    {
        // Wait for the real chapter intro, including the fade and delayed dialogue.
        await ToSignal(Tree.CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
        var dialogue = DialogueManager.Instance;
        var player = Player();
        Check(dialogue.IsDialogueActive && Tree.Paused, "Act 2 conversation pauses gameplay");
        Check(Input.MouseMode == Input.MouseModeEnum.Visible, "conversation exposes choice cursor");
        var guard = Scene().GetNode<Zombie>("BarracksGuard1");
        Vector2 guardPosition = guard.GlobalPosition;
        guard.GlobalPosition = player.GlobalPosition + new Vector2(48, 0);
        guard.StateMachine.ChangeState(guard.AttackState);
        float health = player.HealthComponent.CurrentHealth;
        Vector2 position = player.GlobalPosition;
        await ToSignal(Tree.CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
        Check(Mathf.IsEqualApprox(player.HealthComponent.CurrentHealth, health), "adjacent attacking zombie cannot damage player during dialogue");
        Check(player.GlobalPosition == position, "dialogue does not allow player displacement");
        Check(Find<RichTextLabel>(dialogue)[0].Text.Length > 0, "typewriter continues while gameplay is paused");
        foreach (Key key in new[] { Key.Tab, Key.M, Key.Escape }) await PressKey(key);
        Check(Tree.Paused && !Find<InventoryUI>(Scene())[0].Visible && !Find<PauseMenuUI>(Scene())[0].Visible
            && !Find<MiniMapUI>(Scene())[0].IsFullscreen, "other menus cannot steal dialogue pause");
        guard.GlobalPosition = guardPosition;
        for (int i = 0; i < 40 && dialogue.IsDialogueActive; i++)
        {
            bool choices = dialogue.ChoicesContainer.Visible && dialogue.ChoicesContainer.GetChildCount() > 0;
            await PressKey(choices ? Key.Key1 : Key.E);
        }
        Check(!dialogue.IsDialogueActive && !Tree.Paused && !player.IsInputLocked,
            "dialogue and choice completion release gameplay");
        Check(StoryDirector.Instance.HasFlag("intro_seen_act2_checkpoint"), "completed intro is remembered");
        Input.ActionPress("move_right");
        await ToSignal(Tree.CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        Input.ActionRelease("move_right");
        Check(player.GlobalPosition.X > position.X + 10, "movement resumes after conversation");
        // Normal movement and a dash must both respect the campaign perimeter.
        player.GlobalPosition = new Vector2(80, 2300);
        Input.ActionPress("move_left");
        await ToSignal(Tree.CreateTimer(0.08), SceneTreeTimer.SignalName.Timeout);
        Input.ActionPress("dash");
        await ToSignal(Tree.CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
        Input.ActionRelease("dash");
        await ToSignal(Tree.CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        Input.ActionRelease("move_left");
        Check(player.GlobalPosition.X >= 48, "dash cannot leave the bunker perimeter");
        player.TakeDamage(1, Vector2.Left);
        await ToSignal(Tree.CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
        Check(player.GlobalPosition.X >= 48, "knockback cannot push player outside the bunker");
        player.HealthComponent.SetCurrentHealth(health);
        player.GlobalPosition = new Vector2(570, 2800);
        guard.GlobalPosition = new Vector2(690, 2800);
        guard.FinisherTarget.TriggerStun(10);
        var corner = new StaticBody2D { Position = new Vector2(620, 2850), CollisionLayer = 1 };
        corner.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(20, 60) } });
        Scene().AddChild(corner);
        await SettlePhysics();
        Check(player.TryExecuteFinisher(), "finisher can begin beside wall corner");
        for (int frame = 0; frame < 120 && player.StateMachine.CurrentState is PlayerFinisherState; frame++)
        {
            await ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            await ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
        }
        Check(player.GlobalPosition.X < 600, "finisher hull stops at wall corner");
        Check(player.StateMachine.CurrentState is not PlayerFinisherState && Mathf.IsEqualApprox((float)Engine.TimeScale, 1)
            && !Tree.Paused && !player.IsInvulnerable,
            $"finisher returns to normal gameplay without freezing (state={player.StateMachine.CurrentState.GetType().Name}, scale={Engine.TimeScale})");
        corner.QueueFree();
        player.GlobalPosition = position;
        await SettlePhysics();
        var sample = new DialogueSequence();
        sample.Lines.Add(new DialogueLineResource { Text = "Pause ownership regression check." });
        Tree.Paused = true;
        player.IsInputLocked = true;
        dialogue.StartDialogue(sample);
        dialogue.EndDialogue();
        Check(Tree.Paused && player.IsInputLocked, "dialogue preserves a pre-existing pause and input lock");
        Tree.Paused = false;
        player.IsInputLocked = false;
        dialogue.StartDialogue(sample);
        dialogue.CancelDialogue();
        Check(!Tree.Paused && !player.IsInputLocked, "cancelled conversation releases its gameplay lock");
        Check(Find<Control>(dialogue).Find(control => control.Name == "DialogueRoot").MouseFilter == Control.MouseFilterEnum.Ignore,
            "hidden dialogue root cannot block other menu clicks");
    }

    private async Task PressKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        await ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
    }

    private Node Scene() => Tree.CurrentScene;
    private Player Player() => Tree.GetFirstNodeInGroup("player") as Player;
    private async Task Level(string path)
    {
        Check(Tree.ChangeSceneToFile(path) == Error.Ok, "scene loads: " + path);
        for (int i = 0; i < 5; i++) await ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
        if (Array.Exists(OS.GetCmdlineUserArgs(),arg => arg == "--stress-gc"))
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await ToSignal(Tree,SceneTree.SignalName.ProcessFrame);
        }
    }
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
        GD.Print("PASS " + message);
    }

    private bool HasRoute(Vector2 from, Vector2 to, Player player)
    {
        const int step = 40;
        var origin = new Vector2I(Mathf.RoundToInt(from.X / step), Mathf.RoundToInt(from.Y / step));
        var target = new Vector2I(Mathf.RoundToInt(to.X / step), Mathf.RoundToInt(to.Y / step));
        var seen = new System.Collections.Generic.HashSet<Vector2I> { origin };
        var queue = new System.Collections.Generic.Queue<Vector2I>();
        queue.Enqueue(origin);
        var directions = new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = new CircleShape2D { Radius = 22 }, CollisionMask = 1,
            Exclude = new Godot.Collections.Array<Rid> { player.GetRid() }
        };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == target) return true;
            foreach (var direction in directions)
            {
                var next = current + direction;
                if (next.X < 1 || next.Y < 1 || next.X > 74 || next.Y > 74 || !seen.Add(next)) continue;
                query.Transform = new Transform2D(0, new Vector2(next.X * step, next.Y * step));
                if (player.GetWorld2D().DirectSpaceState.IntersectShape(query, 1).Count == 0) queue.Enqueue(next);
            }
        }
        return false;
    }

    private async Task SettlePhysics()
    {
        for (int i = 0; i < 2; i++)
        {
            await ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            await ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
        }
    }
    private static System.Collections.Generic.List<T> Find<T>(Node parent) where T : Node
    {
        var found = new System.Collections.Generic.List<T>();
        foreach (var child in parent.GetChildren())
        {
            if (child is T match) found.Add(match);
            found.AddRange(Find<T>(child));
        }
        return found;
    }
}
