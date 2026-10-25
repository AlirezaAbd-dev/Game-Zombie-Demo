using System;
using Godot;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;
using TopDownDemo.UI;

namespace TopDownDemo.Entities;

public partial class RiddleDoor : StaticBody2D
{
    [Signal]
    public delegate void DoorOpenedEventHandler();

    [Export] public string DoorId { get; set; } = "door_ancient_vault";
    [Export] public bool IsOpen { get; set; } = false;
    [Export] public Vector2 SlideOffset { get; set; } = new Vector2(0.0f, -180.0f);
    [Export] public Vector2 LockFocusOffset { get; set; } = Vector2.Zero;

    [ExportGroup("Nodes")]
    [Export] public Sprite2D DoorSprite { get; set; }
    [Export] public Sprite2D LockSprite { get; set; }
    [Export] public CollisionShape2D CollisionShape { get; set; }
    [Export] public LightOccluder2D LightOccluder { get; set; }
    [Export] public PointLight2D StatusLight { get; set; }
    [Export] public InteractableComponent Interactable { get; set; }

    private Vector2 _closedPosition;

    public override void _Ready()
    {
        AddToGroup("doors");
        DoorSprite ??= GetNodeOrNull<Sprite2D>("%DoorSprite");
        LockSprite ??= GetNodeOrNull<Sprite2D>("%LockSprite");
        CollisionShape ??= GetNodeOrNull<CollisionShape2D>("%CollisionShape2D");
        LightOccluder ??= GetNodeOrNull<LightOccluder2D>("%LightOccluder2D");
        StatusLight ??= GetNodeOrNull<PointLight2D>("%StatusLight");
        Interactable ??= GetNodeOrNull<InteractableComponent>("%InteractableComponent");

        if (DoorSprite != null)
        {
            _closedPosition = DoorSprite.Position;
        }

        if (Interactable != null)
        {
            Interactable.PromptText = "EXAMINE COMBINATION LOCK";
            Interactable.LockedPromptText = "EXAMINE COMBINATION LOCK";
            Interactable.RequiredItemId = string.Empty;
            Interactable.Interacted += OnInteracted;
        }

        UpdateVisualState(false);
    }

    private void OnInteracted(Player player)
    {
        if (IsOpen) return;
        PadlockPuzzleUI.Instance?.Open(this, player);
    }

    public void OnPuzzleSolved()
    {
        if (IsOpen) return;
        IsOpen = true;

        if (CollisionShape != null)
        {
            CollisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        }

        if (LightOccluder != null)
        {
            LightOccluder.Visible = false;
        }

        if (StatusLight != null)
        {
            StatusLight.Color = new Color(0.2f, 1.0f, 0.4f, 1.0f);
        }

        if (Interactable != null)
        {
            Interactable.IsActive = false;
        }

        // Lock shackle pop & fade effect
        if (LockSprite != null)
        {
            var lockTween = CreateTween();
            lockTween.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
            lockTween.TweenProperty(LockSprite, "position", LockSprite.Position + new Vector2(0, 30), 0.45);
            lockTween.Parallel().TweenProperty(LockSprite, "modulate:a", 0.0f, 0.45);
        }

        // Heavy door sliding open
        if (DoorSprite != null)
        {
            var tween = CreateTween();
            tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            tween.TweenProperty(DoorSprite, "position", _closedPosition + SlideOffset, 0.95);
            tween.Parallel().TweenProperty(DoorSprite, "modulate:a", 0.5f, 0.95);
        }

        AudioManager.Instance?.PlaySound(SoundType.DoorOpen, GlobalPosition);

        StoryDirector.Instance?.TriggerRadioComms(
            "CMDR. REYES (HQ)",
            "Padlock disengaged! The sealed vault is open. Grab whatever supplies are stored within!",
            5.5f
        );

        StoryDirector.Instance?.EmitSignal(
            StoryDirector.SignalName.ObjectiveUpdated,
            "VAULT UNLOCKED",
            "Collect the Sanctuary Armory Key and medical cache inside."
        );

        EmitSignal(SignalName.DoorOpened);
    }

    private void UpdateVisualState(bool animate)
    {
        if (StatusLight != null)
        {
            StatusLight.Color = IsOpen ? new Color(0.2f, 1.0f, 0.4f, 1.0f) : new Color(1.0f, 0.75f, 0.2f, 1.0f);
        }

        if (IsOpen)
        {
            if (CollisionShape != null) CollisionShape.Disabled = true;
            if (LightOccluder != null) LightOccluder.Visible = false;
            if (DoorSprite != null) DoorSprite.Position = _closedPosition + SlideOffset;
            if (LockSprite != null) LockSprite.Visible = false;
        }
    }
}
