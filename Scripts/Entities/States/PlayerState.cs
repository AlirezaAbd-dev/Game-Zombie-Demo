using Godot;
using TopDownDemo.Core;

namespace TopDownDemo.Entities.States;

public abstract class PlayerState : IState
{
    protected readonly Player Player;

    protected PlayerState(Player player)
    {
        Player = player;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update(float delta) { }
    public virtual void PhysicsUpdate(float delta) { }
}
