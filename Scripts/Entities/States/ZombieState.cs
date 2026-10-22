using TopDownDemo.Core;

namespace TopDownDemo.Entities.States;

public abstract class ZombieState : IState
{
    protected readonly Zombie Zombie;

    protected ZombieState(Zombie zombie)
    {
        Zombie = zombie;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update(float delta) { }
    public virtual void PhysicsUpdate(float delta) { }
}
