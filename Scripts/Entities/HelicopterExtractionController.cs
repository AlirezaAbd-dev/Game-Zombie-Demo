using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;
using TopDownDemo.UI;

namespace TopDownDemo.Entities;

public enum ExtractionState { WaitingForBeacon, Approaching, Holding, Landed, Boarding, Departing, Complete }

public partial class HelicopterExtractionController : Node
{
    [Export] public HelicopterExtractionData Data { get; set; }
    [Export] public Node2D Helicopter { get; set; }
    [Export] public Marker2D LandingPoint { get; set; }
    [Export] public Marker2D SeatPoint { get; set; }
    [Export] public CollisionShape2D HullCollision { get; set; }
    [Export] public Area2D LandingZone { get; set; }
    [Export] public CollisionShape2D LandingSensor { get; set; }
    [Export] public EvacHelipad Pad { get; set; }
    [Export] public AudioStreamPlayer2D RotorAudio { get; set; }
    public ExtractionState State { get; private set; }
    public string Message { get; private set; } = "RESTORE THE BEACON TO REQUEST VULTURE-9";
    public float Progress { get; private set; }
    public bool CanBoard => State == ExtractionState.Landed;
    public event Action StatusChanged;
    private StoryDirector _story;
    private float _timer;
    private Player _request;
    private Player _passenger;
    private Vector2 _boardingStart;
    private bool _inputLock, _invulnerable, _visible;
    private uint _layer, _mask;
    private readonly PhysicsShapeQueryParameters2D _landingQuery = new() { CollisionMask = 3 };
    private readonly System.Collections.Generic.List<(CharacterBody2D Body, CollisionShape2D Shape)> _landingActors = new();

    public override void _Ready()
    {
        if (RotorAudio != null) RotorAudio.Bus = PlayerSettings.EffectsBus;
        if (Data == null || Helicopter == null || LandingPoint == null || SeatPoint == null
            || LandingZone == null || LandingSensor?.Shape == null || HullCollision == null || Pad == null
            || Data.ApproachSeconds <= 0 || Data.BoardingSeconds <= 0 || Data.DepartureSeconds <= 0)
        { GD.PushError("Extraction requires flight data, aircraft, landing/seat markers, zone, hull and boarding pad."); SetPhysicsProcess(false); return; }
        CallDeferred(nameof(Initialize));
    }

    private void Initialize()
    {
        _story = StoryDirector.Instance;
        if (_story == null) return;
        _story.FlagsChanged += OnFlagsChanged;
        if (RotorAudio?.Stream is AudioStreamWav rotor)
        {
            rotor.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            rotor.LoopBegin = 0; rotor.LoopEnd = rotor.Data.Length / 2;
        }
        CacheLandingActors("player");
        CacheLandingActors("zombies");
        Helicopter.Visible = false;
        HullCollision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        if (_story.HasFlag(Data.ArrivedFlag) && _story.HasFlag(Data.RequestFlag)
            && _story.HasFlag(StoryFlags.UNIT9_DEFEATED)) Land();
        else OnFlagsChanged();
        Publish();
    }

    private void OnFlagsChanged()
    {
        if (State != ExtractionState.WaitingForBeacon || !_story.HasFlag(Data.RequestFlag)
            || !_story.HasFlag(StoryFlags.UNIT9_DEFEATED)) return;
        Helicopter.Visible = true;
        if (RotorAudio != null && !RotorAudio.Playing) RotorAudio.Play();
        Helicopter.GlobalPosition = LandingPoint.GlobalPosition + Data.ApproachOffset;
        SetState(ExtractionState.Approaching, "VULTURE-9 INBOUND - CLEAR THE LANDING ZONE");
    }

    public bool RequestBoard(Player player)
    {
        if (!CanBoard || !IsInstanceValid(player) || player.IsDead || !_story.HasFlag(StoryFlags.UNIT9_DEFEATED)) return false;
        _request = player;
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_story == null) return;
        float dt = (float)delta;
        if (_request != null)
        {
            var player = _request; _request = null;
            if (CanBoard && IsInstanceValid(player) && !player.IsDead
                && player.GlobalPosition.DistanceTo(Pad.Interactable.GlobalPosition) <= 130 && !ZoneObstructed(false))
                BeginBoarding(player);
            else HUDUI.Instance?.ShowFloatingNotification("REACH THE BOARDING DOOR / CLEAR HOSTILES FROM THE PAD", Colors.Orange);
        }
        if (State == ExtractionState.Approaching)
        {
            _timer += dt;
            Progress = Mathf.Min(1, _timer / Data.ApproachSeconds);
            Helicopter.GlobalPosition = LandingPoint.GlobalPosition + Data.ApproachOffset * (1 - Progress);
            Helicopter.Scale = Vector2.One * Mathf.Lerp(1.6f,1,Progress);
            if (Progress >= 1)
            {
                if (ZoneObstructed(true))
                {
                    Helicopter.GlobalPosition = LandingPoint.GlobalPosition + new Vector2(0,-350);
                    SetState(ExtractionState.Holding,"PILOT HOLDING - CLEAR INFECTED AND STAND OUTSIDE THE MARKED PAD");
                }
                else Land();
            }
        }
        else if (State == ExtractionState.Holding)
        {
            if (!ZoneObstructed(true)) Land();
        }
        else if (State == ExtractionState.Boarding)
        {
            _timer += dt;
            Progress = Mathf.Min(1,_timer / Data.BoardingSeconds);
            Vector2 motion = (SeatPoint.GlobalPosition - _passenger.GlobalPosition)
                * Mathf.Min(1,dt / Mathf.Max(0.01f,Data.BoardingSeconds - _timer + dt));
            if (_passenger.MoveAndCollide(motion) != null)
            { AbortDeparture("BOARDING PATH OBSTRUCTED - CLEAR THE DOOR AND TRY AGAIN"); return; }
            if (Progress >= 1)
            {
                _passenger.Visible = false;
                _passenger.CollisionLayer = 0; _passenger.CollisionMask = 0;
                HullCollision.SetDeferred(CollisionShape2D.PropertyName.Disabled,true);
                SetState(ExtractionState.Departing,"ALL ABOARD - VULTURE-9 LIFTING OFF");
            }
        }
        else if (State == ExtractionState.Departing)
        {
            _timer += dt;
            Progress = Mathf.Min(1,_timer / Data.DepartureSeconds);
            Helicopter.GlobalPosition = LandingPoint.GlobalPosition + Data.DepartureOffset * Progress;
            Helicopter.Scale = Vector2.One * Mathf.Lerp(1,1.5f,Progress);
            if (Progress >= 1) FinishDeparture();
        }
        Publish();
    }

    private bool ZoneObstructed(bool includePlayer)
    {
        // Check current node transforms as well as space queries: the physics broadphase
        // can still contain last-frame positions when another actor has just moved.
        foreach (var actor in _landingActors)
            if (IsInstanceValid(actor.Body) && IsInstanceValid(actor.Shape) && !actor.Shape.Disabled
                && (actor.Body.CollisionLayer & 3) != 0 && BlocksLanding(actor.Body,includePlayer)
                && LandingSensor.Shape.Collide(LandingSensor.GlobalTransform,actor.Shape.Shape,actor.Shape.GlobalTransform)) return true;
        _landingQuery.Shape = LandingSensor.Shape;
        _landingQuery.Transform = LandingSensor.GlobalTransform;
        foreach (var hit in LandingZone.GetWorld2D().DirectSpaceState.IntersectShape(_landingQuery,64))
        {
            var body = hit["collider"].AsGodotObject();
            if (BlocksLanding(body,includePlayer)) return true;
        }
        return false;
    }

    private static bool BlocksLanding(GodotObject body,bool includePlayer) =>
        (includePlayer && body is Player { IsDead: false }) || body is Zombie { IsDead: false } || body is Boss_Unit9 { IsDead: false };

    private void CacheLandingActors(string group)
    {
        foreach (Node node in GetTree().GetNodesInGroup(group))
            if (node is CharacterBody2D body)
                foreach (Node child in body.GetChildren())
                    if (child is CollisionShape2D shape && shape.Shape != null) _landingActors.Add((body,shape));
    }

    private void Land()
    {
        Helicopter.Visible = true;
        if (RotorAudio != null && !RotorAudio.Playing) RotorAudio.Play();
        Helicopter.GlobalPosition = LandingPoint.GlobalPosition;
        Helicopter.Scale = Vector2.One;
        HullCollision.SetDeferred(CollisionShape2D.PropertyName.Disabled,false);
        _story.SetFlag(Data.ArrivedFlag);
        SetState(ExtractionState.Landed,$"VULTURE-9 ON THE PAD - APPROACH THE EAST BOARDING DOOR [{ControlBindings.Hint("interact")}]");
        AudioManager.Instance?.PlaySound(SoundType.RadioChime);
    }

    private void BeginBoarding(Player player)
    {
        _passenger = player;
        _boardingStart = player.GlobalPosition;
        _inputLock = player.IsInputLocked; _invulnerable = player.IsInvulnerable; _visible = player.Visible;
        _layer = player.CollisionLayer; _mask = player.CollisionMask;
        player.IsInputLocked = true; player.IsInvulnerable = true;
        player.MovementComponent?.ResetMotion();
        SetState(ExtractionState.Boarding,"BOARDING VULTURE-9");
    }

    private void FinishDeparture()
    {
        var saves = SaveSystem.Instance;
        var transitions = SceneTransitionManager.Instance;
        bool endingWasSet = _story.HasFlag(StoryFlags.ENDING_TRIGGERED);
        _story.SetFlag(StoryFlags.ENDING_TRIGGERED);
        if (saves == null || transitions == null || transitions.IsTransitioning
            || !saves.Save(saves.CurrentSlot,_passenger,"res://Scenes/Credits.tscn",StoryAct.Credits))
        {
            if (!endingWasSet) _story.RemoveFlag(StoryFlags.ENDING_TRIGGERED);
            AbortDeparture("EXTRACTION CHECKPOINT FAILED - BOARD AGAIN TO RETRY");
            return;
        }
        SetState(ExtractionState.Complete,"EXTRACTION CONFIRMED");
        _story.CurrentAct = StoryAct.Credits;
        transitions.TransitionToScene("res://Scenes/Credits.tscn","EXTRACTION CONFIRMED","Vulture-9 / Blacksite Foxtrot-7");
    }

    private void AbortDeparture(string message)
    {
        RestorePassenger();
        Helicopter.GlobalPosition = LandingPoint.GlobalPosition;
        Helicopter.Scale = Vector2.One;
        HullCollision.SetDeferred(CollisionShape2D.PropertyName.Disabled,false);
        SetState(ExtractionState.Landed,message);
    }

    private void RestorePassenger()
    {
        if (!IsInstanceValid(_passenger)) return;
        _passenger.GlobalPosition = _boardingStart;
        _passenger.Visible = _visible; _passenger.IsInputLocked = _inputLock; _passenger.IsInvulnerable = _invulnerable;
        _passenger.CollisionLayer = _layer; _passenger.CollisionMask = _mask;
        _passenger.MovementComponent?.ResetMotion();
    }

    private void SetState(ExtractionState state,string message) { State = state; Message = message; _timer = 0; Progress = 0; Publish(); }
    private void Publish() => StatusChanged?.Invoke();

    public override void _Input(InputEvent input)
    {
        if (State is not (ExtractionState.Boarding or ExtractionState.Departing or ExtractionState.Complete)) return;
        if (input.IsActionPressed("pause_game") || input.IsActionPressed("toggle_inventory")
            || input.IsActionPressed("toggle_map") || input.IsActionPressed("ui_cancel") || input.IsActionPressed("restart"))
            GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_story)) _story.FlagsChanged -= OnFlagsChanged;
        if (State is ExtractionState.Boarding or ExtractionState.Departing) RestorePassenger();
    }
}
