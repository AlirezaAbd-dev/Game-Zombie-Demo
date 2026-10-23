using System.Collections.Generic;
using Godot;

namespace TopDownDemo.Entities.Components;

[GlobalClass]
public partial class PlayerInteractionComponent : Area2D
{
    [Signal]
    public delegate void FocusedInteractableChangedEventHandler(string promptText, bool canInteract);

    [Export] public float InteractionRange { get; set; } = 90.0f;

    private readonly List<InteractableComponent> _nearbyInteractables = new();
    private InteractableComponent _currentFocus = null;
    private Player _player;

    public InteractableComponent CurrentFocus => _currentFocus;

    public override void _Ready()
    {
        _player = GetParent<Player>();
        CollisionLayer = 0;
        CollisionMask = 2147483647; // Detect all area layers
        AreaEntered += OnAreaEntered;
        AreaExited += OnAreaExited;
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
        // Clean dead / inactive nodes
        _nearbyInteractables.RemoveAll(i => !IsInstanceValid(i) || !i.IsInsideTree());

        InteractableComponent bestCandidate = null;
        float minDistanceSq = float.MaxValue;
        Vector2 playerPos = GlobalPosition;

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
    }

    public bool TryTriggerInteraction()
    {
        if (_currentFocus == null || !IsInstanceValid(_currentFocus))
            return false;

        return _currentFocus.TryInteract(_player);
    }
}
