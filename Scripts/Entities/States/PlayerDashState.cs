using Godot;

namespace TopDownDemo.Entities.States;

public class PlayerDashState : PlayerState
{
    private float _dashTimer;
    private Vector2 _dashDir;

    public PlayerDashState(Player player) : base(player) { }

    public override void Enter()
    {
        _dashDir = Player.GetMovementInput();
        if (_dashDir == Vector2.Zero)
        {
            _dashDir = Transform2D.Identity.X.Rotated(Player.GlobalRotation);
        }
        else
        {
            _dashDir = _dashDir.Normalized();
        }

        Player.StaminaComponent.TryConsumeDash();
        _dashTimer = Player.StaminaComponent.DashDuration;
        Player.IsInvulnerable = true;

        Player.AddTrauma(0.2f);
        Player.SpawnDashGhost();
    }

    public override void Exit()
    {
        Player.IsInvulnerable = false;
    }

    public override void PhysicsUpdate(float delta)
    {
        _dashTimer -= delta;
        Player.MovementComponent.Move(_dashDir, Player.StaminaComponent.DashSpeed / Player.MovementComponent.BaseSpeed);

        if (_dashTimer <= 0.0f)
        {
            Vector2 input = Player.GetMovementInput();
            Player.StateMachine.ChangeState(input != Vector2.Zero ? Player.WalkState : Player.IdleState);
        }
    }
}
