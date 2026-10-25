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

    public Vector2 LastKnownPlayerPos { get; set; } = Vector2.Zero;
    public bool HasTargetAcquired { get; set; } = false;
    public float VisionRange { get; set; } = 550.0f;
    public float ProximityHearRange { get; set; } = 95.0f;
    public float EyeGlowBaseEnergy { get; private set; } = 0.65f;

    private readonly Godot.Collections.Array<Rid> _losExclude = new();
    private readonly Godot.Collections.Array<Rid> _steeringExclude = new();
    private static readonly Vector2[] CandidateDirs;
    private readonly float[] _candidateInterests = new float[12];
    private readonly float[] _candidateDangers = new float[12];
    private Vector2 _smoothedMoveDir = Vector2.Zero;

    static Zombie()
    {
        CandidateDirs = new Vector2[12];
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.Tau / 12.0f;
            CandidateDirs[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }

    private float _walkPhase = 0.0f;
    private Vector2 _baseScale = new(3.0f, 3.0f);

    public override void _Ready()
    {
        AddToGroup("zombies");
        _steeringExclude.Add(GetRid());
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
        bool isBoss = Stats?.TypeName == "Specimen Zero";
        StateMachine.ChangeState(isBoss && HasValidTarget() ? ChaseState : IdleState);
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
            EyeGlowBaseEnergy = Stats.EyeLightEnergy;
        }

        if (Stats.TypeName == "Specimen Zero")
        {
            VisionRange = 1200.0f;
        }
        else if (Stats.TypeName == "Runner")
        {
            VisionRange = 650.0f;
        }
        else
        {
            VisionRange = 520.0f;
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

        // When taking damage, immediately alert to the damage source and start chasing!
        if (TargetPlayer != null && GodotObject.IsInstanceValid(TargetPlayer))
        {
            AlertToPosition(TargetPlayer.GlobalPosition);
        }
    }

    public void OnStunned()
    {
        if (IsDead) return;
        StateMachine?.ChangeState(StunnedState);
    }

    public void OnStunRecovered()
    {
        if (IsDead) return;
        StateMachine?.ChangeState(CanSeePlayer() ? ChaseState : IdleState);
    }

    public void AlertToPosition(Vector2 pos)
    {
        if (IsDead) return;
        LastKnownPlayerPos = pos;
        HasTargetAcquired = true;
        if (StateMachine?.CurrentState == IdleState)
        {
            StateMachine.ChangeState(ChaseState);
        }
    }

    public void OnHearingNoise(Vector2 noisePos, float maxDistance = 450.0f)
    {
        if (IsDead) return;
        float dist = GlobalPosition.DistanceTo(noisePos);
        if (dist > maxDistance) return;

        // If line of sight to noise is clear, or within close acoustic radius
        if (HasLineOfSightTo(noisePos) || dist < 220.0f)
        {
            AlertToPosition(noisePos);
        }
    }

    public bool HasLineOfSightTo(Vector2 targetPos)
    {
        var spaceState = GetWorld2D()?.DirectSpaceState;
        if (spaceState == null) return false;

        _losExclude.Clear();
        _losExclude.Add(GetRid());
        if (TargetPlayer is CollisionObject2D colPlayer && GodotObject.IsInstanceValid(colPlayer))
        {
            _losExclude.Add(colPlayer.GetRid());
        }

        Vector2 from = GlobalPosition;

        for (int step = 0; step < 4; step++)
        {
            var query = PhysicsRayQueryParameters2D.Create(
                from,
                targetPos,
                collisionMask: 1,
                exclude: _losExclude
            );

            var hit = spaceState.IntersectRay(query);
            if (hit.Count == 0)
            {
                // Unobstructed line of sight!
                return true;
            }

            var collider = hit["collider"].AsGodotObject();
            if (collider is StaticBody2D)
            {
                // Blocked by a static wall, closed blast door, or prop
                return false;
            }
            else if (collider is CollisionObject2D colObj)
            {
                // Dynamic entity / other zombie; add to exclude and keep checking
                _losExclude.Add(colObj.GetRid());
            }
            else
            {
                return false;
            }
        }

        return false;
    }

    public bool CanSeePlayer()
    {
        if (TargetPlayer == null || !GodotObject.IsInstanceValid(TargetPlayer))
        {
            FindPlayer();
            if (TargetPlayer == null || !GodotObject.IsInstanceValid(TargetPlayer)) return false;
        }

        if (TargetPlayer is Player p)
        {
            if (p.IsDead || p.IsHiddenInLocker) return false;

            float dist = GlobalPosition.DistanceTo(p.GlobalPosition);
            float effectiveVisionRange = p.IsCrouching ? VisionRange * 0.6f : VisionRange;
            float effectiveHearRange = p.IsCrouching ? ProximityHearRange * 0.4f : ProximityHearRange;

            if (dist > effectiveVisionRange) return false;

            // Immediate close detection (breathing, footsteps in close melee range)
            if (dist <= effectiveHearRange)
            {
                return HasLineOfSightTo(p.GlobalPosition);
            }

            // Vision cone check (approx 150-degree forward field of view; narrower when crouching)
            Vector2 forward = new Vector2(Mathf.Cos(Rotation), Mathf.Sin(Rotation));
            Vector2 toPlayer = (p.GlobalPosition - GlobalPosition).Normalized();
            float dotThreshold = p.IsCrouching ? 0.35f : 0.15f;
            if (forward.Dot(toPlayer) < dotThreshold)
            {
                // Player is outside zombie's field of view
                return false;
            }

            return HasLineOfSightTo(p.GlobalPosition);
        }

        return false;
    }

    public Vector2 CalculateSteeringDirection(Vector2 targetPos, float delta)
    {
        Vector2 toTarget = targetPos - GlobalPosition;
        float dist = toTarget.Length();
        if (dist < 1.0f) return Vector2.Zero;

        Vector2 desiredDir = toTarget / dist;
        var spaceState = GetWorld2D()?.DirectSpaceState;
        if (spaceState == null) return desiredDir;

        // Step 1: Forward ray probe to check if the path is clear
        float forwardProbeDist = 65.0f;
        var forwardQuery = PhysicsRayQueryParameters2D.Create(
            GlobalPosition,
            GlobalPosition + desiredDir * forwardProbeDist,
            collisionMask: 1,
            exclude: _steeringExclude
        );

        var forwardHit = spaceState.IntersectRay(forwardQuery);
        bool forwardBlocked = false;
        if (forwardHit.Count > 0 && forwardHit["collider"].AsGodotObject() is StaticBody2D)
        {
            forwardBlocked = true;
        }

        // If unobstructed ahead and not currently colliding with a wall, move straight
        if (!forwardBlocked && GetSlideCollisionCount() == 0)
        {
            _smoothedMoveDir = _smoothedMoveDir.Lerp(desiredDir, delta * 14.0f).Normalized();
            return _smoothedMoveDir;
        }

        // Step 2: Context Steering (Evaluate 12 candidate directions)
        for (int i = 0; i < 12; i++)
        {
            Vector2 cand = CandidateDirs[i];
            _candidateInterests[i] = Mathf.Max(0.0f, cand.Dot(desiredDir));

            var query = PhysicsRayQueryParameters2D.Create(
                GlobalPosition,
                GlobalPosition + cand * 60.0f,
                collisionMask: 1,
                exclude: _steeringExclude
            );
            var hit = spaceState.IntersectRay(query);
            if (hit.Count > 0 && hit["collider"].AsGodotObject() is StaticBody2D)
            {
                Vector2 hitPos = hit["position"].AsVector2();
                float hitDist = GlobalPosition.DistanceTo(hitPos);
                _candidateDangers[i] = Mathf.Clamp(1.0f - (hitDist / 60.0f), 0.3f, 1.0f);
            }
            else
            {
                _candidateDangers[i] = 0.0f;
            }
        }

        // Bleed danger into adjacent directions to keep safety distance from walls
        for (int i = 0; i < 12; i++)
        {
            if (_candidateDangers[i] > 0.5f)
            {
                int prev = (i - 1 + 12) % 12;
                int next = (i + 1) % 12;
                _candidateDangers[prev] = Mathf.Max(_candidateDangers[prev], _candidateDangers[i] * 0.45f);
                _candidateDangers[next] = Mathf.Max(_candidateDangers[next], _candidateDangers[i] * 0.45f);
            }
        }

        // Find best direction with highest net score
        int bestIndex = 0;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 12; i++)
        {
            float score = _candidateInterests[i] - _candidateDangers[i] * 2.2f;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        Vector2 bestDir = CandidateDirs[bestIndex];

        // Step 3: Handle active wall collisions to slide along wall tangent
        int slideCount = GetSlideCollisionCount();
        for (int i = 0; i < slideCount; i++)
        {
            var col = GetSlideCollision(i);
            if (col.GetCollider() is StaticBody2D)
            {
                Vector2 normal = col.GetNormal();
                Vector2 tangent = new Vector2(-normal.Y, normal.X);
                if (tangent.Dot(desiredDir) < 0) tangent = -tangent;

                if (bestDir.Dot(-normal) > 0)
                {
                    bestDir = (bestDir + tangent * 2.5f).Normalized();
                }
            }
        }

        _smoothedMoveDir = _smoothedMoveDir.Lerp(bestDir, delta * 10.0f).Normalized();
        return _smoothedMoveDir;
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
