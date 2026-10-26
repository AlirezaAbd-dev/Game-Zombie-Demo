using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;
using TopDownDemo.UI;

namespace TopDownDemo.Entities;

public partial class Pickup : Area2D
{
    [Signal] public delegate void CollectedEventHandler();
    [Signal] public delegate void AmmoRemainingChangedEventHandler(int remaining);
    private bool _collected;
    private int _ammoRoundsRemaining = -1;
    private Label _ammoLabel;
    private readonly PhysicsRayQueryParameters2D _collectionRay = new() { CollisionMask = 1, HitFromInside = true };
    private readonly Godot.Collections.Array<Rid> _collectionExclusions = new();
    public int RemainingAmmoRounds => _ammoRoundsRemaining >= 0 ? _ammoRoundsRemaining
        : Item != null && Item.Type == ItemType.Ammo && float.IsFinite(Item.Value) && Item.Value > 0 && Item.Value < int.MaxValue
            ? (int)Item.Value : 0;
    [Export] public ItemData Item { get; set; }
    [Export] public string StoryFlagOnCollect { get; set; } = string.Empty;

    [ExportGroup("Nodes")]
    [Export] public Sprite2D Sprite { get; set; }
    [Export] public PointLight2D GlowLight { get; set; }
    [Export] public CollisionShape2D PickupCollision { get; set; }
    private Label _itemLabel;
    private Player _collectionRequest;

    private float _bobTimer = 0.0f;

    public override void _Ready()
    {
        AddToGroup("pickups");
        _bobTimer = (float)GD.RandRange(0.0, Mathf.Tau);

        Sprite ??= GetNodeOrNull<Sprite2D>("%Sprite2D");
        GlowLight ??= GetNodeOrNull<PointLight2D>("%GlowLight");
        PickupCollision ??= GetNodeOrNull<CollisionShape2D>("%PickupCollision");

        UpdateVisuals();
    }

    public void Setup(ItemData item)
    {
        Item = item;
        _ammoRoundsRemaining = -1;
        UpdateVisuals();
    }

    public void RestoreAmmoRounds(int remaining)
    {
        _ammoRoundsRemaining = Mathf.Max(0,remaining);
        if (Sprite != null && IsInsideTree()) UpdateAmmoLabel();
    }

    private void UpdateAmmoLabel()
    {
        if (Item?.Type != ItemType.Ammo || _ammoRoundsRemaining < 0) return;
        if (_ammoLabel == null)
        {
            _ammoLabel = new Label { Text = "", Position = new Vector2(-100,-38), Size = new Vector2(200,24),
                HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_ammoLabel);
        }
        _ammoLabel.Text = $"{RemainingAmmoRounds} ROUNDS LEFT";
    }

    public void UpdateVisuals()
    {
        if (Item == null || Sprite == null) return;
        UpdateAmmoLabel();

        if (Item.Icon != null)
        {
            Sprite.Texture = Item.Icon;
            float size = float.IsFinite(Item.WorldIconSize) ? Mathf.Clamp(Item.WorldIconSize,12,48) : 32;
            int pixels = Mathf.Max(Item.Icon.GetWidth(),Item.Icon.GetHeight());
            if (pixels > 0) Sprite.Scale = Vector2.One * (size / pixels);
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
            if (PickupCollision?.Shape is CircleShape2D circle)
            {
                circle.Radius = 80.0f;
            }

            if (_itemLabel == null)
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
                _itemLabel = label;
            }
        }
    }

    public override void _Process(double delta)
    {
        _bobTimer += (float)delta * 3.5f;
        float offset = Mathf.Sin(_bobTimer) * 1.0f;

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
        if (_collectionRequest != null)
        {
            var player = _collectionRequest;
            _collectionRequest = null;
            if (IsInstanceValid(player) && !player.IsDead && player.GlobalPosition.DistanceSquaredTo(GlobalPosition) <= 120 * 120)
            {
                if (!HasClearCollectionPath(player))
                    HUDUI.Instance?.ShowFloatingNotification("ITEM IS OUT OF REACH",Colors.Orange);
                else if (CollectByPlayer(player)) return;
                else HUDUI.Instance?.ShowFloatingNotification(Item.Type == ItemType.Ammo
                    ? "AMMUNITION RESERVE FULL" : "MAKE SPACE TO COLLECT THIS ITEM",Colors.Orange);
            }
        }
        var bodies = GetOverlappingBodies();
        for (int i = 0; i < bodies.Count; i++)
        {
            if (bodies[i] is Player player && IsInstanceValid(player) && !player.IsDead)
            {
                if (HasClearCollectionPath(player) && CollectByPlayer(player))
                {
                    return;
                }
            }
        }
    }

    public bool RequestCollection(Player player)
    {
        if (_collected || IsQueuedForDeletion() || !IsInstanceValid(player) || player.IsDead
            || Item == null || player.GlobalPosition.DistanceSquaredTo(GlobalPosition) > 120 * 120) return false;
        _collectionRequest = player;
        return true;
    }

    /// <summary>Physics-tick visibility shared by automatic collection, requests and prompts.</summary>
    public bool HasClearCollectionPath(Player player)
    {
        if (!IsInstanceValid(player) || player.IsDead) return false;
        var space = GetWorld2D()?.DirectSpaceState;
        if (space == null) return false;
        _collectionExclusions.Clear();
        _collectionExclusions.Add(player.GetRid());
        _collectionRay.Exclude = _collectionExclusions;
        _collectionRay.From = GlobalPosition;
        _collectionRay.To = player.GlobalPosition;
        return space.IntersectRay(_collectionRay).Count == 0;
    }

    public bool CollectByPlayer(Player player)
    {
        if (_collected || IsQueuedForDeletion() || player == null || player.IsDead || Item == null) return false;

        bool collected = false;
        bool finished = true;
        bool isHealingItem = Item.Type == ItemType.Health || Item.Type == ItemType.Herb;

        // Priority 1: Store in inventory if there is free space or stack available
        if (Item.Type == ItemType.Ammo)
        {
            int accepted = player.InventoryComponent?.AddReserveAmmo(Item.Id,RemainingAmmoRounds) ?? 0;
            if (accepted <= 0) return false;
            _ammoRoundsRemaining = RemainingAmmoRounds - accepted;
            EmitSignal(SignalName.AmmoRemainingChanged,_ammoRoundsRemaining);
            finished = _ammoRoundsRemaining == 0;
            collected = true;
            UpdateAmmoLabel();
            HUDUI.Instance?.ShowFloatingNotification($"+{accepted} ROUNDS: {Item.ItemName}",new Color(1,0.95f,0.3f));
        }
        else if (player.InventoryComponent != null && player.InventoryComponent.AddItem(Item))
        {
            collected = true;
            HUDUI.Instance?.ShowFloatingNotification($"+ ACQUIRED: {Item.ItemName}", new Color(1.0f, 0.95f, 0.3f, 1.0f));
        }
        // Priority 2: If inventory is full, but player has lost health and this item heals, consume on the spot
        else if (isHealingItem)
        {
            collected = InventoryComponent.TryApplyRestorative(Item,player);
        }

        if (collected)
        {
            if (finished)
            {
                _collected = true;
                if (!string.IsNullOrEmpty(StoryFlagOnCollect)) StoryDirector.Instance?.SetFlag(StoryFlagOnCollect);
                EmitSignal(SignalName.Collected);
            }
            try
            {
                AudioManager.Instance?.PlaySound(SoundType.PickupCollect, GlobalPosition);
                SpawnCollectEffect();

                if (Item.Type == ItemType.Key)
                {
                    if (Item.Id == "keycard_substation" && StoryDirector.Instance?.IsCampaign != true)
                    {
                        StoryDirector.Instance?.EmitSignal(
                            StoryDirector.SignalName.ObjectiveUpdated,
                            "MISSION 1: RESTORE SECTOR POWER",
                            $"Keycard acquired! Go East to the Substation Door and press [{ControlBindings.Hint("interact")}] to unlock."
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
                if (finished) QueueFree();
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
