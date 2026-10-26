using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;
using TopDownDemo.UI;

namespace TopDownDemo.Entities;

public partial class CoolantTrapController : Node2D
{
    [Export] public CoolantTrapData Data { get; set; }
    [Export] public InteractableComponent Controls { get; set; }
    [Export] public Area2D DischargeZone { get; set; }
    public float RemainingSeconds { get; private set; }
    public float BurstVisibility { get; private set; }
    private Player _request;
    private StoryDirector _story;
    private string _timerKey;
    private int _displaySeconds = -1;

    public override void _Ready()
    {
        if (Data == null || Controls == null || DischargeZone == null || Data.RechargeSeconds <= 0)
        { GD.PushError("Coolant trap requires data, valve controls and a discharge zone."); SetPhysicsProcess(false); return; }
        Controls.RequiredStoryFlag = Data.PowerFlag;
        Controls.LockedPromptText = "RESTORE EMERGENCY POWER FIRST";
        Controls.Interacted += player => _request = player;
        _timerKey = GetTree().CurrentScene.SceneFilePath + "::" + Data.TimerId;
        _story = StoryDirector.Instance;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_story == null || SaveSystem.Instance == null) return;
        var timers = SaveSystem.Instance.ActiveSaveData.PersistentTimers;
        timers.TryGetValue(_timerKey,out float readyAt);
        RemainingSeconds = Mathf.Max(0, readyAt - _story.Stats.PlaytimeSeconds);
        BurstVisibility = Mathf.Max(0, BurstVisibility - (float)delta);
        int seconds = Mathf.CeilToInt(RemainingSeconds);
        if (seconds != _displaySeconds)
        {
            _displaySeconds = seconds;
            Controls.PromptText = seconds > 0 ? $"COOLANT RECHARGING / {seconds}s" : "DISCHARGE COOLANT / LURE UNIT-9 INTO MARKED ZONE";
        }
        if (_request == null) return;
        var player = _request; _request = null;
        if (!IsInstanceValid(player) || player.IsDead || player.GlobalPosition.DistanceTo(Controls.GlobalPosition) > 150
            || !_story.HasFlag(Data.PowerFlag)) return;
        if (RemainingSeconds > 0)
        { HUDUI.Instance?.ShowFloatingNotification("COOLANT PRESSURE RECOVERING", Colors.Orange); return; }
        timers[_timerKey] = _story.Stats.PlaytimeSeconds + Data.RechargeSeconds;
        RemainingSeconds = Data.RechargeSeconds;
        BurstVisibility = 1;
        foreach (var body in DischargeZone.GetOverlappingBodies())
        {
            if (body is Boss_Unit9 boss && !boss.IsDead && boss.IsEncounterActive)
            { boss.TakeDamage(Data.BossDamage, Vector2.Zero); boss.ApplyStagger(Data.StaggerSeconds); }
            else if (body is Zombie zombie && !zombie.IsDead) zombie.TakeDamage(Data.InfectedDamage, Vector2.Zero);
        }
        AudioManager.Instance?.PlaySound(SoundType.VentBreak, GlobalPosition);
    }
}
