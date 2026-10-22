using Godot;

public partial class Zombie : CharacterBody2D
{
    [Export] public float MaxHealth { get; set; } = 50.0f;
    [Export] public float BaseSpeed { get; set; } = 135.0f;
    [Export] public float AttackDamage { get; set; } = 15.0f;
    [Export] public float AttackInterval { get; set; } = 0.8f;

    public float Health { get; private set; }
    private float _speed;
    private Vector2 _knockbackVelocity = Vector2.Zero;
    private float _attackTimer = 0.0f;
    private float _walkPhase = 0.0f;
    private readonly Vector2 _baseScale = new(3.0f, 3.0f);
    public bool IsDead { get; private set; } = false;

    private Sprite2D _sprite;
    private Area2D _attackArea;
    private PointLight2D _eyesLight;
    private Node2D _player;
    private PackedScene _pickupScene;

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
        Vector2 dir = toPlayer.Normalized();

        Rotation = Mathf.LerpAngle(Rotation, dir.Angle(), dt * 8.0f);

        _walkPhase += dt * 9.0f;
        float wobble = Mathf.Sin(_walkPhase) * 0.1f;
        _sprite.Rotation = wobble;
        _sprite.Scale = _baseScale + new Vector2(wobble * 0.4f, -wobble * 0.4f);

        if (dist > 35.0f)
        {
            Velocity = dir * _speed + _knockbackVelocity;
        }
        else
        {
            Velocity = _knockbackVelocity;
        }

        _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, dt * 900.0f);
        MoveAndSlide();

        HandleAttack(dt);
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

    public void TakeDamage(float amount, Vector2 knockbackDir)
    {
        if (IsDead) return;

        Health -= amount;
        _knockbackVelocity = knockbackDir * 320.0f;

        var tween = CreateTween();
        _sprite.Modulate = new Color(3.5f, 0.4f, 0.4f, 1.0f);
        tween.TweenProperty(_sprite, "modulate", Colors.White, 0.1);

        if (Health <= 0.0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        var parent = GetParent();
        if (parent != null)
        {
            var deathFx = new CpuParticles2D
            {
                GlobalPosition = GlobalPosition,
                Emitting = true,
                OneShot = true,
                Explosiveness = 1.0f,
                Amount = 32,
                Lifetime = 0.6f,
                Spread = 180.0f,
                InitialVelocityMin = 40.0f,
                InitialVelocityMax = 160.0f,
                Gravity = Vector2.Zero,
                ScaleAmountMin = 3.0f,
                ScaleAmountMax = 8.0f,
                Color = new Color(0.7f, 0.08f, 0.08f, 0.9f)
            };
            deathFx.Finished += deathFx.QueueFree;
            parent.AddChild(deathFx);

            // Item drop chance
            if (_pickupScene != null)
            {
                float roll = (float)GD.RandRange(0.0, 1.0);
                if (roll < 0.24f) // 24% Ammo drop
                {
                    var pickup = _pickupScene.Instantiate<Pickup>();
                    pickup.GlobalPosition = GlobalPosition;
                    pickup.Setup(Pickup.PickupType.Ammo, 25.0f);
                    parent.CallDeferred("add_child", pickup);
                }
                else if (roll < 0.35f) // 11% Medkit drop
                {
                    var pickup = _pickupScene.Instantiate<Pickup>();
                    pickup.GlobalPosition = GlobalPosition;
                    pickup.Setup(Pickup.PickupType.Health, 35.0f);
                    parent.CallDeferred("add_child", pickup);
                }
            }
        }

        var world = GetTree().CurrentScene;
        if (world != null && world.HasMethod("OnZombieKilled"))
        {
            world.Call("OnZombieKilled");
        }

        QueueFree();
    }
}
