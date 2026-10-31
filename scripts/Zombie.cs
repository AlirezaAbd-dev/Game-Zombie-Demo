using System.Collections.Generic;
using Godot;

public partial class Zombie : CharacterBody2D
{
    public enum ZombieType
    {
        Walker,
        Runner,
        Spitter,
        Boomer,
        JuggernautBoss
    }

    private enum BossFsmState
    {
        Pursue,
        Telegraph,
        Charge,
        Stunned
    }

    [Export] public float MaxHealth { get; set; } = 50.0f;
    [Export] public float BaseSpeed { get; set; } = 135.0f;
    [Export] public float AttackDamage { get; set; } = 15.0f;
    [Export] public float AttackInterval { get; set; } = 0.8f;
    [Export] public float KnockbackResistance { get; set; } = 0.0f;

    public ZombieType Type { get; private set; } = ZombieType.Walker;
    public int ScoreCost { get; private set; } = 2;
    public float Health { get; private set; }
    public bool IsDead { get; private set; } = false;

    private float _speed;
    private Vector2 _knockbackVelocity = Vector2.Zero;
    private float _attackTimer = 0.0f;
    private float _walkPhase = 0.0f;
    private Vector2 _baseScale = new(3.0f, 3.0f);

    // Archetype behaviors
    private float _runnerLungeTimer = 0.0f;
    private float _runnerLungeDuration = 0.0f;

    private float _spitterShotTimer = 0.0f;

    private bool _isBoomerPrimed = false;
    private float _boomerFuseTimer = 0.0f;

    // Boss FSM
    private BossFsmState _bossState = BossFsmState.Pursue;
    private float _bossStateTimer = 0.0f;
    private float _bossShotTimer = 0.0f;
    private Vector2 _bossChargeDir = Vector2.Zero;
    private Line2D? _bossTelegraphLine;

    private Sprite2D _sprite = null!;
    private Area2D _attackArea = null!;
    private PointLight2D? _eyesLight;
    private Node2D? _player;
    private PackedScene? _pickupScene;

    public override void _Ready()
    {
        Health = MaxHealth;
        _speed = BaseSpeed * (float)GD.RandRange(0.85, 1.2);
        _walkPhase = (float)GD.RandRange(0.0, Mathf.Tau);
        AddToGroup("zombies");

        _sprite = GetNode<Sprite2D>("Sprite2D");
        _attackArea = GetNode<Area2D>("AttackArea");
        _eyesLight = GetNodeOrNull<PointLight2D>("EyeGlow");

        _pickupScene = GD.Load<PackedScene>("res://scenes/pickup.tscn");

        FindPlayer();
    }

    private void FindPlayer()
    {
        _player = (Node2D)GetTree().GetFirstNodeInGroup("player");
    }

    public void ConfigureArchetype(ZombieType type, int wave)
    {
        Type = type;
        float waveScale = 1.0f + (wave - 1) * 0.08f;

        switch (type)
        {
            case ZombieType.Walker:
                ScoreCost = 2;
                MaxHealth = 50f * waveScale;
                BaseSpeed = 135f;
                _baseScale = new Vector2(3.0f, 3.0f);
                Scale = Vector2.One;
                Modulate = Colors.White;
                break;

            case ZombieType.Runner:
                ScoreCost = 3;
                MaxHealth = 32f * waveScale;
                BaseSpeed = 215f;
                _baseScale = new Vector2(2.5f, 2.5f);
                Scale = new Vector2(0.82f, 0.82f);
                Modulate = new Color(0.65f, 0.95f, 1.0f);
                break;

            case ZombieType.Spitter:
                ScoreCost = 4;
                MaxHealth = 42f * waveScale;
                BaseSpeed = 115f;
                _baseScale = new Vector2(2.8f, 2.8f);
                Scale = new Vector2(0.95f, 0.95f);
                Modulate = new Color(0.55f, 1.0f, 0.45f);
                break;

            case ZombieType.Boomer:
                ScoreCost = 5;
                MaxHealth = 95f * waveScale;
                BaseSpeed = 92f;
                _baseScale = new Vector2(4.0f, 4.0f);
                Scale = new Vector2(1.35f, 1.35f);
                Modulate = new Color(1.0f, 0.65f, 0.2f);
                break;

            case ZombieType.JuggernautBoss:
                ScoreCost = 15;
                MaxHealth = 550f * waveScale;
                BaseSpeed = 105f;
                KnockbackResistance = 0.85f;
                _baseScale = new Vector2(6.0f, 6.0f);
                Scale = new Vector2(2.1f, 2.1f);
                Modulate = new Color(0.85f, 0.25f, 0.95f);

                _bossTelegraphLine = new Line2D
                {
                    Width = 28f,
                    DefaultColor = new Color(1f, 0.1f, 0.1f, 0.45f),
                    Visible = false,
                    TopLevel = true
                };
                AddChild(_bossTelegraphLine);
                break;
        }

        Health = MaxHealth;
        _speed = BaseSpeed * (float)GD.RandRange(0.9, 1.1);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead) return;

        if (!IsInstanceValid(_player))
        {
            FindPlayer();
            if (!IsInstanceValid(_player)) return;
        }

        float dt = (float)delta;
        Vector2 toPlayer = _player.GlobalPosition - GlobalPosition;
        float dist = toPlayer.Length();
        Vector2 dir = dist > 0.001f ? toPlayer / dist : Vector2.Zero;

        // Process Archetype AI
        switch (Type)
        {
            case ZombieType.Walker:
                ProcessWalkerPhysics(dt, dir, dist);
                break;

            case ZombieType.Runner:
                ProcessRunnerPhysics(dt, dir, dist);
                break;

            case ZombieType.Spitter:
                ProcessSpitterPhysics(dt, dir, dist);
                break;

            case ZombieType.Boomer:
                ProcessBoomerPhysics(dt, dir, dist);
                break;

            case ZombieType.JuggernautBoss:
                ProcessBossPhysics(dt, dir, dist);
                break;
        }

        HandleAttack(dt);
    }

    private void ProcessWalkerPhysics(float dt, Vector2 dir, float dist)
    {
        Rotation = Mathf.LerpAngle(Rotation, dir.Angle(), dt * 8.0f);
        AnimateWobble(dt, 9.0f, 0.1f);

        if (dist > 35.0f)
            Velocity = dir * _speed + _knockbackVelocity;
        else
            Velocity = _knockbackVelocity;

        _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, dt * 900.0f);
        MoveAndSlide();
    }

    private void ProcessRunnerPhysics(float dt, Vector2 dir, float dist)
    {
        Rotation = Mathf.LerpAngle(Rotation, dir.Angle(), dt * 12.0f);
        AnimateWobble(dt, 14.0f, 0.15f);

        _runnerLungeTimer += dt;
        if (_runnerLungeDuration > 0.0f)
        {
            _runnerLungeDuration -= dt;
            Velocity = dir * 460.0f + _knockbackVelocity;
        }
        else
        {
            if (_runnerLungeTimer >= 2.4f && dist >= 90.0f && dist <= 220.0f)
            {
                _runnerLungeTimer = 0.0f;
                _runnerLungeDuration = 0.22f;
                Velocity = dir * 460.0f;
            }
            else
            {
                Velocity = (dist > 35.0f ? dir * _speed : Vector2.Zero) + _knockbackVelocity;
            }
        }

        _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, dt * 900.0f);
        MoveAndSlide();
    }

    private void ProcessSpitterPhysics(float dt, Vector2 dir, float dist)
    {
        Rotation = Mathf.LerpAngle(Rotation, dir.Angle(), dt * 7.0f);
        AnimateWobble(dt, 8.0f, 0.08f);

        Vector2 moveTarget = Vector2.Zero;
        if (dist < 200.0f)
        {
            // Retreat
            moveTarget = -dir * _speed;
        }
        else if (dist > 360.0f)
        {
            // Advance
            moveTarget = dir * _speed;
        }
        else
        {
            // Strafe slightly
            Vector2 perp = new(-dir.Y, dir.X);
            moveTarget = perp * (_speed * 0.6f);
        }

        Velocity = moveTarget + _knockbackVelocity;
        _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, dt * 900.0f);
        MoveAndSlide();

        // Lead prediction shooting
        _spitterShotTimer += dt;
        if (_spitterShotTimer >= 2.8f)
        {
            _spitterShotTimer = 0.0f;
            FireAcidLead();
        }
    }

    private void FireAcidLead()
    {
        if (_player == null || !IsInstanceValid(_player)) return;

        Vector2 targetPos = _player.GlobalPosition;
        if (_player is CharacterBody2D charBody)
        {
            float bulletSpeed = 520.0f;
            float travelTime = GlobalPosition.DistanceTo(targetPos) / bulletSpeed;
            targetPos += charBody.Velocity * travelTime;
        }

        Vector2 fireDir = (targetPos - GlobalPosition).Normalized();
        var proj = new AcidProjectile
        {
            GlobalPosition = GlobalPosition + fireDir * 30.0f,
            Rotation = fireDir.Angle()
        };
        GetParent().AddChild(proj);
    }

    private void ProcessBoomerPhysics(float dt, Vector2 dir, float dist)
    {
        Rotation = Mathf.LerpAngle(Rotation, dir.Angle(), dt * 6.0f);
        AnimateWobble(dt, 7.0f, 0.12f);

        if (_isBoomerPrimed)
        {
            _boomerFuseTimer += dt;
            Velocity = _knockbackVelocity;
            _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, dt * 900.0f);
            MoveAndSlide();

            // Flash white/red
            float pulse = Mathf.Sin(_boomerFuseTimer * 30f);
            Modulate = pulse > 0f ? Colors.White : new Color(1.0f, 0.2f, 0.1f);

            if (_boomerFuseTimer >= 0.55f)
            {
                ExplodeBoomer();
            }
            return;
        }

        if (dist <= 52.0f)
        {
            PrimeBoomer();
            return;
        }

        Velocity = (dist > 30.0f ? dir * _speed : Vector2.Zero) + _knockbackVelocity;
        _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, dt * 900.0f);
        MoveAndSlide();
    }

    private void PrimeBoomer()
    {
        if (_isBoomerPrimed) return;
        _isBoomerPrimed = true;
        _boomerFuseTimer = 0.0f;
    }

    private void ExplodeBoomer()
    {
        if (IsDead) return;
        IsDead = true;

        float radius = 140.0f;
        AudioManager.Instance?.PlayAt("SfxExplosion", GlobalPosition, 1.15f);
        HitStopManager.Instance?.TriggerHitStop(0.05f, 0.04f);
        DecalManager.Instance?.SpawnScorchMark(GlobalPosition, 55f);

        // Gore particles
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
                Lifetime = 0.65f,
                Spread = 180f,
                Gravity = Vector2.Zero,
                InitialVelocityMin = 50f,
                InitialVelocityMax = 200f,
                ScaleAmountMin = 4f,
                ScaleAmountMax = 8f,
                Color = new Color(0.75f, 0.1f, 0.1f, 0.95f)
            };
            fx.Finished += fx.QueueFree;
            parent.AddChild(fx);
        }

        // Damage checks
        if (_player is Player p && IsInstanceValid(p))
        {
            if (GlobalPosition.DistanceTo(p.GlobalPosition) <= radius)
            {
                p.TakeDamage(40.0f, (p.GlobalPosition - GlobalPosition).Normalized());
                p.AddTrauma(0.5f);
            }
        }

        foreach (var node in GetTree().GetNodesInGroup("zombies"))
        {
            if (node != this && node is Zombie z && IsInstanceValid(z))
            {
                if (GlobalPosition.DistanceTo(z.GlobalPosition) <= radius)
                {
                    z.TakeDamage(85.0f, GlobalPosition, 1.5f);
                }
            }
        }

        foreach (var node in GetTree().GetNodesInGroup("explosive_barrels"))
        {
            if (node is Node2D b && IsInstanceValid(b))
            {
                if (GlobalPosition.DistanceTo(b.GlobalPosition) <= radius)
                {
                    b.Call("TakeDamage", 85.0f, GlobalPosition, 1.0f);
                }
            }
        }

        NotifyWorldDeath();
        QueueFree();
    }

    private void ProcessBossPhysics(float dt, Vector2 dir, float dist)
    {
        _bossStateTimer += dt;

        switch (_bossState)
        {
            case BossFsmState.Pursue:
                Rotation = Mathf.LerpAngle(Rotation, dir.Angle(), dt * 6.0f);
                AnimateWobble(dt, 6.0f, 0.06f);

                Velocity = (dist > 50.0f ? dir * _speed : Vector2.Zero) + _knockbackVelocity;
                _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, dt * 900.0f);
                MoveAndSlide();

                // 3-way acid fan every 1.2s
                _bossShotTimer += dt;
                if (_bossShotTimer >= 1.2f)
                {
                    _bossShotTimer = 0.0f;
                    FireBossFan(dir);
                }

                if (_bossStateTimer >= 3.5f)
                {
                    _bossState = BossFsmState.Telegraph;
                    _bossStateTimer = 0.0f;
                    _bossChargeDir = dir;
                    if (_bossTelegraphLine != null)
                    {
                        _bossTelegraphLine.Visible = true;
                        _bossTelegraphLine.Points = new[] { GlobalPosition, GlobalPosition + _bossChargeDir * 450.0f };
                    }
                }
                break;

            case BossFsmState.Telegraph:
                Velocity = Vector2.Zero;
                if (_bossTelegraphLine != null)
                {
                    _bossTelegraphLine.Points = new[] { GlobalPosition, GlobalPosition + _bossChargeDir * 450.0f };
                }

                if (_bossStateTimer >= 0.85f)
                {
                    if (_bossTelegraphLine != null)
                    {
                        _bossTelegraphLine.Visible = false;
                    }
                    _bossState = BossFsmState.Charge;
                    _bossStateTimer = 0.0f;
                }
                break;

            case BossFsmState.Charge:
                Velocity = _bossChargeDir * 640.0f;
                MoveAndSlide();

                if (GetSlideCollisionCount() > 0)
                {
                    // Collided with obstacle
                    for (int i = 0; i < GetSlideCollisionCount(); i++)
                    {
                        var col = GetSlideCollision(i);
                        if (col.GetCollider() is StaticBody2D)
                        {
                            _bossState = BossFsmState.Stunned;
                            _bossStateTimer = 0.0f;
                            Velocity = Vector2.Zero;
                            HitStopManager.Instance?.TriggerHitStop(0.04f, 0.05f);
                            AudioManager.Instance?.PlayAt("SfxExplosion", GlobalPosition, 1.4f);
                            break;
                        }
                    }
                }

                if (_bossState == BossFsmState.Charge && _bossStateTimer >= 0.65f)
                {
                    _bossState = BossFsmState.Pursue;
                    _bossStateTimer = 0.0f;
                }
                break;

            case BossFsmState.Stunned:
                Velocity = Vector2.Zero;
                Modulate = (Mathf.Sin(_bossStateTimer * 20f) > 0f) ? Colors.Yellow : new Color(0.85f, 0.25f, 0.95f);

                if (_bossStateTimer >= 1.5f)
                {
                    Modulate = new Color(0.85f, 0.25f, 0.95f);
                    _bossState = BossFsmState.Pursue;
                    _bossStateTimer = 0.0f;
                }
                break;
        }
    }

    private void FireBossFan(Vector2 dir)
    {
        float baseAngle = dir.Angle();
        float[] offsets = { -0.25f, 0.0f, 0.25f };

        foreach (float off in offsets)
        {
            Vector2 shotDir = Vector2.Right.Rotated(baseAngle + off);
            var proj = new AcidProjectile
            {
                GlobalPosition = GlobalPosition + shotDir * 50.0f,
                Rotation = shotDir.Angle()
            };
            GetParent().AddChild(proj);
        }
    }

    private void AnimateWobble(float dt, float speed, float intensity)
    {
        _walkPhase += dt * speed;
        float wobble = Mathf.Sin(_walkPhase) * intensity;
        _sprite.Rotation = wobble;
        _sprite.Scale = _baseScale + new Vector2(wobble * 0.4f, -wobble * 0.4f);
    }

    private void HandleAttack(float delta)
    {
        _attackTimer += delta;
        if (_attackTimer >= AttackInterval)
        {
            foreach (Node2D body in _attackArea.GetOverlappingBodies())
            {
                if (body.IsInGroup("player") && body.HasMethod("TakeDamage"))
                {
                    _attackTimer = 0.0f;
                    Vector2 attackDir = (body.GlobalPosition - GlobalPosition).Normalized();
                    body.Call("TakeDamage", AttackDamage, attackDir);

                    var tween = CreateTween();
                    tween.TweenProperty(_sprite, "position", new Vector2(12, 0), 0.08);
                    tween.TweenProperty(_sprite, "position", Vector2.Zero, 0.15);
                    break;
                }
            }
        }
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, GlobalPosition, 1.0f);
    }

    public void TakeDamage(float amount, Vector2 hitOrigin, float knockbackMultiplier = 1.0f)
    {
        if (IsDead) return;

        float effectiveAmount = (_bossState == BossFsmState.Stunned) ? amount * 1.5f : amount;
        Health -= effectiveAmount;

        Vector2 kbDir = (GlobalPosition - hitOrigin).Normalized();
        if (kbDir == Vector2.Zero) kbDir = Vector2.Right;

        float kbStrength = 320.0f * knockbackMultiplier * (1.0f - KnockbackResistance);
        _knockbackVelocity = kbDir * kbStrength;

        AudioManager.Instance?.PlayAt("SfxZombieHit", GlobalPosition);
        DecalManager.Instance?.SpawnBloodSplatter(GlobalPosition, kbDir);

        var tween = CreateTween();
        _sprite.Modulate = new Color(3.5f, 0.4f, 0.4f, 1.0f);
        tween.TweenProperty(_sprite, "modulate", Colors.White, 0.1);

        if (Health <= 0.0f)
        {
            if (Type == ZombieType.Boomer)
            {
                PrimeBoomer();
            }
            else
            {
                Die();
            }
        }
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        if (_bossTelegraphLine != null && IsInstanceValid(_bossTelegraphLine))
        {
            _bossTelegraphLine.QueueFree();
        }

        var parent = GetParent();
        if (parent != null)
        {
            var deathFx = new CpuParticles2D
            {
                GlobalPosition = GlobalPosition,
                Emitting = true,
                OneShot = true,
                Explosiveness = 1.0f,
                Amount = Type == ZombieType.JuggernautBoss ? 64 : 32,
                Lifetime = 0.6f,
                Spread = 180.0f,
                InitialVelocityMin = 40.0f,
                InitialVelocityMax = 180.0f,
                Gravity = Vector2.Zero,
                ScaleAmountMin = 3.0f,
                ScaleAmountMax = 8.0f,
                Color = new Color(0.7f, 0.08f, 0.08f, 0.9f)
            };
            deathFx.Finished += deathFx.QueueFree;
            parent.AddChild(deathFx);

            // Loot drops
            if (_pickupScene != null)
            {
                float roll = (float)GD.RandRange(0.0, 1.0);
                if (Type == ZombieType.JuggernautBoss || roll < 0.24f)
                {
                    var pickup = _pickupScene.Instantiate<Pickup>();
                    pickup.GlobalPosition = GlobalPosition;
                    pickup.Setup(Pickup.PickupType.Ammo, 30.0f);
                    parent.CallDeferred("add_child", pickup);
                }
                if (Type == ZombieType.JuggernautBoss || roll < 0.35f)
                {
                    var pickup = _pickupScene.Instantiate<Pickup>();
                    pickup.GlobalPosition = GlobalPosition + new Vector2(15, 0);
                    pickup.Setup(Pickup.PickupType.Health, 35.0f);
                    parent.CallDeferred("add_child", pickup);
                }
            }
        }

        NotifyWorldDeath();
        QueueFree();
    }

    private void NotifyWorldDeath()
    {
        if (_player is Player p && IsInstanceValid(p))
        {
            p.OnZombieKilled();
        }

        var world = GetTree().CurrentScene;
        if (world != null && world.HasMethod("OnZombieKilled"))
        {
            world.Call("OnZombieKilled");
        }
    }
}
