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

    [Export] public string ActivationFlag { get; set; } = string.Empty;
    [Export] public bool AuthoredReinforcements { get; set; }
    [Export] public BossCombatData CombatData { get; set; }
    public BossCombatComponent Combat { get; private set; }
    public bool IsEncounterActive => string.IsNullOrEmpty(ActivationFlag) || StoryDirector.Instance?.HasFlag(ActivationFlag) == true;
    private ObstacleSteeringComponent _steering;
    private StoryDirector _story;
    private bool _activated;

    public HealthComponent HealthComponent { get; private set; }
    public MovementComponent MovementComponent { get; private set; }
    public Sprite2D Sprite { get; private set; }
    public PointLight2D EyeGlow { get; private set; }

    public int CurrentPhase { get; private set; } = 1;
    public bool IsDead => HealthComponent?.IsDead ?? false;

    private Node2D _targetPlayer;
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

        Combat = new BossCombatComponent { Data = CombatData };
        AddChild(Combat);
        _steering = new ObstacleSteeringComponent { Body = this };
        AddChild(_steering);
        AddChild(new BossTelegraphView { Boss = this });
        CallDeferred(nameof(InitializeEncounter));
    }

    private void InitializeEncounter()
    {
        if (IsQueuedForDeletion()) return;
        _story = StoryDirector.Instance;
        if (_story == null) return;
        _story.FlagsChanged += RefreshEncounter;
        RefreshEncounter();
    }

    private void RefreshEncounter()
    {
        if (_activated || !IsEncounterActive || IsDead) return;
        _activated = true;
        _story.SetFlag(StoryFlags.UNIT9_ENCOUNTERED);
        if (CurrentPhase >= 2) _story.SetFlag("unit9_phase2");
        AudioManager.Instance?.PlayMusic("boss", 1.5f);
        HUDUI.Instance?.UpdateBossHealth(HealthComponent.CurrentHealth, MaxHealth);
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_story)) _story.FlagsChanged -= RefreshEncounter;
    }

    public void ApplyStagger(float seconds)
    {
        Combat?.Cancel();
        _isRoaring = true;
        _phaseRoarTimer = Mathf.Max(_phaseRoarTimer, seconds);
    }

    private void FindPlayer()
    {
        _targetPlayer = GetTree().GetFirstNodeInGroup("player") as Node2D;
    }

    public void TakeDamage(float amount, Vector2 knockbackDir)
    {
        if (IsDead || !IsEncounterActive) return;

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
        if (current <= 0) return;

        float healthRatio = current / max;

        int phase = healthRatio <= 0.33f ? 3 : healthRatio <= 0.66f ? 2 : 1;
        if (phase > CurrentPhase) TransitionToPhase(phase);
    }

    private void TransitionToPhase(int newPhase)
    {
        CurrentPhase = newPhase;
        ApplyStagger(1.6f);
        if (newPhase >= 2) StoryDirector.Instance?.SetFlag("unit9_phase2");

        AudioManager.Instance?.PlaySound(SoundType.BossRoar, GlobalPosition);

        if (Sprite != null)
        {
            Sprite.Modulate = GetPhaseColor();
        }

        if (newPhase == 2)
        {
            MovementComponent.BaseSpeed = BaseSpeed * 1.25f;
            HUDUI.Instance?.ShowWarningBanner("UNIT-9 MUTATION ESCALATED — CORROSIVE SUBSTANCE DETECTED!");
            if (!AuthoredReinforcements) SpawnMinions(2);
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

        if (IsDead || !IsEncounterActive) { Combat?.Cancel(); MovementComponent?.Stop(); return; }

        if (_targetPlayer == null || !GodotObject.IsInstanceValid(_targetPlayer))
        {
            FindPlayer();
            return;
        }
        if (_targetPlayer is Player { IsDead: true }) { MovementComponent?.Stop(); return; }

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

        Vector2 toPlayer = _targetPlayer.GlobalPosition - GlobalPosition;
        float distance = toPlayer.Length();
        Vector2 dir = toPlayer.Normalized();

        Rotation = Mathf.LerpAngle(Rotation, dir.Angle(), dt * 6.0f);

        if (_targetPlayer is not Player target) return;
        if (Combat.Step(dt, target))
        {
            MovementComponent?.Stop();
            if (Combat.Phase is BossAttackPhase.MeleeWindup or BossAttackPhase.AcidWindup)
                Rotation = Combat.LockedDirection.Angle();
            return;
        }
        if (distance > AttackRange || !Combat.HasSight(target))
        {
            Vector2 movement = _steering.GetDirection(target);
            if (movement == Vector2.Zero) MovementComponent.Stop();
            else MovementComponent.Move(movement);
            _walkPhase += dt * MovementComponent.BaseSpeed * 0.08f;
            if (Sprite != null) Sprite.Rotation = Mathf.Sin(_walkPhase) * 0.08f;
        }
        else MovementComponent.Stop();
    }

    private void OnDied()
    {
        Combat?.Cancel();
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
