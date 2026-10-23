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
            Vector2 moveDir = dir;
            if (Zombie.GetSlideCollisionCount() > 0)
            {
                var collision = Zombie.GetSlideCollision(0);
                Vector2 normal = collision.GetNormal();
                Vector2 tangent = new Vector2(-normal.Y, normal.X);
                if (tangent.Dot(dir) < 0) tangent = -tangent;
                moveDir = (dir + tangent * 1.8f).Normalized();
            }

            Zombie.MovementComponent.Move(moveDir, Zombie.SpeedModifier);
        }
        else
        {
            Zombie.MovementComponent.Stop();
        }
    }
}
