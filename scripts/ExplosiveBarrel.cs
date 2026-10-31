using System.Collections.Generic;
using Godot;

public partial class ExplosiveBarrel : StaticBody2D
{
    [Export] public float Health { get; set; } = 40.0f;

    private bool _isDetonating = false;
    private Polygon2D? _outerPoly;
    private Polygon2D? _innerPoly;

    public override void _Ready()
    {
        AddToGroup("explosive_barrels");
        CollisionLayer = 3; // Layers 1 and 2
        CollisionMask = 0;

        int points = 8;
        Vector2[] outerVerts = new Vector2[points];
        Vector2[] innerVerts = new Vector2[points];

        for (int i = 0; i < points; i++)
        {
            float ang = (i / (float)points) * Mathf.Tau + (Mathf.Pi / 8f);
            outerVerts[i] = new Vector2(Mathf.Cos(ang) * 22f, Mathf.Sin(ang) * 22f);
            innerVerts[i] = new Vector2(Mathf.Cos(ang) * 14f, Mathf.Sin(ang) * 14f);
        }

        _outerPoly = new Polygon2D
        {
            Polygon = outerVerts,
            Color = new Color(0.85f, 0.18f, 0.15f, 1.0f)
        };
        AddChild(_outerPoly);

        _innerPoly = new Polygon2D
        {
            Polygon = innerVerts,
            Color = new Color(0.95f, 0.72f, 0.12f, 1.0f)
        };
        AddChild(_innerPoly);

        var col = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = 22.0f }
        };
        AddChild(col);

        // Subtle orange point light
        var lightTex = GD.Load<Texture2D>("res://assets/textures/light_radial.png");
        if (lightTex != null)
        {
            var light = new PointLight2D
            {
                Texture = lightTex,
                Color = new Color(1.0f, 0.5f, 0.1f, 1.0f),
                Energy = 0.6f,
                TextureScale = 0.45f
            };
            AddChild(light);
        }
    }

    public void TakeDamage(float amount, Vector2 hitOrigin = default, float knockbackMultiplier = 1.0f)
    {
        if (_isDetonating) return;

        Health -= amount;

        if (_outerPoly != null)
        {
            var tween = CreateTween();
            _outerPoly.Modulate = new Color(3.5f, 3.5f, 3.5f, 1.0f);
            tween.TweenProperty(_outerPoly, "modulate", Colors.White, 0.1);
        }

        if (Health <= 0.0f)
        {
            StartDetonation();
        }
    }

    private void StartDetonation()
    {
        if (_isDetonating) return;
        _isDetonating = true;

        var tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector2.One * 1.25f, 0.25);
        tween.TweenCallback(Callable.From(Detonate));
    }

    private void Detonate()
    {
        float blastRadius = 165.0f;

        // Particles
        var parent = GetParent();
        if (parent != null)
        {
            var fx = new CpuParticles2D
            {
                GlobalPosition = GlobalPosition,
                Emitting = true,
                OneShot = true,
                Explosiveness = 1.0f,
                Amount = 45,
                Lifetime = 0.55f,
                Spread = 180f,
                Gravity = Vector2.Zero,
                InitialVelocityMin = 60f,
                InitialVelocityMax = 220f,
                ScaleAmountMin = 4f,
                ScaleAmountMax = 9f,
                Color = new Color(1.0f, 0.55f, 0.15f, 1.0f)
            };
            fx.Finished += fx.QueueFree;
            parent.AddChild(fx);
        }

        AudioManager.Instance?.PlayAt("SfxExplosion", GlobalPosition);
        HitStopManager.Instance?.TriggerHitStop(0.05f, 0.045f);
        DecalManager.Instance?.SpawnScorchMark(GlobalPosition, 65f);

        // Player check
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null && IsInstanceValid(player))
        {
            if (GlobalPosition.DistanceTo(player.GlobalPosition) <= blastRadius)
            {
                player.TakeDamage(35.0f, (player.GlobalPosition - GlobalPosition).Normalized());
                player.AddTrauma(0.75f);
            }
        }

        // Zombie damage
        foreach (var node in GetTree().GetNodesInGroup("zombies"))
        {
            if (node is Zombie z && IsInstanceValid(z))
            {
                if (GlobalPosition.DistanceTo(z.GlobalPosition) <= blastRadius)
                {
                    z.TakeDamage(125.0f, GlobalPosition, 2.0f);
                }
            }
        }

        // Barrel chain reactions
        foreach (var node in GetTree().GetNodesInGroup("explosive_barrels"))
        {
            if (node != this && node is ExplosiveBarrel b && IsInstanceValid(b))
            {
                if (GlobalPosition.DistanceTo(b.GlobalPosition) <= blastRadius)
                {
                    b.TakeDamage(125.0f, GlobalPosition, 1.0f);
                }
            }
        }

        QueueFree();
    }
}
