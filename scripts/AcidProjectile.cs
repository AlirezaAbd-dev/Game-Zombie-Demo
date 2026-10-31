using Godot;

public partial class AcidProjectile : Area2D
{
    [Export] public float Speed { get; set; } = 520.0f;
    [Export] public float Damage { get; set; } = 12.0f;
    [Export] public float MaxLifetime { get; set; } = 1.1f;

    private float _lifetime = 0.0f;
    private bool _hasHit = false;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 3; // Layer 1 (Player/Zombies) and Layer 2 (Obstacles)

        int points = 12;
        Vector2[] verts = new Vector2[points];
        for (int i = 0; i < points; i++)
        {
            float ang = (i / (float)points) * Mathf.Tau;
            verts[i] = new Vector2(Mathf.Cos(ang) * 7.0f, Mathf.Sin(ang) * 7.0f);
        }

        var poly = new Polygon2D
        {
            Polygon = verts,
            Color = new Color(0.35f, 1.0f, 0.25f, 0.95f)
        };
        AddChild(poly);

        var col = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = 7.0f }
        };
        AddChild(col);

        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_hasHit) return;

        Position += Transform.X * Speed * (float)delta;
        _lifetime += (float)delta;

        if (_lifetime >= MaxLifetime)
        {
            Impact(null);
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body.IsInGroup("zombies"))
            return;

        Impact(body);
    }

    private void Impact(Node2D? hitBody)
    {
        if (_hasHit) return;
        _hasHit = true;

        if (hitBody is Player p && !p.IsDead)
        {
            p.TakeDamage(Damage, Transform.X);
        }

        SpawnAcidPool();
        QueueFree();
    }

    private void SpawnAcidPool()
    {
        var parent = GetParent();
        if (parent == null) return;

        var pool = new AcidPool
        {
            GlobalPosition = GlobalPosition
        };
        parent.AddChild(pool);
    }
}
