using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;
using TopDownDemo.UI;

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
        AddToGroup("pickups");
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
            else if (Item.Type == ItemType.Health || Item.Type == ItemType.Herb)
            {
                if (Item.Id == "trauma_kit")
                {
                    GlowLight.Color = new Color(0.3f, 1.0f, 0.85f, 1.0f);
                    GlowLight.TextureScale = 1.4f;
                }
                else if (Item.Id == "herb_blue")
                {
                    GlowLight.Color = new Color(0.2f, 0.5f, 1.0f, 1.0f);
                    GlowLight.TextureScale = 1.0f;
                }
                else if (Item.Id == "bandage")
                {
                    GlowLight.Color = new Color(0.85f, 1.0f, 0.4f, 1.0f);
                    GlowLight.TextureScale = 0.85f;
                }
                else
                {
                    GlowLight.Color = new Color(0.2f, 1.0f, 0.4f, 1.0f);
                    GlowLight.TextureScale = 1.1f;
                }
            }
            else
            {
                GlowLight.Color = new Color(1.0f, 0.85f, 0.2f, 1.0f);
                GlowLight.TextureScale = 1.0f;
            }
        }

        if (Item.Type == ItemType.Key || Item.Type == ItemType.Document)
        {
            var col = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (col != null && col.Shape is CircleShape2D circle)
            {
                circle.Radius = 80.0f;
            }

            if (GetNodeOrNull<Label>("ItemLabel") == null)
            {
                var label = new Label
                {
                    Name = "ItemLabel",
                    Text = Item.ItemName.ToUpper(),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Position = new Vector2(-120, -38),
                    Size = new Vector2(240, 20)
                };
                label.Modulate = Item.Type == ItemType.Key 
                    ? new Color(0.3f, 1.0f, 1.0f, 0.95f) 
                    : new Color(1.0f, 0.88f, 0.45f, 0.95f);
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

    public override void _PhysicsProcess(double delta)
    {
        var bodies = GetOverlappingBodies();
        for (int i = 0; i < bodies.Count; i++)
        {
            if (bodies[i] is Player player && IsInstanceValid(player) && !player.IsDead)
            {
                if (CollectByPlayer(player))
                {
                    return;
                }
            }
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            CollectByPlayer(player);
        }
    }

    public bool CollectByPlayer(Player player)
    {
        if (player == null || player.IsDead || Item == null) return false;

        bool collected = false;
        bool isHealingItem = Item.Type == ItemType.Health || Item.Type == ItemType.Herb;

        // Priority 1: Store in inventory if there is free space or stack available
        if (player.InventoryComponent != null && player.InventoryComponent.AddItem(Item))
        {
            collected = true;
            HUDUI.Instance?.ShowFloatingNotification($"+ ACQUIRED: {Item.ItemName}", new Color(1.0f, 0.95f, 0.3f, 1.0f));
        }
        // Priority 2: If inventory is full, but player has lost health and this item heals, consume on the spot
        else if (isHealingItem && player.HealthComponent != null && player.HealthComponent.CurrentHealth < player.HealthComponent.MaxHealth)
        {
            player.Heal(Item.Value);
            collected = true;
            HUDUI.Instance?.ShowHealNotification(Item.ItemName, Item.Value);
        }

        if (collected)
        {
            try
            {
                AudioManager.Instance?.PlaySound(SoundType.PickupCollect, GlobalPosition);
                SpawnCollectEffect();

                if (Item.Type == ItemType.Key)
                {
                    if (Item.Id == "keycard_substation")
                    {
                        StoryDirector.Instance?.EmitSignal(
                            StoryDirector.SignalName.ObjectiveUpdated,
                            "MISSION 1: RESTORE SECTOR POWER",
                            "Keycard acquired! Go East to the Substation Door and press [E] to unlock."
                        );
                        StoryDirector.Instance?.TriggerRadioComms(
                            "CMDR. REYES (HQ)",
                            "Keycard acquired. Head to the East blast door, unlock it, and get that generator running!",
                            5.0f
                        );
                    }
                    else if (Item.Id == "key_sanctuary")
                    {
                        StoryDirector.Instance?.TriggerRadioComms(
                            "CMDR. REYES (HQ)",
                            "Sanctuary Key retrieved! High-grade military armory munitions and supplies are secured.",
                            5.0f
                        );
                    }
                    else if (Item.Id == "biolab_keycard")
                    {
                        StoryDirector.Instance?.SetFlag(StoryFlags.BIOLAB_KEYCARD_FOUND);
                        HUDUI.Instance?.ShowHealNotification("BIOLAB KEYCARD", 0, "SECTOR 3 BLAST DOOR ACCESS GRANTED");
                    }
                }
                else if (Item.Id == "ghorbani_files")
                {
                    StoryDirector.Instance?.SetFlag(StoryFlags.GHORBANI_FILES_FOUND);
                    HUDUI.Instance?.ShowHealNotification("COMMANDER'S DOSSIER", 0, "CONSPIRACY EVIDENCE UNLOCKED");
                }
                else if (Item.Id == "archivist_journal")
                {
                    StoryDirector.Instance?.SetFlag(StoryFlags.ARCHIVIST_JOURNAL_COLLECTED);
                    HUDUI.Instance?.ShowHealNotification("ARCHIVIST'S CIPHER JOURNAL", 0, "SECRET LORE ACQUIRED");
                }
                else if (Item.Id == "riddle_parchment")
                {
                    StoryDirector.Instance?.TriggerRadioComms(
                        "CMDR. REYES (HQ)",
                        "Cipher parchment acquired: 'The harvest feeds the beast, and the beast protects the child.' Use that combination on the ancient vault lock!",
                        6.0f
                    );
                }
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[Pickup] Audio/Story notify notice: {ex.Message}");
            }
            finally
            {
                QueueFree();
            }
            return true;
        }

        return false;
    }

    private void SpawnCollectEffect()
    {
        var parent = GetParent();
        if (parent == null) return;

        Color particleColor = (Item?.Type == ItemType.Health || Item?.Type == ItemType.Herb)
            ? (Item?.Id == "trauma_kit" ? new Color(0.3f, 1.0f, 0.85f, 0.95f) : (Item?.Id == "herb_blue" ? new Color(0.2f, 0.6f, 1.0f, 0.95f) : new Color(0.2f, 1.0f, 0.4f, 0.9f)))
            : new Color(1.0f, 0.9f, 0.3f, 0.9f);

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
