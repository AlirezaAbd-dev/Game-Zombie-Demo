using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Entities.States;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities;

public partial class Zombie : CharacterBody2D, IDamageable
{
    [Signal]
    public delegate void DiedEventHandler();

    public static event Action OnAnyZombieKilled;

    [Export] public EnemyStats Stats { get; set; }

    [ExportGroup("Components")]
    [Export] public HealthComponent HealthComponent { get; set; }
    [Export] public MovementComponent MovementComponent { get; set; }
    [Export] public FinisherTargetComponent FinisherTarget { get; set; }

    [ExportGroup("Visuals & Triggers")]
    [Export] public Sprite2D Sprite { get; set; }
    [Export] public Area2D AttackArea { get; set; }
    [Export] public PointLight2D EyeGlow { get; set; }

    [ExportGroup("Drop Scenes")]
    [Export] public PackedScene PickupScene { get; set; }

    public StateMachine StateMachine { get; private set; }
    public ZombieIdleState IdleState { get; private set; }
    public ZombieChaseState ChaseState { get; private set; }
    public ZombieAttackState AttackState { get; private set; }
    public ZombieStunnedState StunnedState { get; private set; }
    public ZombieDeadState DeadState { get; private set; }

    public Node2D TargetPlayer { get; private set; }
    public float SpeedModifier { get; private set; } = 1.0f;
    public bool IsDead => HealthComponent?.IsDead ?? false;

    private float _walkPhase = 0.0f;
    private Vector2 _baseScale = new(3.0f, 3.0f);

    public override void _Ready()
    {
        AddToGroup("zombies");
        ValidateAndBindNodes();

        Stats ??= GD.Load<EnemyStats>("res://Resources/Enemies/Zombie_Walker.tres") ?? new EnemyStats();
        ApplyStats(Stats);

        SpeedModifier = (float)GD.RandRange(0.9, 1.15);
        _walkPhase = (float)GD.RandRange(0.0, Mathf.Tau);

        StateMachine = new StateMachine();
        AddChild(StateMachine);

        IdleState = new ZombieIdleState(this);
        ChaseState = new ZombieChaseState(this);
        AttackState = new ZombieAttackState(this);
        StunnedState = new ZombieStunnedState(this);
        DeadState = new ZombieDeadState(this);

        if (HealthComponent != null)
        {
            HealthComponent.Died += () => StateMachine.ChangeState(DeadState);
        }

        FindPlayer();
        StateMachine.ChangeState(HasValidTarget() ? ChaseState : IdleState);
    }

    public void ApplyStats(EnemyStats stats)
    {
        if (stats == null) return;
        Stats = stats;

        // Normalize sprite scale based on texture resolution (boss texture is 96px, base grid is 32px)
        float textureScaleFactor = 1.0f;
        if (stats.CustomTexture != null && stats.CustomTexture.GetWidth() > 32)
        {
            textureScaleFactor = 32.0f / (float)stats.CustomTexture.GetWidth();
        }

        // Apply scale strictly to visual sprite, keeping root physics scale uniform (1.0)
        _baseScale = new Vector2(3.0f, 3.0f) * textureScaleFactor * Stats.ScaleMultiplier;
        Scale = Vector2.One;

        if (Sprite != null)
        {
            Sprite.Scale = _baseScale;
            if (Stats.CustomTexture != null)
            {
                Sprite.Texture = Stats.CustomTexture;
            }
            Sprite.Modulate = Stats.Tint;
        }

        // Configure collision shape to accurately match the enemy's visual size and prevent wall jamming
        UpdateCollisionShapeForStats(stats);

        HealthComponent?.Initialize(Stats.MaxHealth);

        if (MovementComponent != null)
        {
            MovementComponent.BaseSpeed = Stats.BaseSpeed;
        }

        if (EyeGlow != null)
        {
            EyeGlow.Color = Stats.EyeLightColor;
            EyeGlow.Energy = Stats.EyeLightEnergy;
        }
    }

    private void UpdateCollisionShapeForStats(EnemyStats stats)
    {
        var colShapeNode = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (colShapeNode == null) return;

        colShapeNode.Position = Vector2.Zero;

        if (stats.TypeName == "Specimen Zero")
        {
            // Smooth circular navigation hull for the boss: covers full 134px body and glides along walls
            colShapeNode.Shape = new CircleShape2D { Radius = 44.0f };
            if (AttackArea != null)
            {
                var attackCol = AttackArea.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
                if (attackCol != null)
                {
                    attackCol.Position = Vector2.Zero;
                    attackCol.Shape = new CircleShape2D { Radius = 72.0f };
                }
            }
        }
        else
        {
            // Circular navigation hull for standard zombies (no sharp corners snagging on walls/boxes)
            colShapeNode.Shape = new CircleShape2D { Radius = 24.0f };
        }
    }

    private void ValidateAndBindNodes()
    {
        HealthComponent ??= GetNodeOrNull<HealthComponent>("%HealthComponent");
        MovementComponent ??= GetNodeOrNull<MovementComponent>("%MovementComponent");
        Sprite ??= GetNodeOrNull<Sprite2D>("%Sprite2D");
        AttackArea ??= GetNodeOrNull<Area2D>("%AttackArea");
        EyeGlow ??= GetNodeOrNull<PointLight2D>("%EyeGlow");
        FinisherTarget ??= GetNodeOrNull<FinisherTargetComponent>("%FinisherTargetComponent");

        if (FinisherTarget == null)
        {
            FinisherTarget = new FinisherTargetComponent { Name = "FinisherTargetComponent" };
            AddChild(FinisherTarget);
        }

        if (HealthComponent == null) GD.PrintErr($"[Zombie] Missing HealthComponent on {GetPath()}");
        if (MovementComponent == null) GD.PrintErr($"[Zombie] Missing MovementComponent on {GetPath()}");
        if (Sprite == null) GD.PrintErr($"[Zombie] Missing Sprite on {GetPath()}");
    }

    public void FindPlayer()
    {
        TargetPlayer = (Node2D)GetTree().GetFirstNodeInGroup("player");
    }

    public bool HasValidTarget()
    {
        if (!IsInstanceValid(TargetPlayer))
        {
            FindPlayer();
        }
        return IsInstanceValid(TargetPlayer);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        MovementComponent?.DecayKnockback(dt);
        StateMachine?.PhysicsUpdate(dt);

        if (!IsDead)
        {
            GlobalPosition = WorldManager.ClampToPlayableBounds(GlobalPosition);
        }
    }

    public void RotateTowards(Vector2 direction, float delta)
    {
        Rotation = Mathf.LerpAngle(Rotation, direction.Angle(), delta * 8.0f);
    }

    public void AnimateWalk(float delta)
    {
        _walkPhase += delta * 9.0f;
        float wobble = Mathf.Sin(_walkPhase) * 0.1f;
        if (Sprite != null)
        {
            Sprite.Rotation = wobble;
            Sprite.Scale = _baseScale + new Vector2(wobble * 0.4f, -wobble * 0.4f);
        }
    }

    public void PerformAttack()
    {
        if (AttackArea == null) return;

        foreach (Node2D body in AttackArea.GetOverlappingBodies())
        {
            if (body.IsInGroup("player") && body is IDamageable playerDamageable)
            {
                Vector2 attackDir = (body.GlobalPosition - GlobalPosition).Normalized();
                playerDamageable.TakeDamage(Stats.AttackDamage, attackDir);

                if (Sprite != null)
                {
                    var tween = CreateTween();
                    tween.TweenProperty(Sprite, "position", new Vector2(12, 0), 0.08);
                    tween.TweenProperty(Sprite, "position", Vector2.Zero, 0.15);
                }
                break;
            }
        }
    }

    public void TakeDamage(float amount, Vector2 knockbackDir)
    {
        if (IsDead) return;

        HealthComponent?.TakeDamage(amount, knockbackDir);
        float effectiveKnockback = 320.0f * (1.0f - Mathf.Clamp(Stats.KnockbackResistance, 0.0f, 0.9f));
        MovementComponent?.ApplyKnockback(knockbackDir, effectiveKnockback);

        SoundType sType = Stats.TypeName == "Specimen Zero" ? SoundType.BossRoar : SoundType.ZombieGroan;
        AudioManager.Instance?.PlaySound(sType, GlobalPosition, (float)GD.RandRange(0.85, 1.15));

        if (Sprite != null)
        {
            var tween = CreateTween();
            // Brilliant white hit-flash for punchy, immediate hit confirmation
            Sprite.Modulate = new Color(4.5f, 4.5f, 4.5f, 1.0f);
            tween.TweenProperty(Sprite, "modulate", Stats.Tint, 0.08);
        }

        FinisherTarget?.OnDamageTaken(amount);
    }

    public void OnStunned()
    {
        if (IsDead) return;
        StateMachine?.ChangeState(StunnedState);
    }

    public void OnStunRecovered()
    {
        if (IsDead) return;
        StateMachine?.ChangeState(HasValidTarget() ? ChaseState : IdleState);
    }

    public void PerformDeath()
    {
        var parent = GetParent();
        if (parent != null)
        {
            float fxScale = Stats.ScaleMultiplier;
            var deathFx = new CpuParticles2D
            {
                GlobalPosition = GlobalPosition,
                Emitting = true,
                OneShot = true,
                Explosiveness = 1.0f,
                Amount = (int)(32 * fxScale),
                Lifetime = 0.6f,
                Spread = 180.0f,
                InitialVelocityMin = 40.0f,
                InitialVelocityMax = 160.0f * fxScale,
                Gravity = Vector2.Zero,
                ScaleAmountMin = 3.0f * fxScale,
                ScaleAmountMax = 8.0f * fxScale,
                Color = Stats.TypeName == "Runner" ? new Color(0.9f, 0.1f, 0.1f, 0.95f) : new Color(0.6f, 0.1f, 0.1f, 0.9f)
            };
            deathFx.Finished += deathFx.QueueFree;
            parent.AddChild(deathFx);

            SpawnDrop(parent);
        }

        StoryDirector.Instance?.RecordKill();
        EmitSignal(SignalName.Died);
        OnAnyZombieKilled?.Invoke();
        QueueFree();
    }

    private void SpawnDrop(Node parent)
    {
        PickupScene ??= GD.Load<PackedScene>("res://Prefabs/Pickup.tscn");
        if (PickupScene == null) return;

        float roll = (float)GD.RandRange(0.0, 1.0);
        if (roll < Stats.AmmoDropChance)
        {
            // Pick appropriate ammo drop
            float ammoRoll = (float)GD.RandRange(0.0, 1.0);
            string ammoPath = ammoRoll switch
            {
                < 0.50f => "res://Resources/Items/AmmoPack.tres",       // 9mm
                < 0.80f => "res://Resources/Items/AmmoShotgun.tres",    // 12G
                _ => "res://Resources/Items/AmmoRifle.tres"             // .308
            };

            var pickup = PickupScene.Instantiate<Pickup>();
            pickup.GlobalPosition = GlobalPosition;
            pickup.Item = GD.Load<ItemData>(ammoPath);
            parent.CallDeferred("add_child", pickup);
        }
        else if (roll < Stats.AmmoDropChance + Stats.MedkitDropChance)
        {
            string healPath = PickHealingDropForEnemy();
            var pickup = PickupScene.Instantiate<Pickup>();
            pickup.GlobalPosition = GlobalPosition;
            pickup.Item = GD.Load<ItemData>(healPath);
            parent.CallDeferred("add_child", pickup);
        }
    }

    private string PickHealingDropForEnemy()
    {
        float healRoll = (float)GD.RandRange(0.0, 1.0);
        string typeName = Stats?.TypeName ?? "";

        if (typeName == "Specimen Zero")
        {
            // Boss drops high-tier survival medical supplies
            return healRoll < 0.70f
                ? "res://Resources/Items/TraumaKit.tres"
                : "res://Resources/Items/Adrenaline.tres";
        }
        else if (typeName == "Brute")
        {
            // Brute drops substantial medical gear
            return healRoll switch
            {
                < 0.45f => "res://Resources/Items/Medkit.tres",         // +50 HP
                < 0.75f => "res://Resources/Items/Herb_Mixed.tres",     // +60 HP & Stamina
                _ => "res://Resources/Items/TraumaKit.tres"             // +100 HP Full Heal
            };
        }
        else if (typeName == "Runner")
        {
            // Fast Runner drops field dressings or herbs
            return healRoll switch
            {
                < 0.45f => "res://Resources/Items/Bandage.tres",        // +20 HP
                < 0.75f => "res://Resources/Items/Herb_Green.tres",     // +35 HP
                _ => "res://Resources/Items/Herb_Blue.tres"             // +50 Stamina Cleanse
            };
        }
        else
        {
            // Standard Walker drops bandages, herbs, or medkits
            return healRoll switch
            {
                < 0.55f => "res://Resources/Items/Bandage.tres",        // +20 HP
                < 0.88f => "res://Resources/Items/Herb_Green.tres",     // +35 HP
                _ => "res://Resources/Items/Medkit.tres"                // +50 HP
            };
        }
    }
}
