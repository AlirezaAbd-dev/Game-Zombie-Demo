using Godot;

namespace TopDownDemo.Entities.States;

public class ZombieIdleState : ZombieState
{
    private float _visionCheckTimer = 0.0f;
    private float _wanderIntervalTimer = 0.0f;
    private float _wanderWalkTimer = 0.0f;
    private Vector2 _wanderDir = Vector2.Zero;
    private bool _isWandering = false;

    public ZombieIdleState(Zombie zombie) : base(zombie) { }

    public override void Enter()
    {
        Zombie.MovementComponent?.Stop();
        if (Zombie.EyeGlow != null)
        {
            Zombie.EyeGlow.Energy = Zombie.EyeGlowBaseEnergy;
        }

        _visionCheckTimer = (float)GD.RandRange(0.0, 0.15);
        _wanderIntervalTimer = (float)GD.RandRange(2.5, 5.0);
        _wanderWalkTimer = 0.0f;
        _isWandering = false;
    }

    public override void PhysicsUpdate(float delta)
    {
        // Periodic Line-of-Sight vision check
        _visionCheckTimer -= delta;
        if (_visionCheckTimer <= 0.0f)
        {
            _visionCheckTimer = 0.12f;

            if (Zombie.CanSeePlayer())
            {
                Zombie.LastKnownPlayerPos = Zombie.TargetPlayer.GlobalPosition;
                Zombie.HasTargetAcquired = true;
                Zombie.StateMachine.ChangeState(Zombie.ChaseState);
                return;
            }
        }

        // Natural idle shuffle / look-around behavior when player is not visible
        _wanderIntervalTimer -= delta;
        if (_wanderIntervalTimer <= 0.0f)
        {
            _wanderIntervalTimer = (float)GD.RandRange(3.5, 6.5);
            // 50% chance to shuffle slightly, 50% chance to just stand still looking
            if (GD.Randf() < 0.5f)
            {
                float randomAngle = (float)GD.RandRange(0.0, Mathf.Tau);
                _wanderDir = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
                _wanderWalkTimer = (float)GD.RandRange(0.8, 1.6);
                _isWandering = true;
            }
            else
            {
                _isWandering = false;
            }
        }

        if (_isWandering && _wanderWalkTimer > 0.0f)
        {
            _wanderWalkTimer -= delta;
            Zombie.RotateTowards(_wanderDir, delta * 2.0f);
            Zombie.AnimateWalk(delta * 0.4f);
            Zombie.MovementComponent?.Move(_wanderDir, Zombie.SpeedModifier * 0.22f);

            if (Zombie.GetSlideCollisionCount() > 0)
            {
                _isWandering = false;
                Zombie.MovementComponent?.Stop();
            }
        }
        else
        {
            _isWandering = false;
            Zombie.MovementComponent?.Stop();
        }
    }
}
