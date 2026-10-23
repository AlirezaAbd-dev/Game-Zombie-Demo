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

    [ExportGroup("Components")]
    [Export] public HealthComponent HealthComponent { get; set; }
    [Export] public MovementComponent MovementComponent { get; set; }
    [Export] public StaminaComponent StaminaComponent { get; set; }
    [Export] public InventoryComponent InventoryComponent { get; set; }
    [Export] public FlashlightComponent FlashlightComponent { get; set; }
    [Export] public PlayerInteractionComponent InteractionComponent { get; set; }

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
    public bool IsAiming { get; private set; } = false;
    public bool IsDead => HealthComponent?.IsDead ?? false;

    private float _shootTimer = 0.0f;
    private float _cameraTrauma = 0.0f;
    private float _walkPhase = 0.0f;
    private readonly Vector2 _baseScale = new(3.0f, 3.0f);
    private Vector2 _gunRestPos = new(12.333334f, -0.33333334f);
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

        if (HealthComponent != null)
        {
            HealthComponent.Died += OnDied;
        }

        if (InventoryComponent != null)
        {
            InventoryComponent.WeaponChanged += OnWeaponChanged;
            if (InventoryComponent.CurrentWeapon != null)
            {
                OnWeaponChanged(InventoryComponent.CurrentWeapon);
            }
        }

        StateMachine.ChangeState(IdleState);
    }

    private void ValidateAndBindNodes()
    {
        HealthComponent ??= GetNodeOrNull<HealthComponent>("%HealthComponent");
        MovementComponent ??= GetNodeOrNull<MovementComponent>("%MovementComponent");
        StaminaComponent ??= GetNodeOrNull<StaminaComponent>("%StaminaComponent");
        InventoryComponent ??= GetNodeOrNull<InventoryComponent>("%InventoryComponent");
        FlashlightComponent ??= GetNodeOrNull<FlashlightComponent>("%FlashlightComponent");
        InteractionComponent ??= GetNodeOrNull<PlayerInteractionComponent>("%InteractionComponent");

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

        if (_adrenalineTimer > 0.0f)
        {
            _adrenalineTimer -= dt;
            StaminaComponent?.Regenerate(dt * 45.0f);
            if (_adrenalineTimer <= 0.0f && MovementComponent != null)
            {
                MovementComponent.BaseSpeed = 300.0f;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        MovementComponent?.DecayKnockback(dt);
        StateMachine?.PhysicsUpdate(dt);
        GlobalPosition = WorldManager.ClampToPlayableBounds(GlobalPosition);
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

        // Aiming State
        IsAiming = Input.IsActionPressed("aim");

        // Weapon Switch Inputs
        HandleWeaponSwitchInput();

        if (Input.IsActionJustPressed("finisher"))
        {
            if (TryExecuteFinisher())
            {
                return;
            }
        }

        if (Input.IsActionJustPressed("interact"))
        {
            if (TryExecuteFinisher())
            {
                return;
            }
            InteractionComponent?.TryTriggerInteraction();
        }

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

    private void HandleWeaponSwitchInput()
    {
        if (InventoryComponent == null) return;

        if (Input.IsActionJustPressed("weapon_1"))
        {
            InventoryComponent.SelectWeapon(0);
        }
        else if (Input.IsActionJustPressed("weapon_2"))
        {
            InventoryComponent.SelectWeapon(1);
        }
        else if (Input.IsActionJustPressed("weapon_3"))
        {
            InventoryComponent.SelectWeapon(2);
        }
        else if (Input.IsActionJustPressed("next_weapon") || Input.IsActionJustPressed("switch_weapon"))
        {
            InventoryComponent.NextWeapon();
        }
        else if (Input.IsActionJustPressed("prev_weapon"))
        {
            InventoryComponent.PreviousWeapon();
        }
    }

    private void OnWeaponChanged(WeaponData weapon)
    {
        if (weapon == null) return;

        _gunRestPos = weapon.WeaponOffset;
        if (GunSprite != null)
        {
            GunSprite.Texture = weapon.WeaponSprite;
            GunSprite.Position = weapon.WeaponOffset;

            // Weapon switch equip visual bounce
            var tween = CreateTween();
            GunSprite.Scale = new Vector2(0.5f, 1.4f);
            tween.TweenProperty(GunSprite, "scale", Vector2.One, 0.15);
        }

        if (MuzzleFlash != null)
        {
            MuzzleFlash.Color = weapon.MuzzleFlashColor;
            MuzzleFlash.Position = weapon.MuzzleOffset;
        }

        if (MuzzleParticles != null)
        {
            MuzzleParticles.Position = weapon.MuzzleOffset;
        }
    }

    public void Shoot()
    {
        var weapon = InventoryComponent?.CurrentWeapon;
        if (weapon == null || !InventoryComponent.TryConsumeAmmo())
            return;

        _shootTimer = weapon.FireRate;

        Vector2 muzzleGlobal = ToGlobal(weapon.MuzzleOffset);
        float spreadRange = IsAiming ? weapon.AimSpread : weapon.BaseSpread;

        for (int i = 0; i < weapon.Pellets; i++)
        {
            float spread = (float)GD.RandRange(-spreadRange, spreadRange);
            if (ProjectilePool.Instance != null)
            {
                ProjectilePool.Instance.Spawn(muzzleGlobal, GlobalRotation + spread, GetParent(), weapon.Damage, weapon.ProjectileSpeed);
            }
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
            GunSprite.Position = new Vector2(_gunRestPos.X - weapon.RecoilKick, _gunRestPos.Y);
        }

        AddTrauma(weapon.CameraTrauma);

        StoryDirector.Instance?.RecordShotFired();

        AudioManager.Instance?.PlayGunshot(weapon.WeaponId, GlobalPosition);

        if (InventoryComponent.CurrentAmmo == 0)
        {
            StartReload();
        }
    }

    public void StartReload()
    {
        if (InventoryComponent == null || !InventoryComponent.StartReload())
            return;

        var weapon = InventoryComponent.CurrentWeapon;
        float duration = weapon?.ReloadDuration ?? 1.2f;

        if (GunSprite != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(GunSprite, "position", new Vector2(_gunRestPos.X - 5.0f, _gunRestPos.Y - 4.0f), duration * 0.5f);
            tween.TweenProperty(GunSprite, "position", _gunRestPos, duration * 0.5f);
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

    public bool TryExecuteFinisher()
    {
        if (IsDead || StateMachine?.CurrentState is PlayerFinisherState)
            return false;

        var zombies = GetTree()?.GetNodesInGroup("zombies");
        if (zombies == null) return false;

        Zombie bestCandidate = null;
        float minDistance = float.MaxValue;
        Vector2 playerPos = GlobalPosition;

        foreach (var node in zombies)
        {
            if (node is Zombie z && IsInstanceValid(z) && !z.IsDead)
            {
                if (z.FinisherTarget != null && z.FinisherTarget.CanBeFinishedBy(this))
                {
                    float dist = playerPos.DistanceTo(z.GlobalPosition);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestCandidate = z;
                    }
                }
            }
        }

        if (bestCandidate != null)
        {
            int nearbyEnemies = 0;
            foreach (var node in zombies)
            {
                if (node is Zombie other && IsInstanceValid(other) && !other.IsDead)
                {
                    if (bestCandidate.GlobalPosition.DistanceTo(other.GlobalPosition) <= 150.0f)
                    {
                        nearbyEnemies++;
                    }
                }
            }

            FinisherType finisherType = (nearbyEnemies > 1 || (float)GD.RandRange(0.0, 1.0) > 0.5f)
                ? FinisherType.RoundhouseKick
                : FinisherType.CombatKnife;

            StateMachine.ChangeState(new PlayerFinisherState(this, bestCandidate, finisherType));
            return true;
        }

        return false;
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
        AudioManager.Instance?.PlaySound(SoundType.PlayerHurt, GlobalPosition);

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

    private float _adrenalineTimer = 0.0f;

    public void ApplyAdrenalineBoost(float duration)
    {
        if (IsDead) return;
        _adrenalineTimer = duration;

        if (MovementComponent != null)
        {
            MovementComponent.BaseSpeed = 420.0f;
        }

        if (BodySprite != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(BodySprite, "modulate", new Color(2.5f, 0.5f, 0.5f, 1.0f), 0.2);
            tween.TweenProperty(BodySprite, "modulate", Colors.White, 0.3);
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
        Vector2 targetZoom = IsAiming ? new Vector2(1.22f, 1.22f) : Vector2.One;

        if (!IsDead)
        {
            Vector2 mouseLocal = GetLocalMousePosition();
            float shiftRatio = IsAiming ? 0.32f : 0.16f;
            float maxShift = IsAiming ? 180.0f : 120.0f;
            targetOffset = (mouseLocal * shiftRatio).Clamp(new Vector2(-maxShift, -maxShift), new Vector2(maxShift, maxShift));
        }

        Camera.Zoom = Camera.Zoom.Lerp(targetZoom, delta * 6.0f);

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

        var weapon = InventoryComponent?.CurrentWeapon;
        Vector2 muzzlePos = weapon?.MuzzleOffset ?? new Vector2(50, 0);
        float maxRange = 800.0f;

        // Dynamic laser appearance on aim
        if (IsAiming)
        {
            LaserLine.Width = 2.2f;
            LaserLine.Modulate = new Color(2.5f, 0.3f, 0.3f, 1.0f);
        }
        else
        {
            LaserLine.Width = 1.2f;
            LaserLine.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.45f);
        }

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
                LaserDot.Scale = IsAiming ? new Vector2(0.06f, 0.06f) : new Vector2(0.038f, 0.038f);
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
