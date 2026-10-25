using System;
using Godot;
using TopDownDemo.Entities;
using TopDownDemo.Entities.States;

namespace TopDownDemo.Systems;

public partial class StealthSystem : Node
{
    private static StealthSystem _instance;
    public static StealthSystem Instance
    {
        get => GodotObject.IsInstanceValid(_instance) ? _instance : null;
        private set => _instance = value;
    }

    [Signal]
    public delegate void NoiseLevelChangedEventHandler(float noiseLevel);

    [Signal]
    public delegate void PlayerStealthStateChangedEventHandler(bool isCrouching, bool isHidden);

    public float CurrentNoiseLevel { get; private set; } = 0.0f;
    public bool IsPlayerCrouching { get; private set; } = false;
    public bool IsPlayerHidden { get; private set; } = false;

    private float _noiseDecaySpeed = 1.8f;
    private float _targetNoise = 0.0f;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        UpdatePlayerStealthState();

        // Smooth noise decay
        if (CurrentNoiseLevel > _targetNoise)
        {
            CurrentNoiseLevel = Mathf.MoveToward(CurrentNoiseLevel, _targetNoise, dt * _noiseDecaySpeed);
        }
        else
        {
            CurrentNoiseLevel = Mathf.MoveToward(CurrentNoiseLevel, _targetNoise, dt * 6.0f);
        }

        EmitSignal(SignalName.NoiseLevelChanged, CurrentNoiseLevel);
    }

    private void UpdatePlayerStealthState()
    {
        Player player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player == null || !GodotObject.IsInstanceValid(player) || player.IsDead)
        {
            _targetNoise = 0.0f;
            return;
        }

        IsPlayerCrouching = player.IsCrouching;
        IsPlayerHidden = player.IsHiddenInLocker;

        if (IsPlayerHidden)
        {
            _targetNoise = 0.0f;
            return;
        }

        Vector2 input = player.GetMovementInput();
        bool isMoving = input.LengthSquared() > 0.01f;

        if (player.StateMachine?.CurrentState is PlayerDashState)
        {
            _targetNoise = 0.85f;
            EmitAcousticPulse(player.GlobalPosition, 400.0f);
        }
        else if (isMoving)
        {
            if (IsPlayerCrouching)
            {
                _targetNoise = 0.08f; // Near-silent sneak
            }
            else
            {
                _targetNoise = 0.38f; // Standard walking footsteps
                EmitAcousticPulse(player.GlobalPosition, 160.0f);
            }
        }
        else
        {
            _targetNoise = 0.0f; // Silent standing
        }
    }

    public void ReportGunshot(Vector2 origin, float radius = 700.0f)
    {
        CurrentNoiseLevel = 1.0f;
        _targetNoise = 0.6f;
        EmitAcousticPulse(origin, radius);
    }

    public void ReportDistractionNoise(Vector2 origin, float radius = 550.0f)
    {
        EmitAcousticPulse(origin, radius);
    }

    private void EmitAcousticPulse(Vector2 origin, float radius)
    {
        var zombies = GetTree().GetNodesInGroup("zombies");
        foreach (Node node in zombies)
        {
            if (node is Zombie zombie && GodotObject.IsInstanceValid(zombie) && !zombie.IsDead)
            {
                zombie.OnHearingNoise(origin, radius);
            }
        }
    }
}
