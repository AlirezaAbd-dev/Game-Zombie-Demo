using System.Collections.Generic;
using Godot;

namespace TopDownDemo.Entities.Components;

[GlobalClass]
public partial class PlayerInteractionComponent : Area2D
{
    [Signal]
    public delegate void FocusedInteractableChangedEventHandler(string promptText, bool canInteract);

    [Export] public float InteractionRange { get; set; } = 130.0f;

    private readonly List<InteractableComponent> _nearbyInteractables = new();
    private InteractableComponent _currentFocus = null;
    private Player _player;

    public InteractableComponent CurrentFocus => _currentFocus;

    public override void _Ready()
    {
        _player = GetParent<Player>();
        CollisionLayer = 8; // Layer 4
        CollisionMask = 8 | 1; // Detect Layer 4 and Layer 1
        AreaEntered += OnAreaEntered;
        AreaExited += OnAreaExited;
    }

    public void RegisterInteractable(InteractableComponent interactable)
    {
        if (interactable != null && !_nearbyInteractables.Contains(interactable))
        {
            _nearbyInteractables.Add(interactable);
            UpdateNearestInteractable();
        }
    }

    public void UnregisterInteractable(InteractableComponent interactable)
    {
        if (interactable != null)
        {
            _nearbyInteractables.Remove(interactable);
            if (_currentFocus == interactable)
            {
                interactable.NotifyFocus(false, _player);
                _currentFocus = null;
                EmitSignal(SignalName.FocusedInteractableChanged, string.Empty, false);
            }
            UpdateNearestInteractable();
        }
    }

    public override void _Process(double delta)
    {
        UpdateNearestInteractable();
    }

    private void OnAreaEntered(Area2D area)
    {
        if (area is InteractableComponent interactable && !_nearbyInteractables.Contains(interactable))
        {
            _nearbyInteractables.Add(interactable);
            UpdateNearestInteractable();
        }
    }

    private void OnAreaExited(Area2D area)
    {
        if (area is InteractableComponent interactable && _nearbyInteractables.Contains(interactable))
        {
            _nearbyInteractables.Remove(interactable);
            if (_currentFocus == interactable)
            {
                interactable.NotifyFocus(false, _player);
                _currentFocus = null;
                EmitSignal(SignalName.FocusedInteractableChanged, string.Empty, false);
            }
            UpdateNearestInteractable();
        }
    }

    private void UpdateNearestInteractable()
    {
        if (_player == null) return;

        // Clean dead / inactive nodes
        _nearbyInteractables.RemoveAll(i => !IsInstanceValid(i) || !i.IsInsideTree());

        InteractableComponent bestCandidate = null;
        float minDistanceSq = InteractionRange * InteractionRange;
        Vector2 playerPos = _player.GlobalPosition;

        // 1. Check direct physics-tracked candidates
        for (int i = 0; i < _nearbyInteractables.Count; i++)
        {
            var candidate = _nearbyInteractables[i];
            if (!candidate.CanInteract(_player))
                continue;

            float distSq = playerPos.DistanceSquaredTo(candidate.GlobalPosition);
            if (distSq < minDistanceSq)
            {
                minDistanceSq = distSq;
                bestCandidate = candidate;
            }
        }

        // 2. Fallback scan of group 'interactables' in case physics signals had a 1-frame latency
        var treeInteractables = GetTree()?.GetNodesInGroup("interactables");
        if (treeInteractables != null)
        {
            for (int i = 0; i < treeInteractables.Count; i++)
            {
                if (treeInteractables[i] is InteractableComponent candidate && candidate.CanInteract(_player))
                {
                    float distSq = playerPos.DistanceSquaredTo(candidate.GlobalPosition);
                    if (distSq < minDistanceSq)
                    {
                        minDistanceSq = distSq;
                        bestCandidate = candidate;
                    }
                }
            }
        }

        // 3. If no door/terminal interactable is in range, check for nearby pickups
        Pickup bestPickup = null;
        if (bestCandidate == null)
        {
            var pickups = GetTree()?.GetNodesInGroup("pickups");
            if (pickups != null)
            {
                float minPickupDistSq = 110.0f * 110.0f;
                for (int i = 0; i < pickups.Count; i++)
                {
                    if (pickups[i] is Pickup p && IsInstanceValid(p) && p.Item != null)
                    {
                        float distSq = playerPos.DistanceSquaredTo(p.GlobalPosition);
                        if (distSq < minPickupDistSq)
                        {
                            minPickupDistSq = distSq;
                            bestPickup = p;
                        }
                    }
                }
            }
        }

        if (bestCandidate != _currentFocus)
        {
            if (_currentFocus != null && IsInstanceValid(_currentFocus))
            {
                _currentFocus.NotifyFocus(false, _player);
            }

            _currentFocus = bestCandidate;

            if (_currentFocus != null)
            {
                _currentFocus.NotifyFocus(true, _player);
                string prompt = _currentFocus.GetPromptText(_player);
                bool canUse = _currentFocus.HasRequiredItem(_player);
                EmitSignal(SignalName.FocusedInteractableChanged, prompt, canUse);
            }
            else if (bestPickup != null)
            {
                string itemName = !string.IsNullOrEmpty(bestPickup.Item?.ItemName) ? bestPickup.Item.ItemName : "ITEM";
                EmitSignal(SignalName.FocusedInteractableChanged, $"TAKE {itemName}", true);
            }
            else
            {
                EmitSignal(SignalName.FocusedInteractableChanged, string.Empty, false);
            }
        }
        else if (_currentFocus != null)
        {
            // Update prompt dynamically (e.g., if player just collected the keycard)
            string prompt = _currentFocus.GetPromptText(_player);
            bool canUse = _currentFocus.HasRequiredItem(_player);
            EmitSignal(SignalName.FocusedInteractableChanged, prompt, canUse);
        }
        else if (bestPickup != null)
        {
            string itemName = !string.IsNullOrEmpty(bestPickup.Item?.ItemName) ? bestPickup.Item.ItemName : "ITEM";
            EmitSignal(SignalName.FocusedInteractableChanged, $"TAKE {itemName}", true);
        }
        else
        {
            EmitSignal(SignalName.FocusedInteractableChanged, string.Empty, false);
        }
    }

    public bool TryTriggerInteraction()
    {
        if (_player == null) return false;

        // 1. Direct focus interact
        if (_currentFocus != null && IsInstanceValid(_currentFocus))
        {
            return _currentFocus.TryInteract(_player);
        }

        // 2. Proximity check for nearby pickups
        var pickups = GetTree()?.GetNodesInGroup("pickups");
        if (pickups != null)
        {
            Pickup closest = null;
            float closestDist = 120.0f;
            foreach (var node in pickups)
            {
                if (node is Pickup pickup && IsInstanceValid(pickup))
                {
                    float dist = _player.GlobalPosition.DistanceTo(pickup.GlobalPosition);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = pickup;
                    }
                }
            }

            if (closest != null)
            {
                bool success = closest.CollectByPlayer(_player);
                UpdateNearestInteractable();
                return success;
            }
        }

        // 3. Proximity fallback interact with nearest interactable
        UpdateNearestInteractable();
        if (_currentFocus != null && IsInstanceValid(_currentFocus))
        {
            return _currentFocus.TryInteract(_player);
        }

        return false;
    }
}
