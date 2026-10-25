using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;
using TopDownDemo.UI;

namespace TopDownDemo.Entities;

public partial class Boss_Unit9 : CharacterBody2D, IDamageable
{
    [Signal]
    public delegate void BossDiedEventHandler();

    [Export] public float MaxHealth { get; set; } = 800.0f;
    [Export] public float BaseSpeed { get; set; } = 150.0f;
    [Export] public float AttackDamage { get; set; } = 35.0f;
    [Export] public float AttackRange { get; set; } = 90.0f;

    public HealthComponent HealthComponent { get; private set; }
    public MovementComponent MovementComponent { get; private set; }
    public Sprite2D Sprite { get; private set; }
    public PointLight2D EyeGlow { get; private set; }

    public int CurrentPhase { get; private set; } = 1;
    public bool IsDead => HealthComponent?.IsDead ?? false;

    private Node2D _targetPlayer;
    private float _attackCooldown = 0.0f;
    private float _specialAttackCooldown = 6.0f;
    private float _phaseRoarTimer = 0.0f;
    private bool _isRoaring = false;
    private Vector2 _baseScale = new(4.2f, 4.2f);
    private float _walkPhase = 0.0f;

    public override void _Ready()
    {
        AddToGroup("zombies");
        AddToGroup("boss");

        HealthComponent = GetNodeOrNull<HealthComponent>("HealthComponent");
        if (HealthComponent == null)
        {
            HealthComponent = new HealthComponent
            {
                Name = "HealthComponent",
                MaxHealth = MaxHealth
            };
            AddChild(HealthComponent);
        }

        MovementComponent = GetNodeOrNull<MovementComponent>("MovementComponent");
        if (MovementComponent == null)
        {
            MovementComponent = new MovementComponent
            {
                Name = "MovementComponent",
                BaseSpeed = BaseSpeed
            };
            AddChild(MovementComponent);
        }

        Sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (Sprite == null)
        {
            Sprite = new Sprite2D
            {
                Name = "Sprite2D",
                TextureFilter = TextureFilterEnum.Nearest,
                Scale = _baseScale,
                Modulate = new Color(0.85f, 0.35f, 0.35f)
            };
            var tex = GD.Load<Texture2D>("res://Assets/textures/zombie_brute.png") ?? GD.Load<Texture2D>("res://Assets/textures/zombie.png");
            Sprite.Texture = tex;
            AddChild(Sprite);
        }

        EyeGlow = GetNodeOrNull<PointLight2D>("EyeGlow");
        if (EyeGlow == null)
        {
            EyeGlow = new PointLight2D
            {
                Name = "EyeGlow",
                Color = new Color(1.0f, 0.2f, 0.1f),
                Energy = 1.4f,
                Texture = GD.Load<Texture2D>("res://Assets/textures/light_radial.png"),
                TextureScale = 1.2f
            };
            AddChild(EyeGlow);
        }

        // Add collision shape
        var col = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (col == null)
        {
            col = new CollisionShape2D
            {
                Name = "CollisionShape2D",
                Shape = new CircleShape2D { Radius = 42.0f }
            };
            AddChild(col);
        }

        CollisionLayer = 2; // Enemy layer
        CollisionMask = 1 | 2; // Collide with walls & player

        HealthComponent.HealthChanged += OnHealthChanged;
        HealthComponent.Died += OnDied;

        FindPlayer();

        // Register to StoryDirector and play boss music
        StoryDirector.Instance?.SetFlag(StoryFlags.UNIT9_ENCOUNTERED);
        AudioManager.Instance?.PlayMusic("boss", 1.5f);
    }

    private void FindPlayer()
    {
        _targetPlayer = GetTree().GetFirstNodeInGroup("player") as Node2D;
    }

    public void TakeDamage(float amount, Vector2 knockbackDir)
    {
        if (IsDead) return;

        // Difficulty damage mitigation
        if (StoryDirector.Instance != null && StoryDirector.Instance.Difficulty == DifficultyLevel.Researcher)
        {
            amount *= 0.8f;
        }

        HealthComponent?.TakeDamage(amount, knockbackDir * 0.15f);

        // Flash white on hit
        if (Sprite != null)
        {
            Sprite.Modulate = new Color(3.5f, 3.5f, 3.5f, 1.0f);
            var tween = CreateTween();
            tween.TweenProperty(Sprite, "modulate", GetPhaseColor(), 0.08f);
        }
    }

    private Color GetPhaseColor()
    {
        return CurrentPhase switch
        {
            1 => new Color(0.9f, 0.4f, 0.4f),
            2 => new Color(1.0f, 0.25f, 0.15f),
            3 => new Color(1.5f, 0.15f, 0.15f), // Glowing red berserk
            _ => Colors.White
        };
    }

    private void OnHealthChanged(float current, float max)
    {
        HUDUI.Instance?.UpdateBossHealth(current, max);

        float healthRatio = current / max;

        if (CurrentPhase == 1 && healthRatio <= 0.66f)
        {
            TransitionToPhase(2);
        }
        else if (CurrentPhase == 2 && healthRatio <= 0.33f)
        {
            TransitionToPhase(3);
        }
    }

    private void TransitionToPhase(int newPhase)
    {
        CurrentPhase = newPhase;
        _isRoaring = true;
        _phaseRoarTimer = 1.6f;

        AudioManager.Instance?.PlaySound(SoundType.BossRoar, GlobalPosition);

        if (Sprite != null)
        {
            Sprite.Modulate = GetPhaseColor();
        }

        if (newPhase == 2)
        {
            MovementComponent.BaseSpeed = BaseSpeed * 1.25f;
            HUDUI.Instance?.ShowWarningBanner("UNIT-9 MUTATION ESCALATED — CORROSIVE SUBSTANCE DETECTED!");
            SpawnMinions(2);
        }
        else if (newPhase == 3)
        {
            MovementComponent.BaseSpeed = BaseSpeed * 1.55f;
            AttackDamage *= 1.35f;
            if (EyeGlow != null) EyeGlow.Energy = 2.8f;
            HUDUI.Instance?.ShowWarningBanner("CRITICAL: UNIT-9 ENTERED HYPER-AGGRESSIVE BERSERK STATE!");
        }
    }

    private void SpawnMinions(int count)
    {
        var zombiePrefab = GD.Load<PackedScene>("res://Prefabs/Zombie.tscn");
        if (zombiePrefab == null) return;

        for (int i = 0; i < count; i++)
        {
            var minion = zombiePrefab.Instantiate<Zombie>();
            minion.GlobalPosition = GlobalPosition + new Vector2(GD.Randf() * 160f - 80f, GD.Randf() * 160f - 80f);
            GetParent().CallDeferred("add_child", minion);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        MovementComponent?.DecayKnockback(dt);

        if (IsDead) return;

        if (_targetPlayer == null || !GodotObject.IsInstanceValid(_targetPlayer))
        {
            FindPlayer();
            return;
        }

        if (_isRoaring)
        {
            _phaseRoarTimer -= dt;
            MovementComponent?.Stop();
            // Screen shake while roaring
            if (_targetPlayer is Player p)
            {
                p.AddTrauma(0.2f);
            }
            if (_phaseRoarTimer <= 0.0f)
            {
                _isRoaring = false;
            }
            return;
        }

        _attackCooldown -= dt;
        _specialAttackCooldown -= dt;

        Vector2 toPlayer = _targetPlayer.GlobalPosition - GlobalPosition;
        float distance = toPlayer.Length();
        Vector2 dir = toPlayer.Normalized();

        Rotation = Mathf.LerpAngle(Rotation, dir.Angle(), dt * 6.0f);

        // Acid spit special attack in Phase 2 & 3
        if ((CurrentPhase >= 2) && _specialAttackCooldown <= 0.0f && distance > 140.0f)
        {
            ExecuteAcidSpit(dir);
            _specialAttackCooldown = CurrentPhase == 3 ? 4.0f : 7.0f;
        }

        if (distance > AttackRange)
        {
            // Chase player
            MovementComponent?.Move(dir, 1.0f);
            _walkPhase += dt * (MovementComponent?.BaseSpeed ?? 150f) * 0.08f;
            if (Sprite != null)
            {
                float sway = Mathf.Sin(_walkPhase) * 0.08f;
                Sprite.Rotation = sway;
            }
        }
        else
        {
            MovementComponent?.Stop();
            if (_attackCooldown <= 0.0f)
            {
                ExecuteMeleeAttack();
            }
        }
    }

    private void ExecuteAcidSpit(Vector2 dir)
    {
        AudioManager.Instance?.PlaySound(SoundType.VentBreak, GlobalPosition);

        // Spawn acid projectile using ProjectilePool or direct Node
        if (ProjectilePool.Instance != null)
        {
            for (int i = -1; i <= 1; i++)
            {
                float angleOffset = i * 0.25f;
                ProjectilePool.Instance.Spawn(GlobalPosition + dir * 45f, dir.Angle() + angleOffset, GetParent(), 20.0f, 320.0f, isEnemy: true);
            }
        }
    }

    private void ExecuteMeleeAttack()
    {
        _attackCooldown = CurrentPhase == 3 ? 0.75f : 1.3f;
        AudioManager.Instance?.PlaySound(SoundType.FinisherSlash, GlobalPosition);

        if (_targetPlayer is Player player && GodotObject.IsInstanceValid(player))
        {
            float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
            if (dist <= AttackRange + 25.0f)
            {
                Vector2 knockback = (player.GlobalPosition - GlobalPosition).Normalized();
                player.HealthComponent?.TakeDamage(AttackDamage, knockback);
                player.AddTrauma(0.45f);
            }
        }
    }

    private void OnDied()
    {
        AudioManager.Instance?.PlaySound(SoundType.BossRoar, GlobalPosition);
        StoryDirector.Instance?.SetFlag(StoryFlags.UNIT9_DEFEATED);
        EmitSignal(SignalName.BossDied);

        // Hide boss bar
        HUDUI.Instance?.UpdateBossHealth(0, MaxHealth);

        // Death fade animation
        Tween deathTween = CreateTween();
        deathTween.TweenProperty(this, "modulate:a", 0.0f, 2.0f);
        deathTween.TweenCallback(Callable.From(() =>
        {
            QueueFree();
        }));
    }
}
