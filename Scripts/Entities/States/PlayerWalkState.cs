using Godot;

namespace TopDownDemo.Entities.States;

public class PlayerWalkState : PlayerState
{
    public PlayerWalkState(Player player) : base(player) { }

    public override void Update(float delta)
    {
        Player.HandleAiming();
        Player.HandleCombat(delta);
    }

    public override void PhysicsUpdate(float delta)
    {
        Vector2 input = Player.GetMovementInput();
        if (input == Vector2.Zero)
        {
            Player.StateMachine.ChangeState(Player.IdleState);
            return;
        }

        if (Input.IsActionJustPressed("dash") && Player.StaminaComponent.CanDash() && !Player.IsAiming)
        {
            Player.StateMachine.ChangeState(Player.DashState);
            return;
        }

        float speedMult = Player.IsAiming ? 0.45f : (Player.IsCrouching ? 0.55f : 1.0f);
        Player.MovementComponent.Move(input, speedMult);
        Player.AnimateWalk(Player.IsCrouching ? delta * 0.6f : delta);
    }
}
