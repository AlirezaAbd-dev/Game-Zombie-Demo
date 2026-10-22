using Godot;

namespace TopDownDemo.Entities.States;

public class ZombieIdleState : ZombieState
{
    public ZombieIdleState(Zombie zombie) : base(zombie) { }

    public override void PhysicsUpdate(float delta)
    {
        if (Zombie.HasValidTarget())
        {
            Zombie.StateMachine.ChangeState(Zombie.ChaseState);
            return;
        }

        Zombie.MovementComponent.Stop();
    }
}
