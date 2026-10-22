using Godot;

namespace TopDownDemo.Core;

public interface IDamageable
{
    void TakeDamage(float amount, Vector2 knockbackDir);
}
