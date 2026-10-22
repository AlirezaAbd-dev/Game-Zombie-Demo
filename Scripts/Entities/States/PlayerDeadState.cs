using Godot;

namespace TopDownDemo.Entities.States;

public class PlayerDeadState : PlayerState
{
    public PlayerDeadState(Player player) : base(player) { }

    public override void Enter()
    {
        Player.MovementComponent.Stop();
        Player.OnDeathSequence();
    }

    public override void PhysicsUpdate(float delta)
    {
        Player.MovementComponent.DecayKnockback(delta, 500.0f);
        Player.MovementComponent.Stop();
    }
}
