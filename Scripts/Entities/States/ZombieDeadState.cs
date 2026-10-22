using Godot;

namespace TopDownDemo.Entities.States;

public class ZombieDeadState : ZombieState
{
    public ZombieDeadState(Zombie zombie) : base(zombie) { }

    public override void Enter()
    {
        Zombie.MovementComponent.Stop();
        Zombie.PerformDeath();
    }
}
