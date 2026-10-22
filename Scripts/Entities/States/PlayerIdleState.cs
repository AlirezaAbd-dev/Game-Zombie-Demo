using Godot;

namespace TopDownDemo.Entities.States;

public class PlayerIdleState : PlayerState
{
    public PlayerIdleState(Player player) : base(player) { }

    public override void Update(float delta)
    {
        Player.HandleAiming();
        Player.HandleCombat(delta);
    }

    public override void PhysicsUpdate(float delta)
    {
        Vector2 input = Player.GetMovementInput();
        if (input != Vector2.Zero)
        {
            Player.StateMachine.ChangeState(Player.WalkState);
            return;
        }

        if (Input.IsActionJustPressed("dash") && Player.StaminaComponent.CanDash())
        {
            Player.StateMachine.ChangeState(Player.DashState);
            return;
        }

        Player.MovementComponent.Stop();
        Player.AnimateIdle(delta);
    }
}
