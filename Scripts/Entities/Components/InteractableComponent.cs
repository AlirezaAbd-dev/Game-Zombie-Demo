using Godot;
using TopDownDemo.Entities;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities.Components;

[GlobalClass]
public partial class InteractableComponent : Area2D
{
    [Signal]
    public delegate void InteractedEventHandler(Player player);

    [Signal]
    public delegate void FocusChangedEventHandler(bool isFocused, Player player);

    [Export] public string PromptText { get; set; } = "INTERACT";
    [Export] public string RequiredItemId { get; set; } = string.Empty;
    [Export] public string LockedPromptText { get; set; } = "LOCKED";
    [Export] public bool ConsumeItemOnUse { get; set; } = false;
    [Export] public bool IsActive { get; set; } = true;
    [Export] public bool OneShot { get; set; } = false;

    public bool HasInteracted { get; private set; } = false;

    public override void _Ready()
    {
        AddToGroup("interactables");
        CollisionLayer = 8; // Layer 4 (Interactables)
        CollisionMask = 1 | 8; // Layer 1 (Player) + Layer 4
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            player.InteractionComponent?.RegisterInteractable(this);
            // Auto-unlock blast doors if player approaches with required key
            if (GetParent() is BlastDoor door && !door.IsOpen && HasRequiredItem(player))
            {
                door.OpenDoor();
            }
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Player player)
        {
            player.InteractionComponent?.UnregisterInteractable(this);
        }
    }

    public bool HasRequiredItem(Player player)
    {
        if (string.IsNullOrWhiteSpace(RequiredItemId)) return true;
        return player?.InventoryComponent?.HasItem(RequiredItemId) ?? false;
    }

    public bool CanInteract(Player player)
    {
        if (!IsActive) return false;
        if (OneShot && HasInteracted) return false;
        return true;
    }

    public string GetPromptText(Player player)
    {
        if (!IsActive) return string.Empty;
        if (OneShot && HasInteracted) return string.Empty;

        if (!string.IsNullOrWhiteSpace(RequiredItemId))
        {
            if (!HasRequiredItem(player))
            {
                return string.IsNullOrWhiteSpace(LockedPromptText) ? "LOCKED - KEYCARD REQUIRED" : LockedPromptText;
            }
            return !string.IsNullOrWhiteSpace(PromptText) ? PromptText : "UNLOCK & OPEN";
        }

        return PromptText;
    }

    public bool TryInteract(Player player)
    {
        if (!CanInteract(player)) return false;

        if (!string.IsNullOrWhiteSpace(RequiredItemId))
        {
            if (!HasRequiredItem(player))
            {
                AudioManager.Instance?.PlaySound(SoundType.TerminalData, GlobalPosition);
                return false;
            }

            if (ConsumeItemOnUse)
            {
                player.InventoryComponent?.ConsumeItem(RequiredItemId, 1);
            }
        }

        HasInteracted = true;
        if (OneShot)
        {
            IsActive = false;
        }

        EmitSignal(SignalName.Interacted, player);
        return true;
    }

    public void NotifyFocus(bool isFocused, Player player)
    {
        EmitSignal(SignalName.FocusChanged, isFocused, player);
    }
}
