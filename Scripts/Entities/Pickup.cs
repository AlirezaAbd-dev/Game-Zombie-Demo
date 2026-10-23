using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities;

public partial class Pickup : Area2D
{
    [Export] public ItemData Item { get; set; }

    [ExportGroup("Nodes")]
    [Export] public Sprite2D Sprite { get; set; }
    [Export] public PointLight2D GlowLight { get; set; }

    private float _bobTimer = 0.0f;

    public override void _Ready()
    {
        _bobTimer = (float)GD.RandRange(0.0, Mathf.Tau);

        Sprite ??= GetNodeOrNull<Sprite2D>("%Sprite2D");
        GlowLight ??= GetNodeOrNull<PointLight2D>("%GlowLight");

        UpdateVisuals();
        BodyEntered += OnBodyEntered;
    }

    public void Setup(ItemData item)
    {
        Item = item;
        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        if (Item == null || Sprite == null) return;

        if (Item.Icon != null)
        {
            Sprite.Texture = Item.Icon;
        }

        if (GlowLight != null)
        {
            if (Item.Type == ItemType.Key)
            {
                GlowLight.Color = new Color(0.2f, 0.95f, 1.0f, 1.0f);
                GlowLight.TextureScale = 1.8f;
                GlowLight.Energy = 1.8f;
            }
            else if (Item.Type == ItemType.Health)
            {
                GlowLight.Color = new Color(0.2f, 1.0f, 0.4f, 1.0f);
                GlowLight.TextureScale = 1.0f;
            }
            else
            {
                GlowLight.Color = new Color(1.0f, 0.85f, 0.2f, 1.0f);
                GlowLight.TextureScale = 1.0f;
            }
        }

        if (Item.Type == ItemType.Key)
        {
            var col = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (col != null && col.Shape is CircleShape2D circle)
            {
                circle.Radius = 55.0f;
            }

            if (GetNodeOrNull<Label>("KeycardLabel") == null)
            {
                var label = new Label
                {
                    Name = "KeycardLabel",
                    Text = "SUBSTATION KEYCARD",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Position = new Vector2(-100, -38),
                    Size = new Vector2(200, 20)
                };
                label.Modulate = new Color(0.3f, 1.0f, 1.0f, 0.95f);
                AddChild(label);
            }
        }
    }

    public override void _Process(double delta)
    {
        _bobTimer += (float)delta * 3.5f;
        float offset = Mathf.Sin(_bobTimer) * 4.0f;

        if (Sprite != null)
        {
            Sprite.Position = new Vector2(0, offset);
        }

        if (GlowLight != null)
        {
            GlowLight.Energy = 0.75f + Mathf.Sin(_bobTimer * 1.5f) * 0.25f;
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player && !player.IsDead && Item != null)
        {
            bool collected = false;
            if (Item.Type == ItemType.Health && player.HealthComponent != null)
            {
                if (player.HealthComponent.CurrentHealth < player.HealthComponent.MaxHealth)
                {
                    player.Heal(Item.Value);
                    collected = true;
                }
            }
            else if (Item.Type == ItemType.Ammo && player.InventoryComponent != null)
            {
                player.InventoryComponent.AddItem(Item);
                collected = true;
            }
            else if ((Item.Type == ItemType.Key || Item.Type == ItemType.Document) && player.InventoryComponent != null)
            {
                player.InventoryComponent.AddItem(Item);
                collected = true;
            }

            if (collected)
            {
                AudioManager.Instance?.PlaySound(SoundType.PickupCollect, GlobalPosition);
                SpawnCollectEffect();
                QueueFree();
            }
        }
    }

    private void SpawnCollectEffect()
    {
        var parent = GetParent();
        if (parent == null) return;

        Color particleColor = Item?.Type == ItemType.Health ? new Color(0.2f, 1.0f, 0.4f, 0.9f) : new Color(1.0f, 0.9f, 0.3f, 0.9f);

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
            Color = particleColor
        };
        particles.Finished += particles.QueueFree;
        parent.AddChild(particles);
    }
}
