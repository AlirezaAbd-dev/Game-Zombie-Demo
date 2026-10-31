using System.Collections.Generic;
using GameZombieDemo;
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
    public delegate void WeaponHudUpdatedEventHandler(string weaponName, int mag, int reserve, bool infinite, bool isReloading, float reloadProgress, bool isEmpowered, int grenades);

    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Export] public float Speed { get; set; } = 300.0f;
    [Export] public float MaxHealth { get; set; } = 100.0f;

    // Roguelite Stat Modifiers
    public float DamageMultiplier { get; set; } = 1.0f;
    public float FireRateMultiplier { get; set; } = 1.0f;
    public float ReloadDurationMultiplier { get; set; } = 1.0f;
    public int BonusPierce { get; set; } = 0;
    public bool DashBurnsEnemies { get; set; } = false;
    public int VampiricKillInterval { get; set; } = 0;
    public float ExternalSpeedMultiplier { get; set; } = 1.0f;

    // Grenades
    public int Grenades { get; set; } = 2;
    public int MaxGrenades { get; set; } = 3;
    public float GrenadeCooldown { get; set; } = 8.0f;
    private float _grenadeCooldownTimer = 0.0f;

    // Weapon Inventory
    public class WeaponAmmoState
    {
        public int CurrentMag;
        public int CurrentReserve;
    }

    private readonly Dictionary<WeaponId, WeaponAmmoState> _weaponStates = new();
    public WeaponId CurrentWeaponId { get; private set; } = WeaponId.Pistol;
    public WeaponSpec CurrentSpec => WeaponSpec.Presets[CurrentWeaponId];

    public int CurrentAmmo => _weaponStates[CurrentWeaponId].CurrentMag;
    public int ReserveAmmo => _weaponStates[CurrentWeaponId].CurrentReserve;
    public int MaxReserveAmmo => CurrentSpec.MaxReserve;

    public bool IsReloading { get; private set; } = false;
    private float _reloadElapsed = 0.0f;
    private bool _activeReloadAttempted = false;
    private bool _isCurrentMagEmpowered = false;
    private float _currentBloomRad = 0.0f;

    // Stamina & Dash
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
    public float CameraTrauma => _cameraTrauma;

    private float _shootTimer = 0.0f;
    private float _cameraTrauma = 0.0f;
    private float _walkPhase = 0.0f;
    private readonly Vector2 _baseScale = new(3.0f, 3.0f);
    private readonly Vector2 _gunRestPos = new(12.333334f, -0.33333334f);
    private int _vampiricKillCounter = 0;

    private PackedScene _bulletScene = null!;
    private Camera2D _camera = null!;
    private Sprite2D _bodySprite = null!;
    private Sprite2D _gunSprite = null!;
    private PointLight2D _flashlight = null!;
    private PointLight2D _muzzleFlash = null!;
    private CpuParticles2D _muzzleParticles = null!;
    private RayCast2D _laserRay = null!;
    private Line2D _laserLine = null!;
    private Sprite2D _laserDot = null!;
    private CpuParticles2D _dustParticles = null!;
    private Crosshair? _crosshair;

    public override void _Ready()
    {
        AddToGroup("player");
        Health = MaxHealth;
        CurrentStamina = MaxStamina;

        foreach (var kvp in WeaponSpec.Presets)
        {
            _weaponStates[kvp.Key] = new WeaponAmmoState
            {
                CurrentMag = kvp.Value.MagCapacity,
                CurrentReserve = kvp.Value.MaxReserve
            };
        }

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

        _crosshair = GetTree().CurrentScene?.GetNodeOrNull<Crosshair>("UIOverlay/Crosshair");

        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
        EmitWeaponHud();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsDead) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Key1)
            {
                SwitchWeapon(WeaponId.Pistol);
            }
            else if (keyEvent.Keycode == Key.Key2)
            {
                SwitchWeapon(WeaponId.Shotgun);
            }
            else if (keyEvent.Keycode == Key.Key3)
            {
                SwitchWeapon(WeaponId.Smg);
            }
            else if (keyEvent.Keycode == Key.G)
            {
                TryLaunchGrenade();
            }
        }
        else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
            {
                CycleWeapon(-1);
            }
            else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
            {
                CycleWeapon(1);
            }
            else if (mouseEvent.ButtonIndex == MouseButton.Middle)
            {
                TryLaunchGrenade();
            }
        }
    }

    private void CycleWeapon(int dir)
    {
        int count = 3;
        int next = (((int)CurrentWeaponId + dir) % count + count) % count;
        SwitchWeapon((WeaponId)next);
    }

    public void SwitchWeapon(WeaponId newId)
    {
        if (CurrentWeaponId == newId) return;

        if (IsReloading)
        {
            IsReloading = false;
        }

        CurrentWeaponId = newId;
        _isCurrentMagEmpowered = false;
        _activeReloadAttempted = false;
        _currentBloomRad = 0.0f;

        AudioManager.Instance?.PlayGlobal("SfxReload", 1.25f);
        EmitWeaponHud();
    }

    public void TryLaunchGrenade()
    {
        if (Grenades <= 0 || IsDead) return;

        Grenades--;
        var grenade = new FragGrenade();
        grenade.Launch(GlobalPosition, GetGlobalMousePosition());
        GetParent().AddChild(grenade);
        EmitWeaponHud();
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

        if (_crosshair == null)
        {
            _crosshair = GetTree().CurrentScene?.GetNodeOrNull<Crosshair>("UIOverlay/Crosshair");
        }
        _crosshair?.SetDynamicSpread(CurrentSpec.BaseSpreadRad + _currentBloomRad);

        // Bloom decay
        _currentBloomRad = Mathf.Max(0.0f, _currentBloomRad - CurrentSpec.BloomDecayPerSec * dt);

        // Grenade recharge
        if (Grenades < MaxGrenades)
        {
            _grenadeCooldownTimer += dt;
            if (_grenadeCooldownTimer >= GrenadeCooldown)
            {
                _grenadeCooldownTimer = 0.0f;
                Grenades = Mathf.Min(MaxGrenades, Grenades + 1);
                EmitWeaponHud();
            }
        }

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

        // Dash trigger
        if (Input.IsActionJustPressed("dash") && !IsDashing && CurrentStamina >= DashCost && moveDir != Vector2.Zero)
        {
            StartDash(moveDir.Normalized());
        }

        // Active Dash
        if (IsDashing)
        {
            _dashTimer -= dt;
            Velocity = _dashDirection * DashSpeed * ExternalSpeedMultiplier;

            if (DashBurnsEnemies)
            {
                BurnNearbyZombies();
            }

            if (_dashTimer <= 0.0f)
            {
                IsDashing = false;
                IsInvulnerable = false;
            }

            MoveAndSlide();
            return;
        }

        // Normal movement
        float effectiveSpeed = Speed * ExternalSpeedMultiplier;
        if (moveDir != Vector2.Zero)
        {
            Velocity = effectiveSpeed * moveDir.Normalized();
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
                Mathf.MoveToward(Velocity.X, 0.0f, effectiveSpeed),
                Mathf.MoveToward(Velocity.Y, 0.0f, effectiveSpeed)
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

    private void BurnNearbyZombies()
    {
        foreach (var node in GetTree().GetNodesInGroup("zombies"))
        {
            if (node is Node2D zombie && IsInstanceValid(zombie))
            {
                if (GlobalPosition.DistanceTo(zombie.GlobalPosition) <= 65.0f)
                {
                    if (zombie.HasMethod("TakeDamage"))
                    {
                        zombie.Call("TakeDamage", 40.0f, GlobalPosition, 1.0f);
                    }
                }
            }
        }
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
        AudioManager.Instance?.PlayAt("SfxDash", GlobalPosition);
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
            Modulate = DashBurnsEnemies ? new Color(1.0f, 0.5f, 0.1f, 0.7f) : new Color(0.3f, 0.7f, 1.0f, 0.6f)
        };
        parent.AddChild(ghost);

        var tween = ghost.CreateTween();
        tween.TweenProperty(ghost, "modulate:a", 0.0f, 0.25);
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
            if (IsReloading)
            {
                AttemptActiveReload();
            }
            else
            {
                StartReload();
            }
        }

        float effectiveInterval = CurrentSpec.FireInterval / FireRateMultiplier;
        if (Input.IsActionPressed("shoot") && _shootTimer <= 0.0f)
        {
            var state = _weaponStates[CurrentWeaponId];
            if (state.CurrentMag > 0 && !IsReloading)
            {
                Shoot();
            }
            else if (state.CurrentMag <= 0 && !IsReloading)
            {
                StartReload();
            }
        }
    }

    public void StartReload()
    {
        var state = _weaponStates[CurrentWeaponId];
        if (IsReloading || state.CurrentMag >= CurrentSpec.MagCapacity)
            return;

        if (!CurrentSpec.InfiniteReserve && state.CurrentReserve <= 0)
        {
            AudioManager.Instance?.PlayGlobal("SfxDryFire", 1.0f);
            return;
        }

        IsReloading = true;
        _reloadElapsed = 0.0f;
        _activeReloadAttempted = false;
        _isCurrentMagEmpowered = false;

        AudioManager.Instance?.PlayAt("SfxReload", GlobalPosition);
        EmitWeaponHud();

        if (_gunSprite != null)
        {
            float dur = CurrentSpec.ReloadDuration * ReloadDurationMultiplier;
            var tween = CreateTween();
            tween.TweenProperty(_gunSprite, "position", new Vector2(_gunRestPos.X - 5.0f, _gunRestPos.Y - 4.0f), dur * 0.5f);
            tween.TweenProperty(_gunSprite, "position", _gunRestPos, dur * 0.5f);
        }
    }

    private void AttemptActiveReload()
    {
        if (_activeReloadAttempted) return;
        _activeReloadAttempted = true;

        float effectiveDuration = CurrentSpec.ReloadDuration * ReloadDurationMultiplier;
        float progress = Mathf.Clamp(_reloadElapsed / effectiveDuration, 0.0f, 1.0f);

        if (progress >= 0.45f && progress <= 0.65f)
        {
            // Sweet spot hit!
            FinishReload(empowered: true);
            AudioManager.Instance?.PlayGlobal("SfxPickup", 1.35f);
        }
        else
        {
            // Jam penalty!
            _reloadElapsed = Mathf.Max(0.0f, _reloadElapsed - 0.45f);
            AudioManager.Instance?.PlayGlobal("SfxDryFire", 0.9f);
            EmitWeaponHud();
        }
    }

    private void HandleReloading(float delta)
    {
        if (!IsReloading) return;

        _reloadElapsed += delta;
        float effectiveDuration = CurrentSpec.ReloadDuration * ReloadDurationMultiplier;

        if (_reloadElapsed >= effectiveDuration)
        {
            FinishReload(empowered: false);
        }
        else
        {
            EmitWeaponHud();
        }
    }

    private void FinishReload(bool empowered)
    {
        var state = _weaponStates[CurrentWeaponId];
        int needed = CurrentSpec.MagCapacity - state.CurrentMag;

        if (CurrentSpec.InfiniteReserve)
        {
            state.CurrentMag = CurrentSpec.MagCapacity;
        }
        else
        {
            int added = Mathf.Min(needed, state.CurrentReserve);
            state.CurrentMag += added;
            state.CurrentReserve -= added;
        }

        IsReloading = false;
        _isCurrentMagEmpowered = empowered;
        EmitWeaponHud();
    }

    public void Shoot()
    {
        float effectiveInterval = CurrentSpec.FireInterval / FireRateMultiplier;
        _shootTimer = effectiveInterval;

        var state = _weaponStates[CurrentWeaponId];
        state.CurrentMag--;

        // Bloom accumulation
        _currentBloomRad = Mathf.Min(CurrentSpec.MaxBloomRad, _currentBloomRad + CurrentSpec.BloomPerShotRad);
        float totalSpread = CurrentSpec.BaseSpreadRad + _currentBloomRad;

        // Bullets
        if (_bulletScene != null)
        {
            Vector2 muzzleGlobal = ToGlobal(new Vector2(55, 0));
            for (int i = 0; i < CurrentSpec.PelletCount; i++)
            {
                var bullet = _bulletScene.Instantiate<Bullet>();
                bullet.GlobalPosition = muzzleGlobal;
                float spreadOffset = (float)GD.RandRange(-totalSpread, totalSpread);
                bullet.GlobalRotation = GlobalRotation + spreadOffset;
                bullet.Speed = CurrentSpec.BulletSpeed;
                bullet.Damage = CurrentSpec.BulletDamage * DamageMultiplier;
                bullet.KnockbackMultiplier = CurrentSpec.KnockbackMultiplier;
                bullet.RemainingPierces = CurrentSpec.PiercingCount + BonusPierce;
                bullet.MaxLifetime = CurrentSpec.BulletLifetime;
                bullet.IsEmpowered = _isCurrentMagEmpowered;

                GetParent().AddChild(bullet);
            }
        }

        // Audio
        AudioManager.Instance?.PlayAt(CurrentSpec.SfxKey, GlobalPosition);

        // Muzzle Flash
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

        AddTrauma(CurrentSpec.ShakeTrauma);
        EmitWeaponHud();

        if (state.CurrentMag == 0)
        {
            StartReload();
        }
    }

    public void OnZombieKilled()
    {
        if (VampiricKillInterval > 0)
        {
            _vampiricKillCounter++;
            if (_vampiricKillCounter >= VampiricKillInterval)
            {
                _vampiricKillCounter = 0;
                Heal(6.0f);
            }
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;

        Health = Mathf.Min(MaxHealth, Health + amount);
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        AudioManager.Instance?.PlayGlobal("SfxPickup", 1.0f);

        var tween = CreateTween();
        _bodySprite.Modulate = new Color(0.4f, 2.5f, 0.6f, 1.0f);
        tween.TweenProperty(_bodySprite, "modulate", Colors.White, 0.2);
    }

    public void AddReserveAmmo(int amount)
    {
        if (IsDead) return;

        foreach (var kvp in _weaponStates)
        {
            var spec = WeaponSpec.Presets[kvp.Key];
            if (!spec.InfiniteReserve)
            {
                kvp.Value.CurrentReserve = Mathf.Min(spec.MaxReserve, kvp.Value.CurrentReserve + amount);
            }
        }

        AudioManager.Instance?.PlayGlobal("SfxPickup", 1.15f);
        EmitWeaponHud();

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

    private void EmitWeaponHud()
    {
        var state = _weaponStates[CurrentWeaponId];
        float effectiveDuration = CurrentSpec.ReloadDuration * ReloadDurationMultiplier;
        float progress = IsReloading ? Mathf.Clamp(_reloadElapsed / effectiveDuration, 0.0f, 1.0f) : 0.0f;

        EmitSignal(
            SignalName.WeaponHudUpdated,
            CurrentSpec.DisplayName,
            state.CurrentMag,
            state.CurrentReserve,
            CurrentSpec.InfiniteReserve,
            IsReloading,
            progress,
            _isCurrentMagEmpowered,
            Grenades
        );

        EmitSignal(SignalName.AmmoChanged, state.CurrentMag, state.CurrentReserve, IsReloading);
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
