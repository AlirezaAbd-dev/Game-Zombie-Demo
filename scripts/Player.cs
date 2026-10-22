using Godot;

public partial class Player : CharacterBody2D
{
    [Signal]
    public delegate void HealthChangedEventHandler(float current, float maximum);

    [Signal]
    public delegate void StaminaChangedEventHandler(float current, float maximum);

    [Signal]
    public delegate void AmmoChangedEventHandler(int current, int reserve, bool isReloading);

    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Export] public float Speed { get; set; } = 300.0f;
    [Export] public float MaxHealth { get; set; } = 100.0f;
    [Export] public float FireRate { get; set; } = 0.13f;

    // Resource Management: Ammo
    [Export] public int MagCapacity { get; set; } = 15;
    [Export] public int MaxReserveAmmo { get; set; } = 90;
    [Export] public float ReloadDuration { get; set; } = 1.2f;

    public int CurrentAmmo { get; private set; } = 15;
    public int ReserveAmmo { get; private set; } = 45;
    public bool IsReloading { get; private set; } = false;
    private float _reloadTimer = 0.0f;

    // Resource Management: Stamina & Dash
    [Export] public float MaxStamina { get; set; } = 100.0f;
    [Export] public float DashCost { get; set; } = 35.0f;
    [Export] public float DashSpeed { get; set; } = 750.0f;
    [Export] public float DashDuration { get; set; } = 0.22f;
    [Export] public float StaminaRegenRate { get; set; } = 30.0f;

    public float CurrentStamina { get; private set; } = 100.0f;
    public bool IsDashing { get; private set; } = false;
    public bool IsInvulnerable { get; private set; } = false;
    private float _dashTimer = 0.0f;
    private Vector2 _dashDirection = Vector2.Zero;

    public float Health { get; private set; }
    public bool IsDead { get; private set; } = false;

    private float _shootTimer = 0.0f;
    private float _cameraTrauma = 0.0f;
    private float _walkPhase = 0.0f;
    private readonly Vector2 _baseScale = new(3.0f, 3.0f);
    private readonly Vector2 _gunRestPos = new(12.333334f, -0.33333334f);

    private PackedScene _bulletScene;

    private Camera2D _camera;
    private Sprite2D _bodySprite;
    private Sprite2D _gunSprite;
    private PointLight2D _flashlight;
    private PointLight2D _muzzleFlash;
    private CpuParticles2D _muzzleParticles;
    private RayCast2D _laserRay;
    private Line2D _laserLine;
    private Sprite2D _laserDot;
    private CpuParticles2D _dustParticles;

    public override void _Ready()
    {
        AddToGroup("player");
        Health = MaxHealth;
        CurrentStamina = MaxStamina;
        CurrentAmmo = MagCapacity;

        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, ReserveAmmo, IsReloading);

        _bulletScene = GD.Load<PackedScene>("res://scenes/bullet.tscn");

        _camera = GetNode<Camera2D>("Camera2D");
        _bodySprite = GetNode<Sprite2D>("Body");
        _gunSprite = GetNode<Sprite2D>("Body/Gun");
        _flashlight = GetNode<PointLight2D>("Flashlight");
        _muzzleFlash = GetNode<PointLight2D>("MuzzleFlash");
        _muzzleParticles = GetNode<CpuParticles2D>("MuzzleParticles");
        _laserRay = GetNode<RayCast2D>("LaserRayCast");
        _laserLine = GetNode<Line2D>("LaserLine");
        _laserDot = GetNode<Sprite2D>("LaserDot");
        _dustParticles = GetNode<CpuParticles2D>("FootstepDust");

        if (_camera != null)
        {
            _camera.PositionSmoothingEnabled = true;
            _camera.PositionSmoothingSpeed = 7.0f;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (IsDead)
        {
            UpdateCamera(dt);
            return;
        }

        LookAt(GetGlobalMousePosition());
        HandleShooting(dt);
        HandleReloading(dt);
        HandleStaminaRegen(dt);
        UpdateCamera(dt);
        UpdateLaserSight();
        UpdateFlashlightFlicker();

        if (_gunSprite != null)
        {
            _gunSprite.Position = _gunSprite.Position.Lerp(_gunRestPos, dt * 15.0f);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (IsDead)
        {
            Velocity = Velocity.MoveToward(Vector2.Zero, dt * 500.0f);
            MoveAndSlide();
            return;
        }

        Vector2 moveDir = new(
            Input.GetAxis("move_left", "move_right"),
            Input.GetAxis("move_up", "move_down")
        );

        // Handle Dash trigger
        if (Input.IsActionJustPressed("dash") && !IsDashing && CurrentStamina >= DashCost && moveDir != Vector2.Zero)
        {
            StartDash(moveDir.Normalized());
        }

        // Handle Active Dash
        if (IsDashing)
        {
            _dashTimer -= dt;
            Velocity = _dashDirection * DashSpeed;

            if (_dashTimer <= 0.0f)
            {
                IsDashing = false;
                IsInvulnerable = false;
            }

            MoveAndSlide();
            return;
        }

        // Normal Movement
        if (moveDir != Vector2.Zero)
        {
            Velocity = Speed * moveDir.Normalized();
            _walkPhase += dt * 15.0f;

            float bob = Mathf.Sin(_walkPhase) * 0.09f;
            _bodySprite.Scale = new Vector2(_baseScale.X - bob * 0.4f, _baseScale.Y + bob);
            _bodySprite.Rotation = Mathf.Sin(_walkPhase * 0.5f) * 0.05f;

            if (_dustParticles != null && !_dustParticles.Emitting)
            {
                _dustParticles.Emitting = true;
            }
        }
        else
        {
            Velocity = new Vector2(
                Mathf.MoveToward(Velocity.X, 0.0f, Speed),
                Mathf.MoveToward(Velocity.Y, 0.0f, Speed)
            );

            _bodySprite.Scale = _bodySprite.Scale.Lerp(_baseScale, dt * 12.0f);
            _bodySprite.Rotation = Mathf.Lerp(_bodySprite.Rotation, 0.0f, dt * 12.0f);

            if (_dustParticles != null && _dustParticles.Emitting)
            {
                _dustParticles.Emitting = false;
            }
        }

        MoveAndSlide();
    }

    private void StartDash(Vector2 direction)
    {
        IsDashing = true;
        IsInvulnerable = true;
        _dashTimer = DashDuration;
        _dashDirection = direction;

        CurrentStamina -= DashCost;
        EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);

        AddTrauma(0.2f);

        // Ghost after-image effect
        SpawnDashGhost();

        if (_dustParticles != null)
        {
            _dustParticles.Restart();
            _dustParticles.Emitting = true;
        }
    }

    private void SpawnDashGhost()
    {
        var parent = GetParent();
        if (parent == null || _bodySprite == null) return;

        var ghost = new Sprite2D
        {
            Texture = _bodySprite.Texture,
            GlobalPosition = GlobalPosition,
            GlobalRotation = GlobalRotation,
            Scale = _bodySprite.Scale,
            Modulate = new Color(0.3f, 0.7f, 1.0f, 0.6f)
        };
        parent.AddChild(ghost);

        var tween = ghost.CreateTween();
        tween.TweenProperty(ghost, "modulate", new Color(0.3f, 0.7f, 1.0f, 0.0f), 0.25);
        tween.TweenCallback(Callable.From(ghost.QueueFree));
    }

    private void HandleStaminaRegen(float delta)
    {
        if (!IsDashing && CurrentStamina < MaxStamina)
        {
            CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + StaminaRegenRate * delta);
            EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
        }
    }

    private void HandleShooting(float delta)
    {
        _shootTimer -= delta;

        if (Input.IsActionJustPressed("reload"))
        {
            StartReload();
        }

        if (Input.IsActionPressed("shoot") && _shootTimer <= 0.0f)
        {
            if (CurrentAmmo > 0 && !IsReloading)
            {
                Shoot();
            }
            else if (CurrentAmmo <= 0 && !IsReloading)
            {
                StartReload();
            }
        }
    }

    private void HandleReloading(float delta)
    {
        if (!IsReloading) return;

        _reloadTimer -= delta;
        if (_reloadTimer <= 0.0f)
        {
            int needed = MagCapacity - CurrentAmmo;
            int added = Mathf.Min(needed, ReserveAmmo);
            CurrentAmmo += added;
            ReserveAmmo -= added;
            IsReloading = false;

            EmitSignal(SignalName.AmmoChanged, CurrentAmmo, ReserveAmmo, false);
        }
    }

    public void StartReload()
    {
        if (IsReloading || CurrentAmmo >= MagCapacity || ReserveAmmo <= 0)
            return;

        IsReloading = true;
        _reloadTimer = ReloadDuration;
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, ReserveAmmo, true);

        // Weapon visual tilt during reload
        if (_gunSprite != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(_gunSprite, "position", new Vector2(_gunRestPos.X - 5.0f, _gunRestPos.Y - 4.0f), ReloadDuration * 0.5f);
            tween.TweenProperty(_gunSprite, "position", _gunRestPos, ReloadDuration * 0.5f);
        }
    }

    public void Shoot()
    {
        _shootTimer = FireRate;
        CurrentAmmo--;
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, ReserveAmmo, false);

        if (_bulletScene != null)
        {
            var bullet = _bulletScene.Instantiate<Area2D>();
            Vector2 muzzleGlobal = ToGlobal(new Vector2(55, 0));
            bullet.GlobalPosition = muzzleGlobal;
            bullet.GlobalRotation = GlobalRotation + (float)GD.RandRange(-0.025, 0.025);
            GetParent().AddChild(bullet);
        }

        if (_muzzleFlash != null)
        {
            _muzzleFlash.Energy = 2.4f;
            var tween = CreateTween();
            tween.TweenProperty(_muzzleFlash, "energy", 0.0f, 0.06);
        }

        if (_muzzleParticles != null)
        {
            _muzzleParticles.Restart();
            _muzzleParticles.Emitting = true;
        }

        if (_gunSprite != null)
        {
            _gunSprite.Position = new Vector2(_gunRestPos.X - 3.5f, _gunRestPos.Y);
        }

        AddTrauma(0.18f);

        if (CurrentAmmo == 0)
        {
            StartReload();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;

        Health = Mathf.Min(MaxHealth, Health + amount);
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);

        // Green healing flash
        var tween = CreateTween();
        _bodySprite.Modulate = new Color(0.4f, 2.5f, 0.6f, 1.0f);
        tween.TweenProperty(_bodySprite, "modulate", Colors.White, 0.2);
    }

    public void AddReserveAmmo(int amount)
    {
        if (IsDead) return;

        ReserveAmmo = Mathf.Min(MaxReserveAmmo, ReserveAmmo + amount);
        EmitSignal(SignalName.AmmoChanged, CurrentAmmo, ReserveAmmo, IsReloading);

        // Yellow ammo pickup flash on gun
        if (_gunSprite != null)
        {
            var tween = CreateTween();
            _gunSprite.Modulate = new Color(2.5f, 2.0f, 0.4f, 1.0f);
            tween.TweenProperty(_gunSprite, "modulate", Colors.White, 0.2);
        }
    }

    public void AddTrauma(float amount)
    {
        _cameraTrauma = Mathf.Clamp(_cameraTrauma + amount, 0.0f, 1.0f);
    }

    private void UpdateCamera(float delta)
    {
        if (_camera == null) return;

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

        _camera.Offset = _camera.Offset.Lerp(targetOffset, delta * 5.0f) + shakeOffset;
    }

    private void UpdateLaserSight()
    {
        if (_laserLine == null || _laserRay == null) return;

        Vector2 muzzlePos = new(50, 0);
        float maxRange = 800.0f;

        if (_laserRay.IsColliding())
        {
            Vector2 hitPoint = ToLocal(_laserRay.GetCollisionPoint());
            _laserLine.Points = new[] { muzzlePos, hitPoint };
            if (_laserDot != null)
            {
                _laserDot.Visible = true;
                _laserDot.Position = hitPoint;
            }
        }
        else
        {
            Vector2 endPoint = muzzlePos + new Vector2(maxRange, 0);
            _laserLine.Points = new[] { muzzlePos, endPoint };
            if (_laserDot != null)
            {
                _laserDot.Visible = false;
            }
        }
    }

    private void UpdateFlashlightFlicker()
    {
        if (_flashlight != null)
        {
            double t = Time.GetTicksMsec() * 0.003;
            _flashlight.Energy = 1.35f + (float)Mathf.Sin(t) * 0.04f + (float)Mathf.Sin(t * 2.7) * 0.02f;
        }
    }

    public void TakeDamage(float amount, Vector2 knockbackDir)
    {
        if (IsDead || IsInvulnerable) return;

        Health = Mathf.Max(0.0f, Health - amount);
        Velocity += knockbackDir * 240.0f;
        AddTrauma(0.4f);
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);

        var tween = CreateTween();
        _bodySprite.Modulate = new Color(3.5f, 0.4f, 0.4f, 1.0f);
        tween.TweenProperty(_bodySprite, "modulate", Colors.White, 0.15);

        if (Health <= 0.0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        AddTrauma(0.8f);

        if (_laserLine != null) _laserLine.Visible = false;
        if (_laserDot != null) _laserDot.Visible = false;
        if (_flashlight != null) _flashlight.Energy = 0.2f;

        var tween = CreateTween();
        tween.Parallel().TweenProperty(_bodySprite, "rotation", Rotation + 1.2f, 0.4);
        tween.Parallel().TweenProperty(_bodySprite, "modulate", new Color(0.4f, 0.4f, 0.5f, 0.8f), 0.5);

        EmitSignal(SignalName.PlayerDied);
    }
}
