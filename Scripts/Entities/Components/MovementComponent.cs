using Godot;

namespace TopDownDemo.Entities.Components;

[GlobalClass]
public partial class MovementComponent : Node
{
    [Export] public CharacterBody2D Body { get; set; }
    [Export] public float BaseSpeed { get; set; } = 300.0f;
    [Export] public float Friction { get; set; } = 12.0f;

    private Vector2 _knockbackVelocity = Vector2.Zero;

    public override void _Ready()
    {
        if (Body == null && GetParent() is CharacterBody2D parentBody)
        {
            Body = parentBody;
        }

        if (Body == null)
        {
            GD.PrintErr($"[MovementComponent] Body is not assigned on {GetPath()}");
        }
    }

    public void Move(Vector2 inputDirection, float speedMultiplier = 1.0f)
    {
        if (Body == null) return;

        Vector2 targetVelocity = inputDirection.Normalized() * (BaseSpeed * speedMultiplier);
        Body.Velocity = targetVelocity + _knockbackVelocity;
        Body.MoveAndSlide();
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        _knockbackVelocity += direction.Normalized() * force;
    }

    public void ResetMotion()
    {
        _knockbackVelocity = Vector2.Zero;
        if (Body != null) Body.Velocity = Vector2.Zero;
    }

    public void DecayKnockback(float delta, float decayRate = 900.0f)
    {
        _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, delta * decayRate);
    }

    public void Stop()
    {
        if (Body == null) return;
        Body.Velocity = _knockbackVelocity;
        Body.MoveAndSlide();
    }
}
