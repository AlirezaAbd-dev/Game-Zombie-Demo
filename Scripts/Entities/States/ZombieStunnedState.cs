using Godot;

namespace TopDownDemo.Entities.States;

public class ZombieStunnedState : ZombieState
{
    private float _staggerPhase = 0.0f;

    public ZombieStunnedState(Zombie zombie) : base(zombie) { }

    public override void Enter()
    {
        Zombie.MovementComponent?.Stop();
        _staggerPhase = 0.0f;

        if (Zombie.Sprite != null)
        {
            var tween = Zombie.CreateTween();
            Zombie.Sprite.Modulate = new Color(1.8f, 1.6f, 0.4f, 1.0f);
            tween.TweenProperty(Zombie.Sprite, "modulate", Zombie.Stats.Tint, 0.25);
        }
    }

    public override void Exit()
    {
        if (Zombie.Sprite != null)
        {
            Zombie.Sprite.Rotation = 0.0f;
            Zombie.Sprite.Modulate = Zombie.Stats?.Tint ?? Colors.White;
        }
    }

    public override void PhysicsUpdate(float delta)
    {
        Zombie.MovementComponent?.Stop();

        _staggerPhase += delta * 7.5f;
        float wobble = Mathf.Sin(_staggerPhase) * 0.18f;

        if (Zombie.Sprite != null)
        {
            Zombie.Sprite.Rotation = wobble;
        }
    }
}
