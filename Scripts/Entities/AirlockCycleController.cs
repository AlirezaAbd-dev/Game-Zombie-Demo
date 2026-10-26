using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities;

public partial class AirlockCycleController : Node
{
    [Export] public AirlockCycleData Data { get; set; }
    [Export] public BlastDoor OuterDoor { get; set; }
    [Export] public BlastDoor InnerDoor { get; set; }
    [Export] public Godot.Collections.Array<BlastDoor> ServiceDoors { get; set; } = new();
    [Export] public Area2D ChamberVolume { get; set; }
    [Export] public Area2D SafeStandZone { get; set; }
    [Export] public InteractableComponent Controls { get; set; }
    public AirlockState State { get; private set; }
    public float Progress { get; private set; }
    public string Message { get; private set; } = "RESTORE EXHAUST AND ISOLATE THE LEAK";
    public event Action StatusChanged;
    private StoryDirector _story;
    private Player _request;
    private bool _abortRequested;
    private float _elapsed;
    private bool _initialized;
    public bool IsCycling => State is AirlockState.Sealing or AirlockState.Equalizing or AirlockState.Purging;

    public override void _Ready()
    {
        if (Data == null || OuterDoor == null || InnerDoor == null || ChamberVolume == null
            || SafeStandZone == null || Controls == null || Data.SealSeconds <= 0
            || Data.EqualizeSeconds <= 0 || Data.PurgeSeconds <= 0)
        {
            GD.PushError("Airlock requires cycle data, two main doors, occupancy areas and controls.");
            SetPhysicsProcess(false);
            return;
        }
        Controls.Interacted += OnControls;
        CallDeferred(nameof(Initialize));
    }

    private void Initialize()
    {
        _story = StoryDirector.Instance;
        if (_story == null) return;
        _story.FlagsChanged += OnFlagsChanged;
        _initialized = true;
        if (_story.HasFlag(Data.CompletionFlag)) Complete();
        else Reset("CHECK EXHAUST AND THE COOLANT LEAK BEFORE CYCLING");
    }

    private void OnFlagsChanged()
    {
        if (!IsCycling && State != AirlockState.Complete && _story.HasFlag(Data.ExhaustFlag)
            && _story.HasFlag(Data.LeakIsolatedFlag))
            Publish("READY - STAND INSIDE THE CHAMBER AND BEGIN CYCLE");
    }

    private void OnControls(Player player)
    {
        if (IsCycling) _abortRequested = true;
        else _request = player;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_initialized) return;
        if (_abortRequested)
        {
            _abortRequested = false;
            Reset("CYCLE ABORTED - RETURN DOORS OPEN; RESTART WHEN READY");
        }
        if (_request != null)
        {
            var player = _request;
            _request = null;
            Begin(player);
        }
        if (!IsCycling) return;
        _elapsed += (float)delta;
        if (State == AirlockState.Sealing && _elapsed >= Data.SealSeconds)
            SetPhase(AirlockState.Equalizing, $"EQUALIZING PRESSURE - [{ControlBindings.Hint("interact")}] ABORT");
        else if (State == AirlockState.Equalizing)
        {
            Progress = 0.5f * Mathf.Min(1, _elapsed / Data.EqualizeSeconds);
            if (_elapsed >= Data.EqualizeSeconds) SetPhase(AirlockState.Purging, $"FILTERED PURGE IN PROGRESS - [{ControlBindings.Hint("interact")}] ABORT");
        }
        else if (State == AirlockState.Purging)
        {
            Progress = 0.5f + 0.5f * Mathf.Min(1, _elapsed / Data.PurgeSeconds);
            if (_elapsed >= Data.PurgeSeconds)
            {
                _story.SetFlag(Data.CompletionFlag);
                Complete();
            }
        }
        StatusChanged?.Invoke();
    }

    private void Begin(Player player)
    {
        if (State == AirlockState.Complete || !IsInstanceValid(player) || player.IsDead) return;
        if (!_story.HasFlag(Data.ExhaustFlag) || !_story.HasFlag(Data.LeakIsolatedFlag))
        { Publish("INTERLOCK: RESTORE EXHAUST AND ISOLATE THE COOLANT LEAK"); return; }
        if (!SafeStandZone.GetOverlappingBodies().Contains(player))
        { Publish("STAND INSIDE THE MARKED CHAMBER, CLEAR OF ALL DOORS"); return; }
        foreach (var body in ChamberVolume.GetOverlappingBodies())
            if (body is Zombie { IsDead: false })
            { Publish("CONTAMINATION DETECTED - CLEAR OR LURE INFECTED OUT OF THE CHAMBER"); return; }
        if (!OuterDoor.CanCloseSafely()) { Publish("OUTER DOORWAY OBSTRUCTED - CLEAR IT FIRST"); return; }
        foreach (var door in ServiceDoors)
            if (!door.CanCloseSafely()) { Publish("SERVICE DOORWAY OBSTRUCTED - CLEAR IT FIRST"); return; }
        OuterDoor.RestoreClosed();
        foreach (var door in ServiceDoors) door.RestoreClosed();
        InnerDoor.RestoreClosed();
        Controls.PromptText = "ABORT CYCLE / OPEN RETURN DOORS";
        Progress = 0;
        SetPhase(AirlockState.Sealing, $"SEALING CHAMBER - [{ControlBindings.Hint("interact")}] ABORT");
        AudioManager.Instance?.PlaySound(SoundType.DoorOpen);
    }

    private void Reset(string message)
    {
        State = AirlockState.Idle;
        _elapsed = 0;
        Progress = 0;
        InnerDoor.RestoreClosed();
        OuterDoor.RestoreOpen();
        foreach (var door in ServiceDoors) door.RestoreOpen();
        Controls.IsActive = true;
        Controls.PromptText = "SEAL CHAMBER / BEGIN DECONTAMINATION";
        Publish(message);
    }

    private void Complete()
    {
        State = AirlockState.Complete;
        Progress = 1;
        InnerDoor.RestoreOpen();
        OuterDoor.RestoreClosed();
        foreach (var door in ServiceDoors) door.RestoreClosed();
        Controls.IsActive = false;
        Publish("TRANSFER CLEARED - INNER DOOR OPEN / PROCEED TO LAB");
        AudioManager.Instance?.PlaySound(SoundType.TerminalData);
    }

    private void SetPhase(AirlockState state, string message)
    {
        State = state;
        _elapsed = 0;
        Publish(message);
    }

    private void Publish(string message)
    {
        Message = message;
        StatusChanged?.Invoke();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_story)) _story.FlagsChanged -= OnFlagsChanged;
    }
}
