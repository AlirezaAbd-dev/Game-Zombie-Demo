using Godot;

namespace TopDownDemo.Entities.States;

public class ZombieChaseState : ZombieState
{
    public ZombieChaseState(Zombie zombie) : base(zombie) { }

    public override void PhysicsUpdate(float delta)
    {
        if (!Zombie.HasValidTarget())
        {
            Zombie.StateMachine.ChangeState(Zombie.IdleState);
            return;
        }

        Vector2 toTarget = Zombie.TargetPlayer.GlobalPosition - Zombie.GlobalPosition;
        float distance = toTarget.Length();
        Vector2 dir = toTarget.Normalized();

        Zombie.RotateTowards(dir, delta);
        Zombie.AnimateWalk(delta);

        if (distance <= Zombie.Stats.AttackRange)
        {
            Zombie.StateMachine.ChangeState(Zombie.AttackState);
            return;
        }

        if (distance > Zombie.Stats.StopDistance)
        {
            Zombie.MovementComponent.Move(dir, Zombie.SpeedModifier);
        }
        else
        {
            Zombie.MovementComponent.Stop();
        }
    }
}
