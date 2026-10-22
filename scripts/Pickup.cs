using Godot;

public partial class Pickup : Area2D
{
    public enum PickupType
    {
        Health,
        Ammo
    }

    [Export] public PickupType Type { get; set; } = PickupType.Health;
    [Export] public float Amount { get; set; } = 35.0f;

    private Sprite2D _sprite;
    private PointLight2D _glowLight;
    private float _bobTimer = 0.0f;
    private Texture2D _medkitTex;
    private Texture2D _ammoTex;

    public override void _Ready()
    {
        _bobTimer = (float)GD.RandRange(0.0, Mathf.Tau);

        _sprite = GetNode<Sprite2D>("Sprite2D");
        _glowLight = GetNode<PointLight2D>("GlowLight");

        _medkitTex = GD.Load<Texture2D>("res://assets/textures/medkit.png");
        _ammoTex = GD.Load<Texture2D>("res://assets/textures/ammo_pack.png");

        UpdateVisuals();
        BodyEntered += OnBodyEntered;
    }

    public void Setup(PickupType type, float amount)
    {
        Type = type;
        Amount = amount;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (_sprite == null || _glowLight == null) return;

        if (Type == PickupType.Health)
        {
            _sprite.Texture = _medkitTex;
            _glowLight.Color = new Color(0.2f, 1.0f, 0.4f, 1.0f);
        }
        else
        {
            _sprite.Texture = _ammoTex;
            _glowLight.Color = new Color(1.0f, 0.85f, 0.2f, 1.0f);
        }
    }

    public override void _Process(double delta)
    {
        _bobTimer += (float)delta * 3.5f;
        float offset = Mathf.Sin(_bobTimer) * 4.0f;
        _sprite.Position = new Vector2(0, offset);

        if (_glowLight != null)
        {
            _glowLight.Energy = 0.75f + Mathf.Sin(_bobTimer * 1.5f) * 0.25f;
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player && !player.IsDead)
        {
            bool collected = false;
            if (Type == PickupType.Health)
            {
                if (player.Health < player.MaxHealth)
                {
                    player.Heal(Amount);
                    collected = true;
                }
            }
            else if (Type == PickupType.Ammo)
            {
                if (player.ReserveAmmo < player.MaxReserveAmmo)
                {
                    player.AddReserveAmmo((int)Amount);
                    collected = true;
                }
            }

            if (collected)
            {
                SpawnCollectEffect();
                QueueFree();
            }
        }
    }

    private void SpawnCollectEffect()
    {
        var parent = GetParent();
        if (parent == null) return;

        var particles = new CpuParticles2D
        {
            GlobalPosition = GlobalPosition,
            Emitting = true,
            OneShot = true,
            Explosiveness = 1.0f,
            Amount = 16,
            Lifetime = 0.35f,
            Spread = 180.0f,
            InitialVelocityMin = 30.0f,
            InitialVelocityMax = 90.0f,
            Gravity = Vector2.Zero,
            ScaleAmountMin = 2.0f,
            ScaleAmountMax = 4.0f,
            Color = Type == PickupType.Health ? new Color(0.2f, 1.0f, 0.4f, 0.9f) : new Color(1.0f, 0.9f, 0.3f, 0.9f)
        };
        particles.Finished += particles.QueueFree;
        parent.AddChild(particles);
    }
}
