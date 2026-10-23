using Godot;
using TopDownDemo.Entities;

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
        CollisionLayer = 0;
        CollisionMask = 1; // Detects Player (Layer 1)
    }

    public bool HasRequiredItem(Player player)
    {
        if (string.IsNullOrEmpty(RequiredItemId)) return true;
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

        if (!string.IsNullOrEmpty(RequiredItemId) && !HasRequiredItem(player))
        {
            return string.IsNullOrEmpty(LockedPromptText) ? "LOCKED" : LockedPromptText;
        }

        return PromptText;
    }

    public bool TryInteract(Player player)
    {
        if (!CanInteract(player)) return false;

        if (!string.IsNullOrEmpty(RequiredItemId))
        {
            if (!HasRequiredItem(player))
            {
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
