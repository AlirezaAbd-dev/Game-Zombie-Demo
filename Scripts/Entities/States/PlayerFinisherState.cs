using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities.States;

public class PlayerFinisherState : PlayerState
{
    private readonly Zombie _target;
    private readonly FinisherType _finisherType;
    private float _durationTimer = 0.38f;
    private bool _hasExecutedImpact = false;
    private Vector2 _lungeTarget;
    private float _lungeRemaining;

    public PlayerFinisherState(Player player, Zombie target, FinisherType finisherType) : base(player)
    {
        _target = target;
        _finisherType = finisherType;
    }

    public override void Enter()
    {
        Player.IsInvulnerable = true;
        _durationTimer = 0.38f;
        _hasExecutedImpact = false;

        if (!GodotObject.IsInstanceValid(_target) || _target.IsDead)
        {
            EndFinisher();
            return;
        }

        Vector2 startPos = Player.GlobalPosition;
        Vector2 toTarget = (_target.GlobalPosition - startPos).Normalized();
        if (toTarget == Vector2.Zero) toTarget = Transform2D.Identity.X.Rotated(Player.GlobalRotation);
        Player.Rotation = toTarget.Angle();

        // Hide or tuck gun sprite during melee finisher
        if (Player.GunSprite != null)
        {
            Player.GunSprite.Visible = false;
        }

        // Calculate safe striking position with obstacle raycast detection
        Vector2 targetStrikePos = _target.GlobalPosition - toTarget * 38.0f;
        Vector2 safeStrikePos = targetStrikePos;

        var spaceState = Player.GetWorld2D()?.DirectSpaceState;
        if (spaceState != null)
        {
            var exclude = new Godot.Collections.Array<Rid> { Player.GetRid() };
            if (_target != null && GodotObject.IsInstanceValid(_target))
            {
                exclude.Add(_target.GetRid());
            }

            var rayQuery = PhysicsRayQueryParameters2D.Create(
                startPos,
                targetStrikePos,
                collisionMask: 1, // Layer 1: Obstacles, Walls, Boundaries
                exclude: exclude
            );
            var hit = spaceState.IntersectRay(rayQuery);
            if (hit != null && hit.Count > 0)
            {
                var collider = hit["collider"].AsGodotObject();
                if (collider != Player && collider != _target && collider is not Zombie)
                {
                    Vector2 hitPoint = hit["position"].AsVector2();
                    Vector2 hitNormal = hit["normal"].AsVector2();
                    safeStrikePos = hitPoint + hitNormal * 28.0f;
                }
            }
        }

        // Clamp safe striking position to facility boundaries
        safeStrikePos = WorldManager.ClampToPlayableBounds(safeStrikePos);

        // Move the complete physics hull, rather than tweening through collision shapes.
        _lungeTarget = safeStrikePos;
        _lungeRemaining = 0.12f;
    }

    private void ExecuteImpactStrike()
    {
        if (_hasExecutedImpact) return;
        _hasExecutedImpact = true;

        if (!GodotObject.IsInstanceValid(_target) || _target.IsDead)
        {
            EndFinisher();
            return;
        }

        Vector2 impactPos = _target.GlobalPosition;
        Vector2 strikeDir = (impactPos - Player.GlobalPosition).Normalized();
        if (strikeDir == Vector2.Zero) strikeDir = Transform2D.Identity.X.Rotated(Player.GlobalRotation);

        // RE4 Cinematic Hit-Stop (brief slow motion freeze frame)
        ApplyHitStop(0.12f, 0.08f);

        if (_finisherType == FinisherType.RoundhouseKick)
        {
            ExecuteRoundhouseKick(strikeDir, impactPos);
        }
        else
        {
            ExecuteCombatKnife(strikeDir, impactPos);
        }
    }

    private void ExecuteRoundhouseKick(Vector2 strikeDir, Vector2 impactPos)
    {
        // 360-degree roundhouse kick spin
        if (Player.BodySprite != null)
        {
            var spinTween = Player.CreateTween();
            spinTween.TweenProperty(Player.BodySprite, "rotation", Player.BodySprite.Rotation + Mathf.Tau, 0.22);
        }

        // Visual FX: Shockwave
        SpawnEffect("res://Assets/textures/fx_kick_impact.png", impactPos, strikeDir.Angle(), new Vector2(2.2f, 2.2f));

        // Sound & Screen Shake
        AudioManager.Instance?.PlaySound(SoundType.FinisherKick, impactPos, 1.0f);
        Player.AddTrauma(0.65f);

        // Primary Target Damage & Violent Ragdoll Knockback
        if (GodotObject.IsInstanceValid(_target) && !_target.IsDead)
        {
            if (_target.Stats?.TypeName == "Specimen Zero")
            {
                _target.TakeDamage(175.0f, strikeDir);
                _target.MovementComponent?.ApplyKnockback(strikeDir, 650.0f);
            }
            else
            {
                _target.PerformDeath();
            }
        }

        // AoE Shockwave: Blast nearby surrounding enemies back
        var allZombies = Player.GetTree()?.GetNodesInGroup("zombies");
        if (allZombies != null)
        {
            foreach (var node in allZombies)
            {
                if (node is Zombie z && GodotObject.IsInstanceValid(z) && !z.IsDead && z != _target)
                {
                    float dist = impactPos.DistanceTo(z.GlobalPosition);
                    if (dist <= 150.0f)
                    {
                        Vector2 aoeDir = (z.GlobalPosition - impactPos).Normalized();
                        z.TakeDamage(40.0f, aoeDir);
                        z.MovementComponent?.ApplyKnockback(aoeDir, 520.0f);
                    }
                }
            }
        }
    }

    private void ExecuteCombatKnife(Vector2 strikeDir, Vector2 impactPos)
    {
        // Visceral Knife Slash Visual FX
        SpawnEffect("res://Assets/textures/fx_knife_slash.png", impactPos, strikeDir.Angle(), new Vector2(2.4f, 2.4f));

        // Sound & Camera Impact
        AudioManager.Instance?.PlaySound(SoundType.FinisherSlash, impactPos, 1.0f);
        Player.AddTrauma(0.5f);

        // Lethal Execution Damage
        if (GodotObject.IsInstanceValid(_target) && !_target.IsDead)
        {
            if (_target.Stats?.TypeName == "Specimen Zero")
            {
                _target.TakeDamage(200.0f, strikeDir);
                _target.MovementComponent?.ApplyKnockback(strikeDir, 450.0f);
            }
            else
            {
                _target.PerformDeath();
            }
        }
    }

    private void SpawnEffect(string texturePath, Vector2 pos, float rot, Vector2 scale)
    {
        var parent = Player.GetParent();
        if (parent == null) return;

        var tex = GD.Load<Texture2D>(texturePath);
        if (tex == null) return;

        var sprite = new Sprite2D
        {
            Texture = tex,
            GlobalPosition = pos,
            Rotation = rot,
            Scale = scale,
            ZIndex = 12
        };
        parent.AddChild(sprite);

        var tween = sprite.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(sprite, "scale", scale * 1.35f, 0.22);
        tween.TweenProperty(sprite, "modulate:a", 0.0f, 0.22);
        tween.Chain().TweenCallback(Callable.From(sprite.QueueFree));
    }

    private void ApplyHitStop(float timeScale, float realSeconds)
    {
        Engine.TimeScale = timeScale;
        var timer = Player.GetTree().CreateTimer(realSeconds, true, false, true);
        timer.Timeout += () => Engine.TimeScale = 1.0f;
    }

    public override void Exit()
    {
        Engine.TimeScale = 1.0f;
        Player.IsInvulnerable = false;

        if (Player.GunSprite != null)
        {
            Player.GunSprite.Visible = true;
        }

        if (Player.BodySprite != null)
        {
            Player.BodySprite.Rotation = 0.0f;
        }

        Player.GlobalPosition = WorldManager.ClampToPlayableBounds(Player.GlobalPosition);
    }

    public override void PhysicsUpdate(float delta)
    {
        if (_lungeRemaining > 0.0f)
        {
            Vector2 motion = (_lungeTarget - Player.GlobalPosition) * Mathf.Min(1.0f, delta / _lungeRemaining);
            var collision = Player.MoveAndCollide(motion);
            _lungeRemaining -= delta;
            if (collision != null || _lungeRemaining <= 0.0f)
            {
                _lungeRemaining = 0.0f;
                if (GodotObject.IsInstanceValid(_target) && !_target.IsDead
                    && _target.FinisherTarget?.HasLineOfSightToPlayer(Player) == true)
                    ExecuteImpactStrike();
                else
                {
                    EndFinisher();
                    return;
                }
            }
        }
        else Player.MovementComponent?.Stop();
        Player.GlobalPosition = WorldManager.ClampToPlayableBounds(Player.GlobalPosition);

        _durationTimer -= delta;
        if (_durationTimer <= 0.0f)
        {
            EndFinisher();
        }
    }

    private void EndFinisher()
    {
        Vector2 input = Player.GetMovementInput();
        Player.StateMachine.ChangeState(input != Vector2.Zero ? Player.WalkState : Player.IdleState);
    }
}
