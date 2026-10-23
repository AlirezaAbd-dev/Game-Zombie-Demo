using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities;

public partial class Bullet : Area2D
{
    [Export] public float Speed { get; set; } = 1400.0f;
    [Export] public float Damage { get; set; } = 25.0f;
    [Export] public float MaxLifetime { get; set; } = 1.4f;

    private float _lifetime = 0.0f;
    private bool _isActive = false;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    public void Activate(float damage = 25.0f, float speed = 1400.0f)
    {
        Damage = damage;
        Speed = speed;
        _lifetime = 0.0f;
        _isActive = true;
        Visible = true;
        SetPhysicsProcess(true);
        Monitoring = true;
    }

    public void Deactivate()
    {
        _isActive = false;
        Visible = false;
        SetPhysicsProcess(false);
        Monitoring = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isActive) return;

        Vector2 step = Transform.X * Speed * (float)delta;

        // Continuous collision detection raycast to prevent high-speed tunneling
        var spaceState = GetWorld2D()?.DirectSpaceState;
        if (spaceState != null)
        {
            var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, GlobalPosition + step, collisionMask: 3);
            var hit = spaceState.IntersectRay(query);
            if (hit != null && hit.Count > 0)
            {
                var hitNode = hit["collider"].AsGodotObject() as Node2D;
                if (hitNode != null && !hitNode.IsInGroup("player"))
                {
                    GlobalPosition = hit["position"].AsVector2();
                    OnBodyEntered(hitNode);
                    return;
                }
            }
        }

        Position += step;
        _lifetime += (float)delta;
        if (_lifetime >= MaxLifetime)
        {
            Despawn();
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (!_isActive || body.IsInGroup("player"))
            return;

        if (body is IDamageable damageable)
        {
            damageable.TakeDamage(Damage, Transform.X);
            StoryDirector.Instance?.RecordShotHit();
            SpawnBloodImpact(GlobalPosition);
        }
        else
        {
            SpawnSparkImpact(GlobalPosition);
        }

        Despawn();
    }

    private void Despawn()
    {
        if (ProjectilePool.Instance != null)
        {
            ProjectilePool.Instance.Despawn(this);
        }
        else
        {
            QueueFree();
        }
    }

    private void SpawnBloodImpact(Vector2 pos)
    {
        var parent = GetParent();
        if (parent == null) return;

        var particles = new CpuParticles2D
        {
            GlobalPosition = pos,
            Emitting = true,
            OneShot = true,
            Explosiveness = 0.95f,
            Amount = 16,
            Lifetime = 0.35f,
            Spread = 70.0f,
            Direction = -Transform.X,
            InitialVelocityMin = 60.0f,
            InitialVelocityMax = 180.0f,
            Gravity = Vector2.Zero,
            ScaleAmountMin = 3.0f,
            ScaleAmountMax = 6.0f,
            Color = new Color(0.85f, 0.1f, 0.1f, 0.9f)
        };
        particles.Finished += particles.QueueFree;
        parent.AddChild(particles);
    }

    private void SpawnSparkImpact(Vector2 pos)
    {
        var parent = GetParent();
        if (parent == null) return;

        var particles = new CpuParticles2D
        {
            GlobalPosition = pos,
            Emitting = true,
            OneShot = true,
            Explosiveness = 0.95f,
            Amount = 12,
            Lifetime = 0.25f,
            Spread = 80.0f,
            Direction = -Transform.X,
            InitialVelocityMin = 40.0f,
            InitialVelocityMax = 140.0f,
            Gravity = Vector2.Zero,
            ScaleAmountMin = 2.0f,
            ScaleAmountMax = 4.0f,
            Color = new Color(1.0f, 0.9f, 0.4f, 1.0f)
        };
        particles.Finished += particles.QueueFree;
        parent.AddChild(particles);
    }
}
