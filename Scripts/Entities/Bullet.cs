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
    private Vector2? _launchOrigin;
    private ShotReport _shotReport;
    public bool IsActive => _isActive;
    private readonly PhysicsRayQueryParameters2D _rayQuery = new() { CollisionMask = 3, HitFromInside = true };
    private readonly Godot.Collections.Array<Rid> _rayExclusions = new();

    public bool IsEnemyBullet { get; set; } = false;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    public void Activate(float damage = 25.0f, float speed = 1400.0f, bool isEnemy = false, Vector2? launchOrigin = null, ShotReport shotReport = null)
    {
        _launchOrigin = launchOrigin;
        _shotReport = shotReport;
        Damage = damage;
        Speed = speed;
        IsEnemyBullet = isEnemy;
        _lifetime = 0.0f;
        _isActive = true;
        Visible = true;
        SetPhysicsProcess(true);
        SetDeferred(Area2D.PropertyName.Monitoring, true);

        Modulate = isEnemy ? new Color(0.3f, 1.8f, 0.4f, 1.0f) : Colors.White;
    }

    public void Deactivate()
    {
        _isActive = false;
        _launchOrigin = null;
        _shotReport = null;
        Visible = false;
        SetPhysicsProcess(false);
        SetDeferred(Area2D.PropertyName.Monitoring, false);
        Modulate = Colors.White;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isActive) return;

        Vector2 step = GlobalTransform.X.Normalized() * Speed * (float)delta;
        Vector2 rayStart = _launchOrigin ?? GlobalPosition;
        _launchOrigin = null;

        // Continuous collision detection raycast to prevent high-speed tunneling
        var spaceState = GetWorld2D()?.DirectSpaceState;
        if (spaceState != null)
        {
            // The first sweep includes the body-to-muzzle segment: a long barrel must
            // not spawn a round beyond a close target or through a nearby wall.
            _rayQuery.From = rayStart;
            _rayQuery.To = GlobalPosition + step;
            _rayExclusions.Clear();
            // A friendly body must not hide a wall or hostile farther along this same step.
            for (int pass = 0; pass < 32; pass++)
            {
                _rayQuery.Exclude = _rayExclusions;
                var hit = spaceState.IntersectRay(_rayQuery);
                if (hit.Count == 0) break;
                var hitNode = hit["collider"].AsGodotObject() as Node2D;
                if (hitNode != null)
                {
                    bool shouldHit = IsEnemyBullet
                        ? (!hitNode.IsInGroup("zombies") && !hitNode.IsInGroup("boss"))
                        : !hitNode.IsInGroup("player");

                    if (shouldHit)
                    {
                        GlobalPosition = hit["position"].AsVector2();
                        OnBodyEntered(hitNode);
                        return;
                    }
                }
                _rayExclusions.Add(hit["rid"].AsRid());
                if (pass == 31) { Despawn(); return; }
            }
        }

        GlobalPosition += step;
        _lifetime += (float)delta;
        if (_lifetime >= MaxLifetime)
        {
            Despawn();
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (!_isActive || _launchOrigin.HasValue) return;

        if (IsEnemyBullet)
        {
            if (body.IsInGroup("zombies") || body.IsInGroup("boss"))
                return;

            if (body is Player player && GodotObject.IsInstanceValid(player))
            {
                player.TakeDamage(Damage, GlobalTransform.X.Normalized());
                SpawnAcidImpact(GlobalPosition);
                Despawn();
                return;
            }
            else if (body is IDamageable damageable)
            {
                damageable.TakeDamage(Damage, Transform.X);
                SpawnAcidImpact(GlobalPosition);
                Despawn();
                return;
            }
            else
            {
                SpawnSparkImpact(GlobalPosition);
                Despawn();
                return;
            }
        }
        else
        {
            if (body.IsInGroup("player"))
                return;

            if (body is IDamageable damageable)
            {
                damageable.TakeDamage(Damage, Transform.X);
                if (_shotReport?.TryRecordHit() == true) StoryDirector.Instance?.RecordShotHit();
                SpawnBloodImpact(GlobalPosition);
            }
            else
            {
                SpawnSparkImpact(GlobalPosition);
            }

            Despawn();
        }
    }

    private void Despawn()
    {
        if (ProjectilePool.Instance != null)
        {
            ProjectilePool.Instance.Despawn(this);
        }
        else
        {
            Deactivate();
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

    private void SpawnAcidImpact(Vector2 pos)
    {
        var parent = GetParent();
        if (parent == null) return;

        var particles = new CpuParticles2D
        {
            GlobalPosition = pos,
            Emitting = true,
            OneShot = true,
            Explosiveness = 0.95f,
            Amount = 14,
            Lifetime = 0.35f,
            Spread = 80.0f,
            Direction = -Transform.X,
            InitialVelocityMin = 50.0f,
            InitialVelocityMax = 160.0f,
            Gravity = Vector2.Zero,
            ScaleAmountMin = 2.5f,
            ScaleAmountMax = 5.0f,
            Color = new Color(0.2f, 0.95f, 0.3f, 0.95f)
        };
        particles.Finished += particles.QueueFree;
        parent.AddChild(particles);
    }
}
