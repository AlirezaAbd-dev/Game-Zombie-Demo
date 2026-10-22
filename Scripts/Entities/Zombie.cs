using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Entities.States;

namespace TopDownDemo.Entities;

public partial class Zombie : CharacterBody2D, IDamageable
{
    public static event Action OnAnyZombieKilled;

    [Export] public EnemyStats Stats { get; set; }

    [ExportGroup("Components")]
    [Export] public HealthComponent HealthComponent { get; set; }
    [Export] public MovementComponent MovementComponent { get; set; }

    [ExportGroup("Visuals & Triggers")]
    [Export] public Sprite2D Sprite { get; set; }
    [Export] public Area2D AttackArea { get; set; }
    [Export] public PointLight2D EyeGlow { get; set; }

    [ExportGroup("Drop Scenes")]
    [Export] public PackedScene PickupScene { get; set; }
    [Export] public ItemData AmmoDropData { get; set; }
    [Export] public ItemData MedkitDropData { get; set; }

    public StateMachine StateMachine { get; private set; }
    public ZombieIdleState IdleState { get; private set; }
    public ZombieChaseState ChaseState { get; private set; }
    public ZombieAttackState AttackState { get; private set; }
    public ZombieDeadState DeadState { get; private set; }

    public Node2D TargetPlayer { get; private set; }
    public float SpeedModifier { get; private set; } = 1.0f;
    public bool IsDead => HealthComponent?.IsDead ?? false;

    private float _walkPhase = 0.0f;
    private readonly Vector2 _baseScale = new(3.0f, 3.0f);

    public override void _Ready()
    {
        AddToGroup("zombies");
        ValidateAndBindNodes();

        Stats ??= GD.Load<EnemyStats>("res://Resources/Enemies/ZombieStats.tres") ?? new EnemyStats();
        HealthComponent?.Initialize(Stats.MaxHealth);

        SpeedModifier = (float)GD.RandRange(0.85, 1.2);
        _walkPhase = (float)GD.RandRange(0.0, Mathf.Tau);

        StateMachine = new StateMachine();
        AddChild(StateMachine);

        IdleState = new ZombieIdleState(this);
        ChaseState = new ZombieChaseState(this);
        AttackState = new ZombieAttackState(this);
        DeadState = new ZombieDeadState(this);

        if (HealthComponent != null)
        {
            HealthComponent.Died += () => StateMachine.ChangeState(DeadState);
        }

        FindPlayer();
        StateMachine.ChangeState(HasValidTarget() ? ChaseState : IdleState);
    }

    private void ValidateAndBindNodes()
    {
        HealthComponent ??= GetNodeOrNull<HealthComponent>("%HealthComponent");
        MovementComponent ??= GetNodeOrNull<MovementComponent>("%MovementComponent");
        Sprite ??= GetNodeOrNull<Sprite2D>("%Sprite2D");
        AttackArea ??= GetNodeOrNull<Area2D>("%AttackArea");
        EyeGlow ??= GetNodeOrNull<PointLight2D>("%EyeGlow");

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
        MovementComponent?.ApplyKnockback(knockbackDir, 320.0f);

        if (Sprite != null)
        {
            var tween = CreateTween();
            Sprite.Modulate = new Color(3.5f, 0.4f, 0.4f, 1.0f);
            tween.TweenProperty(Sprite, "modulate", Colors.White, 0.1);
        }
    }

    public void PerformDeath()
    {
        var parent = GetParent();
        if (parent != null)
        {
            var deathFx = new CpuParticles2D
            {
                GlobalPosition = GlobalPosition,
                Emitting = true,
                OneShot = true,
                Explosiveness = 1.0f,
                Amount = 32,
                Lifetime = 0.6f,
                Spread = 180.0f,
                InitialVelocityMin = 40.0f,
                InitialVelocityMax = 160.0f,
                Gravity = Vector2.Zero,
                ScaleAmountMin = 3.0f,
                ScaleAmountMax = 8.0f,
                Color = new Color(0.7f, 0.08f, 0.08f, 0.9f)
            };
            deathFx.Finished += deathFx.QueueFree;
            parent.AddChild(deathFx);

            SpawnDrop(parent);
        }

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
            var pickup = PickupScene.Instantiate<Pickup>();
            pickup.GlobalPosition = GlobalPosition;
            pickup.Item = AmmoDropData ?? GD.Load<ItemData>("res://Resources/Items/AmmoPack.tres");
            parent.CallDeferred("add_child", pickup);
        }
        else if (roll < Stats.AmmoDropChance + Stats.MedkitDropChance)
        {
            var pickup = PickupScene.Instantiate<Pickup>();
            pickup.GlobalPosition = GlobalPosition;
            pickup.Item = MedkitDropData ?? GD.Load<ItemData>("res://Resources/Items/Medkit.tres");
            parent.CallDeferred("add_child", pickup);
        }
    }
}
