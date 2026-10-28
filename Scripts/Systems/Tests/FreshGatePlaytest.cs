using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.UI;

namespace TopDownDemo.Systems.Tests;

/// <summary>A known-route opening baseline. Uses real inputs, normal stats and live enemies.
/// Ideal aim and prior route/puzzle knowledge make this a lower bound, not a first-time pacing study.</summary>
public partial class FreshGatePlaytest : Node
{
    private Player _actor;
    private Vector2 _destination;
    private bool _walking;
    private bool _running;
    private string _failure;
    private string _step = "launch";
    private int _dialogueLine = -1;
    private float _readingSeconds;
    private float _healCooldown;
    private float _combatReportCooldown;
    private readonly Stopwatch _elapsed = new();
    private readonly PhysicsRayQueryParameters2D _sight = new() { CollisionMask = 1 | 2 };
    private readonly Godot.Collections.Array<Rid> _exclude = new();
    private readonly PhysicsShapeQueryParameters2D _shotClearance = new() { CollisionMask = 1, Shape = new CircleShape2D { Radius = 10 } };
    private SceneTree Tree => GetTree();
    public override void _Ready() => Callable.From(Run).CallDeferred();

    private async void Run()
    {
        try
        {
            Tree.CurrentScene = null;
            var saves = SaveSystem.EnsureInstance(this);
            saves.CreateNewGame(0, DifficultyLevel.Survivor);
            var preferences = PlayerSettings.Current.Copy();
            preferences.CameraLookAhead = false; preferences.CameraShake = 0;
            PlayerSettings.Apply(preferences);
            if (Tree.ChangeSceneToFile("res://Scenes/Act1_Gate.tscn") != Error.Ok) throw new InvalidOperationException("Opening failed to load");
            await Wait(0.15);
            _actor = (Player)Tree.GetFirstNodeInGroup("player");
            if (_actor.IsInvulnerable || _actor.IsInputLocked || _actor.MovementComponent.BaseSpeed != 300)
                throw new InvalidOperationException("Baseline must begin with normal player settings");
            _exclude.Add(_actor.GetRid()); _sight.Exclude = _exclude; _shotClearance.Exclude = _exclude;
            _elapsed.Start(); _running = true;
            await Walk(650, 2650);
            await Walk(420, 2650);
            await Interact("response vehicle", () => StoryDirector.Instance.HasFlag("response_vehicle_examined"));
            await Walk(500, 2150); await Walk(500, 1850); await Walk(400, 1600);
            await Interact("security log", () => StoryDirector.Instance.HasFlag("gate_log_read"));
            await Walk(650, 1400); await Walk(650, 1300);
            Require(_actor.InventoryComponent.HasItem("keycard_substation"), "maintenance credential collected by normal proximity");
            await Walk(650, 1500); await Walk(1500, 1500); await Walk(2200, 1500);
            await Walk(2700, 1600);
            Require(_actor.InventoryComponent.HasItem("auxiliary_fuse"), "fuse collected by normal proximity");
            // The maintenance cage blocks the direct northward line; use its east aisle.
            await Walk(2860, 1600); await Walk(2860, 1200); await Walk(2500, 1100);
            await Interact("generator access", () => Find<BlastDoor>("GeneratorAccess").IsOpen);
            await Walk(2500, 850); await Walk(2600, 350);
            await Interact("fuse installation", () => StoryDirector.Instance.HasFlag("auxiliary_fuse_installed"));
            await Walk(2400, 500); await Walk(1900, 500); await Walk(1450, 380);
            await Interact("power panel", () => Tree.Paused && All<PowerRoutingUI>(Tree.CurrentScene).Any());
            var view = All<PowerRoutingUI>(Tree.CurrentScene).First();
            var switches = All<CheckButton>(view).ToArray();
            await Wait(3); // Deliberate known-solution UI input, without changing story flags directly.
            switches[0].ButtonPressed = true; switches[1].ButtonPressed = true;
            All<Button>(view).Single(button => button.Text == "APPLY ROUTING").EmitSignal(BaseButton.SignalName.Pressed);
            Require(StoryDirector.Instance.HasFlag(StoryFlags.FACILITY_POWER_RESTORED), "real routing UI restores power");
            await Walk(1100, 500);
            await Interact("relay shortcut", () => Find<BlastDoor>("RelayShortcut").IsOpen);
            await Walk(800, 500); await Walk(450, 380);
            await Interact("bunker override", () => StoryDirector.Instance.HasFlag(StoryFlags.GATE_OVERRIDDEN));
            await Walk(500, 200);
            _walking = false;
            int shots = StoryDirector.Instance.Stats.ShotsFired;
            int kills = StoryDirector.Instance.Stats.TotalKills;
            float health = _actor.HealthComponent.CurrentHealth;
            int rounds = _actor.InventoryComponent.CurrentAmmo + _actor.InventoryComponent.CurrentReserve;
            double gateSeconds = _elapsed.Elapsed.TotalSeconds;
            var timing = saves.ActiveSaveData.SceneTimings["res://Scenes/Act1_Gate.tscn"];
            float active = timing.ActiveSeconds, dialogue = timing.DialogueSeconds;
            await Interact("bunker departure", () => SceneTransitionManager.Instance.IsTransitioning);
            for (int i = 0; i < 120 && (Tree.CurrentScene?.SceneFilePath != "res://Scenes/Act2_Checkpoint.tscn" || SceneTransitionManager.Instance.IsTransitioning); i++) await Wait(0.1);
            Require(Tree.CurrentScene?.SceneFilePath == "res://Scenes/Act2_Checkpoint.tscn" && !Tree.Paused, "normal opening run reaches bunker with responsive gameplay");
            _running = false; ControlBindings.ReleaseGameplayActions();
            var report = new { Scenario = "Gate known-route baseline", Difficulty = "Survivor", Cheats = false,
                PriorRouteKnowledge = true, IdealAim = true, CameraLookAhead = false, CameraShake = 0, AssumedReadingCharactersPerSecond = 16,
                ElapsedSeconds = gateSeconds, ActiveSeconds = active, DialogueSeconds = dialogue,
                ShotsFired = shots, Kills = kills, RemainingHealth = health, RemainingHandgunRounds = rounds,
                Limitation = "Opening chapter only; known route, ideal aim and known puzzle solution. Not a first-time or one-hour pacing validation." };
            string path = ProjectSettings.GlobalizePath("user://fresh-gate-result.json");
            File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print("FRESH GATE RESULT: " + JsonSerializer.Serialize(report));
            GD.Print("FRESH GATE PASS: " + path);
            Tree.CurrentScene.QueueFree(); await Wait(0.3); Tree.Quit();
        }
        catch (Exception error)
        {
            _running = false; ControlBindings.ReleaseGameplayActions();
            GD.PushError($"FRESH GATE FAIL at {_step}, position {(_actor != null && GodotObject.IsInstanceValid(_actor) ? _actor.GlobalPosition : Vector2.Zero)}: {error}");
            Tree.CurrentScene?.QueueFree(); await Wait(0.3); Tree.Quit(1);
        }
    }

    public override void _Process(double delta)
    {
        if (!_running || DialogueManager.Instance?.IsDialogueActive != true) return;
        var dialogue = DialogueManager.Instance;
        if (_dialogueLine != dialogue.CurrentLineIndex) { _dialogueLine = dialogue.CurrentLineIndex; _readingSeconds = 0; }
        _readingSeconds += (float)delta;
        var intro = DialogueDatabase.Get("act1_intro");
        float readTime = dialogue.CurrentLineIndex < intro.Lines.Count ? Math.Max(3, intro.Lines[dialogue.CurrentLineIndex].Text.Length / 16f + 1) : 3;
        if (_readingSeconds >= readTime) { PressInteract(); _readingSeconds = 0; }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_running || !GodotObject.IsInstanceValid(_actor)) return;
        if (_actor.IsDead) { _failure = "Player died during normal combat"; return; }
        if (Tree.Paused || _actor.IsInputLocked) { ReleaseMovement(); Set("shoot", false); Set("aim", false); return; }
        _healCooldown -= (float)delta;
        bool heal = _actor.HealthComponent.CurrentHealth < 55 && _healCooldown <= 0;
        Set("quick_heal", heal);
        if (heal) _healCooldown = 1.5f;
        Zombie target = null; float nearest = 550 * 550;
        foreach (Node node in Tree.GetNodesInGroup("zombies"))
        {
            if (node is not Zombie zombie || zombie.IsDead || !zombie.Visible || zombie.ProcessMode == ProcessModeEnum.Disabled) continue;
            float distance = zombie.GlobalPosition.DistanceSquaredTo(_actor.GlobalPosition);
            if (distance >= nearest) continue;
            _sight.From = _actor.GlobalPosition; _sight.To = zombie.GlobalPosition;
            var hit = _actor.GetWorld2D().DirectSpaceState.IntersectRay(_sight);
            if (hit.Count > 0 && hit["collider"].AsGodotObject() != zombie)
            {
                continue;
            }
            _shotClearance.Transform = new Transform2D(0, _actor.GlobalPosition);
            _shotClearance.Motion = zombie.GlobalPosition - _actor.GlobalPosition;
            float[] clearance = _actor.GetWorld2D().DirectSpaceState.CastMotion(_shotClearance);
            if (clearance.Length > 0 && clearance[0] < 1) continue;
            nearest = distance; target = zombie;
        }
        bool fighting = target != null;
        _combatReportCooldown -= (float)delta;
        if (fighting && _combatReportCooldown <= 0)
        {
            _combatReportCooldown = 3;
            GD.Print($"FRESH COMBAT hp={_actor.HealthComponent.CurrentHealth:0} ammo={_actor.InventoryComponent.CurrentAmmo}/{_actor.InventoryComponent.CurrentReserve} target={target.Name} targetHP={target.HealthComponent.CurrentHealth:0} range={Mathf.Sqrt(nearest):0} shots={StoryDirector.Instance.Stats.ShotsFired} hits={StoryDirector.Instance.Stats.ShotsHit} rotation={_actor.Rotation:0.00} mouse={_actor.GetGlobalMousePosition()} targetPosition={target.GlobalPosition}");
        }
        Set("aim", fighting);
        Set("shoot", fighting && _actor.InventoryComponent.CurrentAmmo > 0 && !_actor.InventoryComponent.IsReloading);
        Set("reload", _actor.InventoryComponent.CurrentAmmo == 0 && _actor.InventoryComponent.CurrentReserve > 0 && !_actor.InventoryComponent.IsReloading);
        Vector2 aim = fighting ? target.GlobalPosition : _destination;
        Vector2 viewport = _actor.GetCanvasTransform() * aim;
        if (DisplayServer.GetName() != "headless") GetViewport().WarpMouse(viewport);
        Vector2 window = GetViewport().GetFinalTransform() * viewport;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = window, GlobalPosition = window });
        if (!_walking || fighting) { ReleaseMovement(); return; }
        Vector2 offset = _destination - _actor.GlobalPosition;
        Set("move_left", offset.X < -12); Set("move_right", offset.X > 12);
        Set("move_up", offset.Y < -12); Set("move_down", offset.Y > 12);
    }

    private async Task Walk(float x, float y)
    {
        _destination = new Vector2(x, y); _walking = true; _step = "walk " + _destination;
        GD.Print("FRESH STEP " + _step);
        for (int i = 0; i < 500 && _actor.GlobalPosition.DistanceTo(_destination) > 25; i++)
        { await Wait(0.1); if (_failure != null) throw new InvalidOperationException(_failure); }
        Require(_actor.GlobalPosition.DistanceTo(_destination) <= 25, "normal movement reaches " + _destination);
        _walking = false; ReleaseMovement();
    }
    private async Task Interact(string name, Func<bool> done)
    {
        _step = name;
        for (int i = 0; i < 20 && !done(); i++)
        {
            if (_failure != null) throw new InvalidOperationException(_failure);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Wait(0.05);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = false });
            await Wait(0.15);
        }
        Require(done(), name + " completes using actual interact input");
    }
    private static void Set(string action, bool pressed)
    {
        if (pressed && !Input.IsActionPressed(action)) Input.ActionPress(action);
        else if (!pressed && Input.IsActionPressed(action)) Input.ActionRelease(action);
    }
    private static void ReleaseMovement()
    { Set("move_left", false); Set("move_right", false); Set("move_up", false); Set("move_down", false); }
    private static void PressInteract()
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = false });
    }
    private async Task Wait(double seconds) => await ToSignal(Tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private T Find<T>(string name) where T : Node => All<T>(Tree.CurrentScene).First(node => node.Name == name);
    private static System.Collections.Generic.IEnumerable<T> All<T>(Node root) where T : Node
    {
        if (root is T match) yield return match;
        foreach (Node child in root.GetChildren()) foreach (var nested in All<T>(child)) yield return nested;
    }
    private static void Require(bool success, string message)
    { if (!success) throw new InvalidOperationException(message); GD.Print("FRESH PASS " + message); }
}
