using System.Collections.Generic;
using Godot;

public partial class FragGrenade : Area2D
{
    private Vector2 _startPos;
    private Vector2 _targetPos;
    private float _flightTime = 0.65f;
    private float _elapsed = 0.0f;
    private bool _isDetonating = false;

    private Polygon2D? _bodyPoly;

    public void Launch(Vector2 startPos, Vector2 targetPos)
    {
        _startPos = startPos;
        Vector2 diff = targetPos - startPos;
        float maxRange = 450.0f;
        if (diff.Length() > maxRange)
        {
            _targetPos = startPos + diff.Normalized() * maxRange;
        }
        else
        {
            _targetPos = targetPos;
        }

        GlobalPosition = _startPos;
    }

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 0;

        int points = 12;
        Vector2[] verts = new Vector2[points];
        for (int i = 0; i < points; i++)
        {
            float ang = (i / (float)points) * Mathf.Tau;
            verts[i] = new Vector2(Mathf.Cos(ang) * 7f, Mathf.Sin(ang) * 7f);
        }

        _bodyPoly = new Polygon2D
        {
            Polygon = verts,
            Color = new Color(0.28f, 0.38f, 0.22f, 1.0f)
        };
        AddChild(_bodyPoly);

        // Pin detail
        var pin = new Polygon2D
        {
            Polygon = new[]
            {
                new Vector2(-3, -7), new Vector2(3, -7),
                new Vector2(2, -9), new Vector2(-2, -9)
            },
            Color = new Color(0.7f, 0.7f, 0.75f, 1.0f)
        };
        AddChild(pin);
    }

    public override void _Process(double delta)
    {
        if (_isDetonating) return;

        _elapsed += (float)delta;
        float t = Mathf.Clamp(_elapsed / _flightTime, 0.0f, 1.0f);

        GlobalPosition = _startPos.Lerp(_targetPos, t);
        float arc = 1.0f + Mathf.Sin(t * Mathf.Pi) * 0.85f;
        Scale = Vector2.One * arc;
        Rotation += (float)delta * 12.0f;

        if (t >= 1.0f)
        {
            StartDetonationSequence();
        }
    }

    private void StartDetonationSequence()
    {
        _isDetonating = true;
        Scale = Vector2.One;

        if (_bodyPoly != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(_bodyPoly, "modulate", new Color(4.0f, 4.0f, 4.0f, 1.0f), 0.1);
            tween.TweenProperty(_bodyPoly, "modulate", Colors.White, 0.1);
            tween.TweenCallback(Callable.From(Detonate));
        }
        else
        {
            Detonate();
        }
    }

    private void Detonate()
    {
        float blastRadius = 160.0f;
        var spaceState = GetWorld2D().DirectSpaceState;

        // Visual Explosion Particles
        var parent = GetParent();
        if (parent != null)
        {
            var particles = new CpuParticles2D
            {
                GlobalPosition = GlobalPosition,
                Emitting = true,
                OneShot = true,
                Explosiveness = 0.95f,
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
            particles.Finished += particles.QueueFree;
            parent.AddChild(particles);
        }

        // Play audio, hitstop, decal
        AudioManager.Instance?.PlayAt("SfxExplosion", GlobalPosition);
        HitStopManager.Instance?.TriggerHitStop(0.05f, 0.04f);
        DecalManager.Instance?.SpawnScorchMark(GlobalPosition, 60f);

        // Check damage targets: zombies, explosive barrels, and player
        var targets = new List<Node2D>();
        foreach (var node in GetTree().GetNodesInGroup("zombies"))
        {
            if (node is Node2D n) targets.Add(n);
        }
        foreach (var node in GetTree().GetNodesInGroup("explosive_barrels"))
        {
            if (node is Node2D n) targets.Add(n);
        }

        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null)
        {
            targets.Add(player);
        }

        foreach (var target in targets)
        {
            if (!IsInstanceValid(target)) continue;

            float dist = GlobalPosition.DistanceTo(target.GlobalPosition);
            if (dist > blastRadius) continue;

            // Line of sight check on Layer 2 (Obstacles)
            var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, target.GlobalPosition, 2);
            var hit = spaceState.IntersectRay(query);
            if (hit.Count > 0)
            {
                // Obstructed by obstacle
                continue;
            }

            if (target is Player p)
            {
                p.TakeDamage(30f, (p.GlobalPosition - GlobalPosition).Normalized());
                p.AddTrauma(0.5f);
            }
            else
            {
                if (target.HasMethod("TakeDamage"))
                {
                    target.Call("TakeDamage", 110f, GlobalPosition, 2.5f);
                }
            }
        }

        QueueFree();
    }
}
