using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Entities.States;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities;

public partial class Player : CharacterBody2D, IDamageable
{
    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Export] public float FireRate { get; set; } = 0.13f;

    [ExportGroup("Components")]
    [Export] public HealthComponent HealthComponent { get; set; }
    [Export] public MovementComponent MovementComponent { get; set; }
    [Export] public StaminaComponent StaminaComponent { get; set; }
    [Export] public InventoryComponent InventoryComponent { get; set; }
    [Export] public FlashlightComponent FlashlightComponent { get; set; }

    [ExportGroup("Visual Nodes")]
    [Export] public Sprite2D BodySprite { get; set; }
    [Export] public Sprite2D GunSprite { get; set; }
    [Export] public Camera2D Camera { get; set; }
    [Export] public RayCast2D LaserRay { get; set; }
    [Export] public Line2D LaserLine { get; set; }
    [Export] public Sprite2D LaserDot { get; set; }
    [Export] public PointLight2D MuzzleFlash { get; set; }
    [Export] public CpuParticles2D MuzzleParticles { get; set; }
    [Export] public CpuParticles2D DustParticles { get; set; }

    public StateMachine StateMachine { get; private set; }
    public PlayerIdleState IdleState { get; private set; }
    public PlayerWalkState WalkState { get; private set; }
    public PlayerDashState DashState { get; private set; }
    public PlayerDeadState DeadState { get; private set; }

    public bool IsInvulnerable { get; set; } = false;
    public bool IsDead => HealthComponent?.IsDead ?? false;

    private float _shootTimer = 0.0f;
    private float _cameraTrauma = 0.0f;
    private float _walkPhase = 0.0f;
    private readonly Vector2 _baseScale = new(3.0f, 3.0f);
    private readonly Vector2 _gunRestPos = new(12.333334f, -0.33333334f);
    private readonly Vector2[] _laserPoints = new Vector2[2];

    public override void _Ready()
    {
        AddToGroup("player");

        ValidateAndBindNodes();

        StateMachine = new StateMachine();
        AddChild(StateMachine);

        IdleState = new PlayerIdleState(this);
        WalkState = new PlayerWalkState(this);
        DashState = new PlayerDashState(this);
        DeadState = new PlayerDeadState(this);

        HealthComponent.Died += OnDied;
        StateMachine.ChangeState(IdleState);
    }

    private void ValidateAndBindNodes()
    {
        HealthComponent ??= GetNodeOrNull<HealthComponent>("%HealthComponent");
        MovementComponent ??= GetNodeOrNull<MovementComponent>("%MovementComponent");
        StaminaComponent ??= GetNodeOrNull<StaminaComponent>("%StaminaComponent");
        InventoryComponent ??= GetNodeOrNull<InventoryComponent>("%InventoryComponent");
        FlashlightComponent ??= GetNodeOrNull<FlashlightComponent>("%FlashlightComponent");

        BodySprite ??= GetNodeOrNull<Sprite2D>("%Body");
        GunSprite ??= GetNodeOrNull<Sprite2D>("%Gun");
        Camera ??= GetNodeOrNull<Camera2D>("%Camera2D");
        LaserRay ??= GetNodeOrNull<RayCast2D>("%LaserRayCast");
        LaserLine ??= GetNodeOrNull<Line2D>("%LaserLine");
        LaserDot ??= GetNodeOrNull<Sprite2D>("%LaserDot");
        MuzzleFlash ??= GetNodeOrNull<PointLight2D>("%MuzzleFlash");
        MuzzleParticles ??= GetNodeOrNull<CpuParticles2D>("%MuzzleParticles");
        DustParticles ??= GetNodeOrNull<CpuParticles2D>("%FootstepDust");

        if (HealthComponent == null) GD.PrintErr($"[Player] Missing HealthComponent on {GetPath()}");
        if (MovementComponent == null) GD.PrintErr($"[Player] Missing MovementComponent on {GetPath()}");
        if (StaminaComponent == null) GD.PrintErr($"[Player] Missing StaminaComponent on {GetPath()}");
        if (InventoryComponent == null) GD.PrintErr($"[Player] Missing InventoryComponent on {GetPath()}");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        StateMachine?.Update(dt);

        UpdateCamera(dt);
        FlashlightComponent?.UpdateFlicker();
        UpdateLaserSight();

        if (GunSprite != null)
        {
            GunSprite.Position = GunSprite.Position.Lerp(_gunRestPos, dt * 15.0f);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        MovementComponent?.DecayKnockback(dt);
        StateMachine?.PhysicsUpdate(dt);
    }

    public Vector2 GetMovementInput()
    {
        return new Vector2(
            Input.GetAxis("move_left", "move_right"),
            Input.GetAxis("move_up", "move_down")
        );
    }

    public void HandleAiming()
    {
        LookAt(GetGlobalMousePosition());
    }

    public void HandleCombat(float delta)
    {
        _shootTimer -= delta;
        InventoryComponent?.UpdateReload(delta);
        StaminaComponent?.Regenerate(delta);

        if (Input.IsActionJustPressed("reload"))
        {
            StartReload();
        }

        if (Input.IsActionPressed("shoot") && _shootTimer <= 0.0f)
        {
            if (InventoryComponent != null && InventoryComponent.CurrentAmmo > 0 && !InventoryComponent.IsReloading)
            {
                Shoot();
            }
            else if (InventoryComponent != null && InventoryComponent.CurrentAmmo <= 0 && !InventoryComponent.IsReloading)
            {
                StartReload();
            }
        }
    }

    public void Shoot()
    {
        if (InventoryComponent == null || !InventoryComponent.TryConsumeAmmo())
            return;

        _shootTimer = FireRate;

        Vector2 muzzleGlobal = ToGlobal(new Vector2(55, 0));
        float spread = (float)GD.RandRange(-0.025, 0.025);

        if (ProjectilePool.Instance != null)
        {
            ProjectilePool.Instance.Spawn(muzzleGlobal, GlobalRotation + spread, GetParent());
        }

        if (MuzzleFlash != null)
        {
            MuzzleFlash.Energy = 2.4f;
            var flashTween = CreateTween();
            flashTween.TweenProperty(MuzzleFlash, "energy", 0.0f, 0.06);
        }

        if (MuzzleParticles != null)
        {
            MuzzleParticles.Restart();
            MuzzleParticles.Emitting = true;
        }

        if (GunSprite != null)
        {
            GunSprite.Position = new Vector2(_gunRestPos.X - 3.5f, _gunRestPos.Y);
        }

        AddTrauma(0.18f);

        if (InventoryComponent.CurrentAmmo == 0)
        {
            StartReload();
        }
    }

    public void StartReload()
    {
        if (InventoryComponent == null || !InventoryComponent.StartReload())
            return;

        if (GunSprite != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(GunSprite, "position", new Vector2(_gunRestPos.X - 5.0f, _gunRestPos.Y - 4.0f), InventoryComponent.ReloadDuration * 0.5f);
            tween.TweenProperty(GunSprite, "position", _gunRestPos, InventoryComponent.ReloadDuration * 0.5f);
        }
    }

    public void AnimateWalk(float delta)
    {
        _walkPhase += delta * 15.0f;
        float bob = Mathf.Sin(_walkPhase) * 0.09f;

        if (BodySprite != null)
        {
            BodySprite.Scale = new Vector2(_baseScale.X - bob * 0.4f, _baseScale.Y + bob);
            BodySprite.Rotation = Mathf.Sin(_walkPhase * 0.5f) * 0.05f;
        }

        if (DustParticles != null && !DustParticles.Emitting)
        {
            DustParticles.Emitting = true;
        }
    }

    public void AnimateIdle(float delta)
    {
        if (BodySprite != null)
        {
            BodySprite.Scale = BodySprite.Scale.Lerp(_baseScale, delta * 12.0f);
            BodySprite.Rotation = Mathf.Lerp(BodySprite.Rotation, 0.0f, delta * 12.0f);
        }

        if (DustParticles != null && DustParticles.Emitting)
        {
            DustParticles.Emitting = false;
        }
    }

    public void SpawnDashGhost()
    {
        var parent = GetParent();
        if (parent == null || BodySprite == null) return;

        var ghost = new Sprite2D
        {
            Texture = BodySprite.Texture,
            GlobalPosition = GlobalPosition,
            GlobalRotation = GlobalRotation,
            Scale = BodySprite.Scale,
            Modulate = new Color(0.3f, 0.7f, 1.0f, 0.6f)
        };
        parent.AddChild(ghost);

        var tween = ghost.CreateTween();
        tween.TweenProperty(ghost, "modulate", new Color(0.3f, 0.7f, 1.0f, 0.0f), 0.25);
        tween.TweenCallback(Callable.From(ghost.QueueFree));

        if (DustParticles != null)
        {
            DustParticles.Restart();
            DustParticles.Emitting = true;
        }
    }

    public void TakeDamage(float amount, Vector2 knockbackDir)
    {
        if (IsDead || IsInvulnerable) return;

        HealthComponent?.TakeDamage(amount, knockbackDir);
        MovementComponent?.ApplyKnockback(knockbackDir, 240.0f);
        AddTrauma(0.4f);

        if (BodySprite != null)
        {
            var tween = CreateTween();
            BodySprite.Modulate = new Color(3.5f, 0.4f, 0.4f, 1.0f);
            tween.TweenProperty(BodySprite, "modulate", Colors.White, 0.15);
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;

        HealthComponent?.Heal(amount);
        if (BodySprite != null)
        {
            var tween = CreateTween();
            BodySprite.Modulate = new Color(0.4f, 2.5f, 0.6f, 1.0f);
            tween.TweenProperty(BodySprite, "modulate", Colors.White, 0.2);
        }
    }

    public void AddTrauma(float amount)
    {
        _cameraTrauma = Mathf.Clamp(_cameraTrauma + amount, 0.0f, 1.0f);
    }

    private void UpdateCamera(float delta)
    {
        if (Camera == null) return;

        Vector2 targetOffset = Vector2.Zero;
        if (!IsDead)
        {
            Vector2 mouseLocal = GetLocalMousePosition();
            targetOffset = (mouseLocal * 0.16f).Clamp(new Vector2(-120, -120), new Vector2(120, 120));
        }

        _cameraTrauma = Mathf.Max(0.0f, _cameraTrauma - delta * 1.8f);
        float shakeStrength = _cameraTrauma * _cameraTrauma * 16.0f;
        Vector2 shakeOffset = new(
            (float)GD.RandRange(-shakeStrength, shakeStrength),
            (float)GD.RandRange(-shakeStrength, shakeStrength)
        );

        Camera.Offset = Camera.Offset.Lerp(targetOffset, delta * 5.0f) + shakeOffset;
    }

    private void UpdateLaserSight()
    {
        if (LaserLine == null || LaserRay == null) return;

        Vector2 muzzlePos = new(50, 0);
        float maxRange = 800.0f;

        _laserPoints[0] = muzzlePos;
        if (LaserRay.IsColliding())
        {
            Vector2 hitPoint = ToLocal(LaserRay.GetCollisionPoint());
            _laserPoints[1] = hitPoint;
            LaserLine.Points = _laserPoints;
            if (LaserDot != null)
            {
                LaserDot.Visible = true;
                LaserDot.Position = hitPoint;
            }
        }
        else
        {
            _laserPoints[1] = muzzlePos + new Vector2(maxRange, 0);
            LaserLine.Points = _laserPoints;
            if (LaserDot != null)
            {
                LaserDot.Visible = false;
            }
        }
    }

    private void OnDied()
    {
        StateMachine.ChangeState(DeadState);
    }

    public void OnDeathSequence()
    {
        AddTrauma(0.8f);

        if (LaserLine != null) LaserLine.Visible = false;
        if (LaserDot != null) LaserDot.Visible = false;
        FlashlightComponent?.SetLightState(false, 0.2f);

        if (BodySprite != null)
        {
            var tween = CreateTween();
            tween.Parallel().TweenProperty(BodySprite, "rotation", Rotation + 1.2f, 0.4);
            tween.Parallel().TweenProperty(BodySprite, "modulate", new Color(0.4f, 0.4f, 0.5f, 0.8f), 0.5);
        }

        EmitSignal(SignalName.PlayerDied);
    }
}
