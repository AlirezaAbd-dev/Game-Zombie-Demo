using Godot;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities.States;

public class ZombieChaseState : ZombieState
{
    private float _lostSightTimer = 0.0f;
    private float _visionCheckTimer = 0.0f;
    private bool _hasLineOfSight = false;

    public ZombieChaseState(Zombie zombie) : base(zombie) { }

    public override void Enter()
    {
        _lostSightTimer = 0.0f;
        _visionCheckTimer = 0.0f;
        _hasLineOfSight = true;

        if (Zombie.EyeGlow != null)
        {
            // Aggressive eye glow flare
            Zombie.EyeGlow.Energy = Zombie.EyeGlowBaseEnergy * 1.8f;
        }

        // Play alert audio cue
        SoundType sType = Zombie.Stats?.TypeName == "Specimen Zero" ? SoundType.BossRoar : SoundType.ZombieGroan;
        AudioManager.Instance?.PlaySound(sType, Zombie.GlobalPosition, (float)GD.RandRange(0.95, 1.15));
    }

    public override void PhysicsUpdate(float delta)
    {
        if (Zombie.TargetPlayer == null || !GodotObject.IsInstanceValid(Zombie.TargetPlayer))
        {
            Zombie.StateMachine.ChangeState(Zombie.IdleState);
            return;
        }

        if (Zombie.TargetPlayer is Player p && p.IsDead)
        {
            Zombie.StateMachine.ChangeState(Zombie.IdleState);
            return;
        }

        _visionCheckTimer -= delta;
        if (_visionCheckTimer <= 0.0f)
        {
            _visionCheckTimer = 0.1f;
            _hasLineOfSight = Zombie.CanSeePlayer();

            if (_hasLineOfSight)
            {
                Zombie.LastKnownPlayerPos = Zombie.TargetPlayer.GlobalPosition;
                _lostSightTimer = 0.0f;
            }
        }

        if (!_hasLineOfSight)
        {
            _lostSightTimer += delta;
        }

        Vector2 targetPos = _hasLineOfSight ? Zombie.TargetPlayer.GlobalPosition : Zombie.LastKnownPlayerPos;
        float distToTarget = Zombie.GlobalPosition.DistanceTo(targetPos);
        float distToPlayer = Zombie.GlobalPosition.DistanceTo(Zombie.TargetPlayer.GlobalPosition);

        // Check if in attack range and has direct line of sight
        if (_hasLineOfSight && distToPlayer <= Zombie.Stats.AttackRange)
        {
            Zombie.StateMachine.ChangeState(Zombie.AttackState);
            return;
        }

        // If line of sight lost, reached last known position, and searched for a while: calm down and go idle
        if (!_hasLineOfSight && distToTarget < 50.0f && _lostSightTimer > 2.8f)
        {
            Zombie.HasTargetAcquired = false;
            Zombie.StateMachine.ChangeState(Zombie.IdleState);
            return;
        }

        // Lost player completely for extended duration (escaped into another sector or far away)
        if (!_hasLineOfSight && _lostSightTimer > 8.0f)
        {
            Zombie.HasTargetAcquired = false;
            Zombie.StateMachine.ChangeState(Zombie.IdleState);
            return;
        }

        if (distToTarget > Zombie.Stats.StopDistance)
        {
            // Intelligent wall-avoiding context steering
            Vector2 steerDir = Zombie.CalculateSteeringDirection(targetPos, delta);
            if (steerDir != Vector2.Zero)
            {
                Zombie.RotateTowards(steerDir, delta);
                Zombie.AnimateWalk(delta);
                Zombie.MovementComponent?.Move(steerDir, Zombie.SpeedModifier);
            }
            else
            {
                Zombie.MovementComponent?.Stop();
            }
        }
        else
        {
            Zombie.MovementComponent?.Stop();
            if (_hasLineOfSight)
            {
                Vector2 toPlayer = (Zombie.TargetPlayer.GlobalPosition - Zombie.GlobalPosition).Normalized();
                Zombie.RotateTowards(toPlayer, delta);
            }
        }
    }
}
