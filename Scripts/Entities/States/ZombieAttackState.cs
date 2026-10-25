using Godot;

namespace TopDownDemo.Entities.States;

public class ZombieAttackState : ZombieState
{
    private float _attackCooldown = 0.0f;

    public ZombieAttackState(Zombie zombie) : base(zombie) { }

    public override void Enter()
    {
        _attackCooldown = 0.0f;
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

        Vector2 toTarget = Zombie.TargetPlayer.GlobalPosition - Zombie.GlobalPosition;
        float distance = toTarget.Length();
        Vector2 dir = toTarget.Normalized();

        Zombie.RotateTowards(dir, delta);
        Zombie.MovementComponent?.Stop();

        // If player moved behind a wall or out of range, transition back to ChaseState to navigate
        if (!Zombie.HasLineOfSightTo(Zombie.TargetPlayer.GlobalPosition) || distance > Zombie.Stats.AttackRange * 1.35f)
        {
            Zombie.StateMachine.ChangeState(Zombie.ChaseState);
            return;
        }

        _attackCooldown += delta;
        if (_attackCooldown >= Zombie.Stats.AttackInterval)
        {
            _attackCooldown = 0.0f;
            Zombie.PerformAttack();
        }
    }
}
