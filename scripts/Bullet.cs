using Godot;

public partial class Bullet : Area2D
{
    [Export] public float Speed { get; set; } = 1400.0f;
    [Export] public float Damage { get; set; } = 25.0f;
    [Export] public float MaxLifetime { get; set; } = 1.4f;

    private float _lifetime = 0.0f;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        Position += Transform.X * Speed * (float)delta;
        _lifetime += (float)delta;
        if (_lifetime >= MaxLifetime)
        {
            QueueFree();
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body.IsInGroup("player"))
            return;

        if (body.HasMethod("TakeDamage"))
        {
            body.Call("TakeDamage", Damage, Transform.X);
            SpawnBloodImpact(GlobalPosition);
        }
        else
        {
            SpawnSparkImpact(GlobalPosition);
        }

        QueueFree();
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
