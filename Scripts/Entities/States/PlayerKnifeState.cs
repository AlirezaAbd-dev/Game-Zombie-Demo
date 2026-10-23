using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities.States;

/// <summary>
/// Quick Melee Knife Attack - distinct from the stunned enemy finisher.
/// Fast, tactical close-quarters defensive/offensive blade slash.
/// Deals close-range damage, knocks back enemies, and can stagger them into finisher states.
/// </summary>
public class PlayerKnifeState : PlayerState
{
    private float _knifeTimer = 0.22f;
    private bool _hasDealtDamage = false;
    private Vector2 _lungeDir;

    public const float KnifeRange = 115.0f;
    public const float KnifeDamage = 35.0f;
    public const float KnifeKnockback = 340.0f;
    public const float KnifeConeAngle = 2.1f; // ~120 degrees forward arc

    public PlayerKnifeState(Player player) : base(player) { }

    public override void Enter()
    {
        _knifeTimer = 0.22f;
        _hasDealtDamage = false;

        // Face mouse or aim direction
        Player.HandleAiming();
        _lungeDir = Transform2D.Identity.X.Rotated(Player.GlobalRotation);

        // Hide firearm during knife slash
        if (Player.GunSprite != null)
        {
            Player.GunSprite.Visible = false;
        }

        // Animate body forward lunge swipe
        if (Player.BodySprite != null)
        {
            var swipeTween = Player.CreateTween();
            swipeTween.TweenProperty(Player.BodySprite, "position", new Vector2(14.0f, 0.0f), 0.07);
            swipeTween.TweenProperty(Player.BodySprite, "position", Vector2.Zero, 0.15);
        }

        // Spawn visual knife slash blade arc
        SpawnSlashVfx();

        // Play knife slash audio
        AudioManager.Instance?.PlaySound(SoundType.FinisherSlash, Player.GlobalPosition, 1.2f);

        // Tactile screen trauma
        Player.AddTrauma(0.18f);

        // Execute melee cut immediately at start of swing
        ExecuteMeleeCut();
    }

    private void ExecuteMeleeCut()
    {
        if (_hasDealtDamage) return;
        _hasDealtDamage = true;

        var zombies = Player.GetTree()?.GetNodesInGroup("zombies");
        if (zombies == null) return;

        Vector2 playerPos = Player.GlobalPosition;
        Vector2 forward = Transform2D.Identity.X.Rotated(Player.GlobalRotation);
        bool hitAny = false;

        foreach (var node in zombies)
        {
            if (node is Zombie z && GodotObject.IsInstanceValid(z) && !z.IsDead)
            {
                Vector2 toZombie = z.GlobalPosition - playerPos;
                float dist = toZombie.Length();

                if (dist <= KnifeRange)
                {
                    // Check angle in front of player
                    float angle = forward.AngleTo(toZombie);
                    if (Mathf.Abs(angle) <= KnifeConeAngle * 0.5f)
                    {
                        hitAny = true;
                        Vector2 hitDir = toZombie.Normalized();
                        if (hitDir == Vector2.Zero) hitDir = forward;

                        // Deal knife damage & knockback (TakeDamage also triggers FinisherTarget stun accumulation)
                        z.TakeDamage(KnifeDamage, hitDir);
                        z.MovementComponent?.ApplyKnockback(hitDir, KnifeKnockback);

                        // Blood splash impact
                        SpawnBloodSplash(z.GlobalPosition);
                    }
                }
            }
        }

        if (hitAny)
        {
            // Minor hit-stop for visceral tactile impact feel (40ms)
            Engine.TimeScale = 0.25f;
            var timer = Player.GetTree().CreateTimer(0.04f, true, false, true);
            timer.Timeout += () => Engine.TimeScale = 1.0f;
        }
    }

    private void SpawnSlashVfx()
    {
        var parent = Player.GetParent();
        if (parent == null) return;

        var slashTex = GD.Load<Texture2D>("res://Assets/textures/fx_knife_slash.png");
        if (slashTex == null) return;

        Vector2 forward = Transform2D.Identity.X.Rotated(Player.GlobalRotation);
        Vector2 slashPos = Player.GlobalPosition + forward * 42.0f;

        var sprite = new Sprite2D
        {
            Texture = slashTex,
            GlobalPosition = slashPos,
            Rotation = Player.GlobalRotation,
            Scale = new Vector2(1.6f, 1.6f),
            ZIndex = 11,
            Modulate = new Color(1.2f, 1.2f, 1.4f, 0.95f)
        };
        parent.AddChild(sprite);

        var tween = sprite.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(sprite, "scale", new Vector2(2.1f, 2.1f), 0.18);
        tween.TweenProperty(sprite, "modulate:a", 0.0f, 0.18);
        tween.Chain().TweenCallback(Callable.From(sprite.QueueFree));
    }

    private void SpawnBloodSplash(Vector2 pos)
    {
        var parent = Player.GetParent();
        if (parent == null) return;

        var blood = new CpuParticles2D
        {
            GlobalPosition = pos,
            Emitting = true,
            OneShot = true,
            Explosiveness = 0.95f,
            Amount = 14,
            Lifetime = 0.35f,
            Spread = 60.0f,
            InitialVelocityMin = 140.0f,
            InitialVelocityMax = 280.0f,
            ScaleAmountMin = 2.5f,
            ScaleAmountMax = 5.0f,
            Color = new Color(0.75f, 0.05f, 0.05f, 0.9f)
        };
        parent.AddChild(blood);

        var timer = Player.GetTree().CreateTimer(0.45f);
        timer.Timeout += () => blood.QueueFree();
    }

    public override void Exit()
    {
        Engine.TimeScale = 1.0f;
        if (Player.GunSprite != null)
        {
            Player.GunSprite.Visible = true;
        }
        if (Player.BodySprite != null)
        {
            Player.BodySprite.Position = Vector2.Zero;
        }
        Player.GlobalPosition = WorldManager.ClampToPlayableBounds(Player.GlobalPosition);
    }

    public override void PhysicsUpdate(float delta)
    {
        _knifeTimer -= delta;

        // Allow immediate animation cancel into finisher if an enemy is stunned and player presses finisher/knife
        if (Input.IsActionJustPressed("finisher") || Input.IsActionJustPressed("interact") || Input.IsActionJustPressed("knife"))
        {
            if (Player.TryExecuteFinisher())
            {
                return;
            }
        }

        // Controlled responsive movement during knife attack
        Vector2 moveInput = Player.GetMovementInput();
        if (moveInput != Vector2.Zero)
        {
            Player.MovementComponent?.Move(moveInput, 0.45f);
        }
        else
        {
            Player.MovementComponent?.Move(_lungeDir, 0.30f);
        }

        Player.GlobalPosition = WorldManager.ClampToPlayableBounds(Player.GlobalPosition);

        if (_knifeTimer <= 0.0f)
        {
            Vector2 input = Player.GetMovementInput();
            Player.StateMachine.ChangeState(input != Vector2.Zero ? Player.WalkState : Player.IdleState);
        }
    }
}
