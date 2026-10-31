using Godot;

public partial class AcidPool : Area2D
{
    private float _lifetime = 4.0f;
    private float _damageInterval = 0.5f;
    private float _damageTimer = 0.0f;
    private Player? _trackedPlayer;

    public override void _Ready()
    {
        ZIndex = -1;
        CollisionLayer = 0;
        CollisionMask = 1;

        int points = 24;
        Vector2[] verts = new Vector2[points];
        for (int i = 0; i < points; i++)
        {
            float ang = (i / (float)points) * Mathf.Tau;
            float r = 56.0f * (float)GD.RandRange(0.88, 1.12);
            verts[i] = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
        }

        var poly = new Polygon2D
        {
            Polygon = verts,
            Color = new Color(0.2f, 0.95f, 0.25f, 0.42f)
        };
        AddChild(poly);

        var col = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = 56.0f }
        };
        AddChild(col);

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _lifetime -= dt;

        _damageTimer += dt;
        if (_damageTimer >= _damageInterval)
        {
            _damageTimer = 0.0f;
            foreach (var body in GetOverlappingBodies())
            {
                if (body is Player p && !p.IsDead)
                {
                    p.TakeDamage(5.0f, Vector2.Zero);
                }
            }
        }

        if (_lifetime <= 0.0f)
        {
            Cleanup();
            QueueFree();
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player p)
        {
            _trackedPlayer = p;
            p.ExternalSpeedMultiplier = 0.62f;
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Player p)
        {
            p.ExternalSpeedMultiplier = 1.0f;
            if (_trackedPlayer == p)
                _trackedPlayer = null;
        }
    }

    public override void _ExitTree()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        if (IsInstanceValid(_trackedPlayer))
        {
            _trackedPlayer.ExternalSpeedMultiplier = 1.0f;
            _trackedPlayer = null;
        }
    }
}
