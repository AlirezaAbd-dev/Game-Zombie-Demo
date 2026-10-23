using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities.Components;

public enum FinisherType
{
    RoundhouseKick,
    CombatKnife
}

[GlobalClass]
public partial class FinisherTargetComponent : Node2D
{
    [Signal]
    public delegate void StunStateChangedEventHandler(bool isStunned);

    [Signal]
    public delegate void FinisherExecutedEventHandler(int finisherTypeVal);

    [Export] public float StunDuration { get; set; } = 3.5f;
    [Export] public float StunCooldownDuration { get; set; } = 4.0f;
    [Export] public float HeavyDamageThreshold { get; set; } = 45.0f;
    [Export] public float LowHealthThresholdRatio { get; set; } = 0.55f;
    [Export] public float FinisherRange { get; set; } = 140.0f;

    public bool IsStunned { get; private set; } = false;
    public float StunTimer { get; private set; } = 0.0f;
    public float CooldownTimer { get; private set; } = 0.0f;

    private Zombie _zombie;
    private Player _player;
    private Sprite2D _stunStarsSprite;
    private Node2D _promptContainer;
    private Sprite2D _promptBadge;
    private Label _promptLabel;
    private float _recentDamageAccumulator = 0.0f;
    private float _damageWindowTimer = 0.0f;
    private float _visualAnimPhase = 0.0f;
    private bool _hasPlayedPromptChime = false;
    private bool _lowHealthStunTriggered = false;

    public override void _Ready()
    {
        _zombie = GetParent<Zombie>();
        CreateVisualPrompts();
    }

    private void CreateVisualPrompts()
    {
        // 1. Stun Stars Sprite
        var starsTex = GD.Load<Texture2D>("res://Assets/textures/stun_stars.png");
        _stunStarsSprite = new Sprite2D
        {
            Name = "StunStars",
            Texture = starsTex,
            Position = new Vector2(0, -42),
            Visible = false,
            ZIndex = 5
        };
        AddChild(_stunStarsSprite);

        // 2. Finisher Action Prompt Badge
        _promptContainer = new Node2D
        {
            Name = "PromptContainer",
            Position = new Vector2(0, -68),
            Visible = false,
            ZIndex = 10
        };
        AddChild(_promptContainer);

        var promptTex = GD.Load<Texture2D>("res://Assets/textures/finisher_prompt.png");
        _promptBadge = new Sprite2D
        {
            Name = "PromptBadge",
            Texture = promptTex,
            Scale = new Vector2(1.2f, 1.2f)
        };
        _promptContainer.AddChild(_promptBadge);

        _promptLabel = new Label
        {
            Name = "PromptLabel",
            Text = "[F] FINISH",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Position = new Vector2(-60, -11),
            Size = new Vector2(120, 22)
        };
        _promptLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.95f, 0.2f, 1.0f));
        _promptLabel.AddThemeFontSizeOverride("font_size", 11);
        _promptContainer.AddChild(_promptLabel);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (_damageWindowTimer > 0.0f)
        {
            _damageWindowTimer -= dt;
            if (_damageWindowTimer <= 0.0f)
            {
                _recentDamageAccumulator = 0.0f;
            }
        }

        if (CooldownTimer > 0.0f)
        {
            CooldownTimer -= dt;
        }

        if (IsStunned)
        {
            StunTimer -= dt;
            _visualAnimPhase += dt * 7.0f;

            // Animate orbiting stun stars
            if (_stunStarsSprite != null)
            {
                _stunStarsSprite.Visible = true;
                _stunStarsSprite.Rotation += dt * 3.5f;
                float bob = Mathf.Sin(_visualAnimPhase) * 4.0f;
                _stunStarsSprite.Position = new Vector2(0, -44 + bob);
            }

            // Check distance to player for Finisher Prompt
            UpdateFinisherPrompt();

            if (StunTimer <= 0.0f)
            {
                RecoverFromStun();
            }
        }
        else
        {
            if (_stunStarsSprite != null) _stunStarsSprite.Visible = false;
            if (_promptContainer != null) _promptContainer.Visible = false;
            _hasPlayedPromptChime = false;
        }
    }

    public bool HasLineOfSightToPlayer(Player player)
    {
        if (player == null || !IsInstanceValid(player)) return false;

        var spaceState = GetWorld2D()?.DirectSpaceState;
        if (spaceState == null) return true;

        var exclude = new Godot.Collections.Array<Rid> { player.GetRid() };
        if (_zombie != null && IsInstanceValid(_zombie))
        {
            exclude.Add(_zombie.GetRid());
        }

        // Exclude all other zombies so crowds do not block finisher line-of-sight
        var zombies = GetTree()?.GetNodesInGroup("zombies");
        if (zombies != null)
        {
            foreach (var node in zombies)
            {
                if (node is CollisionObject2D col && IsInstanceValid(col))
                {
                    exclude.Add(col.GetRid());
                }
            }
        }

        var query = PhysicsRayQueryParameters2D.Create(
            GlobalPosition,
            player.GlobalPosition,
            collisionMask: 1, // Layer 1: Obstacles, Walls, Boundaries
            exclude: exclude
        );

        var result = spaceState.IntersectRay(query);
        if (result != null && result.Count > 0)
        {
            var collider = result["collider"].AsGodotObject();
            if (collider == player || collider == _zombie || collider is Zombie)
            {
                return true;
            }
            return false;
        }

        // Clear direct line of sight without any walls or boundaries
        return true;
    }

    private void UpdateFinisherPrompt()
    {
        if (!IsInstanceValid(_player))
        {
            _player = (Player)GetTree()?.GetFirstNodeInGroup("player");
        }

        if (_player == null || _player.IsDead || _zombie == null || _zombie.IsDead)
        {
            if (_promptContainer != null) _promptContainer.Visible = false;
            return;
        }

        float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);
        bool inRange = dist <= FinisherRange && HasLineOfSightToPlayer(_player);

        if (_promptContainer != null)
        {
            _promptContainer.Visible = inRange;
            if (inRange)
            {
                // Pulsate prompt scale for high-urgency action feel
                float pulse = 1.15f + Mathf.Sin(_visualAnimPhase * 1.5f) * 0.12f;
                _promptContainer.Scale = new Vector2(pulse, pulse);

                // Alternating prompt text between kick and knife for dynamic feel
                if (_promptLabel != null)
                {
                    _promptLabel.Text = _zombie.Stats?.TypeName == "Specimen Zero"
                        ? "[F] POWER STRIKE"
                        : "[F] KICK / FINISH";
                }

                if (!_hasPlayedPromptChime)
                {
                    _hasPlayedPromptChime = true;
                    AudioManager.Instance?.PlaySound(SoundType.FinisherPrompt, GlobalPosition, 1.05f);
                }
            }
            else
            {
                _hasPlayedPromptChime = false;
            }
        }
    }

    public void OnDamageTaken(float damageAmount)
    {
        if (IsStunned || CooldownTimer > 0.0f || _zombie == null || _zombie.IsDead) return;

        _recentDamageAccumulator += damageAmount;
        _damageWindowTimer = 0.85f;

        // Condition 1: Heavy burst damage in short window
        bool isHeavyDamage = _recentDamageAccumulator >= HeavyDamageThreshold;

        // Condition 2: Low health stumble threshold
        bool isLowHealth = false;
        if (!_lowHealthStunTriggered && _zombie.HealthComponent != null)
        {
            float hpRatio = _zombie.HealthComponent.CurrentHealth / _zombie.HealthComponent.MaxHealth;
            if (hpRatio <= LowHealthThresholdRatio && hpRatio > 0.0f)
            {
                isLowHealth = true;
                _lowHealthStunTriggered = true;
            }
        }

        if (isHeavyDamage || isLowHealth)
        {
            TriggerStun(StunDuration);
        }
    }

    public void TriggerStun(float duration)
    {
        if (_zombie == null || _zombie.IsDead) return;

        IsStunned = true;
        StunTimer = duration;
        _recentDamageAccumulator = 0.0f;
        _visualAnimPhase = 0.0f;
        _hasPlayedPromptChime = false;

        AudioManager.Instance?.PlaySound(SoundType.EnemyStunned, GlobalPosition);
        EmitSignal(SignalName.StunStateChanged, true);
        _zombie.OnStunned();
    }

    public void RecoverFromStun()
    {
        if (!IsStunned) return;

        IsStunned = false;
        StunTimer = 0.0f;
        CooldownTimer = StunCooldownDuration;

        if (_stunStarsSprite != null) _stunStarsSprite.Visible = false;
        if (_promptContainer != null) _promptContainer.Visible = false;

        EmitSignal(SignalName.StunStateChanged, false);
        _zombie?.OnStunRecovered();
    }

    public bool CanBeFinishedBy(Player player)
    {
        if (!IsStunned || _zombie == null || _zombie.IsDead || player == null || player.IsDead)
            return false;

        if (GlobalPosition.DistanceTo(player.GlobalPosition) > FinisherRange)
            return false;

        return HasLineOfSightToPlayer(player);
    }
}
