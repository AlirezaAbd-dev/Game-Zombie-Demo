using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.UI;

namespace TopDownDemo.Systems.Tests;

/// <summary>Authoring/lighting checks plus rendered room views; isolated XDG profile required.</summary>
public partial class EnvironmentSmokeTest : Node
{
    private int _checks;
    private readonly HashSet<string> _areaCodes = new();
    private readonly HashSet<string> _materials = new();
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
            saves.ActiveSaveData.Flags.UnionWith(new[] { "intro_seen_act1_gate", "intro_seen_act2_checkpoint" });
            await Level("res://Scenes/Act1_Gate.tscn");
            await Capture("environment-arrival-dark",new Vector2(500,2800));
            StoryDirector.Instance.SetFlag(StoryFlags.FACILITY_POWER_RESTORED);
            saves.ActiveSaveData.Flags.Add(StoryFlags.FACILITY_POWER_RESTORED);
            await Capture("environment-arrival",new Vector2(500,2800));
            await Capture("environment-generator",new Vector2(2500,500));
            await VerifyResponseVehicle();
            await Level("res://Scenes/Act2_Checkpoint.tscn");
            await Capture("environment-transfer",new Vector2(500,2500));
            await Capture("environment-barracks",new Vector2(1500,2350));
            await Level("res://Scenes/Transit_Decontamination.tscn");
            await Capture("environment-decontamination",new Vector2(700,1400));
            await Level("res://Scenes/Act3_Lab.tscn");
            await Capture("environment-triage",new Vector2(500,1500));
            await Level("res://Scenes/Act4_SubBasement.tscn");
            await Capture("environment-archives",new Vector2(500,1300));
            await Level("res://Scenes/Act5_Arena.tscn");
            await Capture("environment-silo",new Vector2(1000,1300));
            await Capture("environment-roof",new Vector2(1000,350));
            Check(_areaCodes.Count == 45,"every campaign room has a distinct authored area code");
            Check(_materials.Count == 7,"campaign uses all seven authored floor palettes");
            GD.Print($"ENVIRONMENT SMOKE PASS: {_checks} checks");
            Tree.CurrentScene.QueueFree();
            await ToSignal(Tree.CreateTimer(0.3),SceneTreeTimer.SignalName.Timeout);
            Tree.Quit();
        }
        catch (Exception error)
        {
            GD.PushError("ENVIRONMENT SMOKE FAIL: " + error);
            if (IsInstanceValid(Tree.CurrentScene)) Tree.CurrentScene.QueueFree();
            await ToSignal(Tree.CreateTimer(0.3),SceneTreeTimer.SignalName.Timeout);
            Tree.Quit(1);
        }
    }

    private async Task Level(string path)
    {
        Tree.Paused = true; // Lock the fixture before the newly loaded player's first input tick.
        Check(Tree.ChangeSceneToFile(path) == Error.Ok,"environment fixture loads: " + path);
        await Settle();
        var director = CampaignActDirector.Instance;
        var view = director.GetChildren().OfType<CampaignEnvironmentView>().Single();
        Check(view.StyledFloors == director.Rooms.Count && view.StyledWalls == Tree.GetNodesInGroup("campaign_walls").Count,
            "all room floors and rectangular walls receive their authored presentation");
        Check(Tree.GetNodesInGroup("campaign_floor").OfType<ColorRect>().All(floor => floor.ZIndex == -10)
            && view.GetChildren().OfType<CampaignRoomMarkingsView>().All(marking => marking.ZIndex == -9),
            "floor surfaces and paint stay below actors and combat telegraphs");
        Check(Tree.GetNodesInGroup("campaign_room_label").OfType<CanvasItem>().All(label => !label.Visible),
            "replacement room stencils suppress redundant prototype labels");
        foreach (var room in director.Rooms)
        {
            Check(room.VisualProfile?.FloorMaterial?.Shader?.ResourcePath == "res://Assets/shaders/facility_surface.gdshader"
                && !string.IsNullOrWhiteSpace(room.AreaCode),"authored material and area code: " + room.Title);
            _areaCodes.Add(room.AreaCode);
            _materials.Add(room.VisualProfile.FloorMaterial.ResourcePath);
        }
        Actor.IsInputLocked = true; Actor.IsInvulnerable = true;
        Actor.SetProcess(false);
        Actor.Camera.PositionSmoothingEnabled = false;
        Actor.Camera.Offset = Vector2.Zero;
        Actor.Camera.Zoom = Vector2.One;
        foreach (Node node in Tree.GetNodesInGroup("zombies")) node.SetPhysicsProcess(false);
        Tree.Paused = false;
        GC.Collect(); GC.WaitForPendingFinalizers();
    }

    private async Task VerifyResponseVehicle()
    {
        Actor.GlobalPosition = new Vector2(420,2650);
        Actor.IsInputLocked = false; Actor.SetProcess(true);
        await Settle();
        var vehicle = Tree.CurrentScene.GetNode<CampaignInteraction>("ResponseVehicle");
        Check(Actor.InteractionComponent.CurrentFocus == vehicle.Interactable,
            "response vehicle remains reachable beside its physical body");
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
        await Settle();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = false });
        await Settle();
        Check(StoryDirector.Instance.HasFlag("response_vehicle_examined"),
            "real interaction input reads the vehicle's introductory field note");
        Actor.IsInputLocked = true; Actor.SetProcess(false);
    }

    private async Task Capture(string name,Vector2 position)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture-playtest")) return;
        Actor.GlobalPosition = position; Actor.GlobalRotation = 0;
        Actor.Camera.ResetSmoothing(); Actor.Camera.ForceUpdateScroll();
        await Settle();
        RenderingServer.ForceDraw(false);
        using var screenshot = GetViewport().GetTexture().GetImage();
        Check(screenshot.SavePng($"/tmp/foxtrot-{name}.png") == Error.Ok,"rendered " + name);
    }

    private async Task Settle()
    {
        for (int i = 0; i < 5; i++)
        { await ToSignal(Tree,SceneTree.SignalName.PhysicsFrame); await ToSignal(Tree,SceneTree.SignalName.ProcessFrame); }
    }
    private void Check(bool condition,string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("PASS " + message);
    }
}
