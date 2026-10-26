using Godot;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities;

public partial class BlastDoor : StaticBody2D
{
    [Signal]
    public delegate void DoorOpenedEventHandler();

    [Export] public string DoorId { get; set; } = "blast_door";
    [Export] public string RequiredItemId { get; set; } = string.Empty;
    [Export] public string RequiredStoryFlag { get; set; } = string.Empty;
    [Export] public string LockedPromptText { get; set; } = string.Empty;
    [Export] public bool IsOpen { get; set; } = false;
    [Export] public bool OpenOnPowerRestored { get; set; } = false;
    [Export] public bool OpenOnEvacPhase { get; set; } = false;
    [Export] public Vector2 SlideOffset { get; set; } = new Vector2(90.0f, 0.0f);

    [ExportGroup("Nodes")]
    [Export] public Sprite2D DoorSprite { get; set; }
    [Export] public CollisionShape2D CollisionShape { get; set; }
    [Export] public LightOccluder2D LightOccluder { get; set; }
    [Export] public PointLight2D StatusLight { get; set; }
    [Export] public InteractableComponent Interactable { get; set; }

    private Vector2 _closedPosition;
    private Tween _motionTween;

    public override void _Ready()
    {
        AddToGroup("doors");
        DoorSprite ??= GetNodeOrNull<Sprite2D>("%DoorSprite");
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
            Interactable.RequiredStoryFlag = RequiredStoryFlag;
            if (!string.IsNullOrEmpty(RequiredItemId))
            {
                Interactable.RequiredItemId = RequiredItemId;
                Interactable.PromptText = "UNLOCK & OPEN BLAST DOOR";
            }
            if (!string.IsNullOrEmpty(LockedPromptText))
            {
                Interactable.LockedPromptText = LockedPromptText;
            }
            Interactable.Interacted += OnInteracted;
        }

        if (StoryDirector.Instance != null)
        {
            if (OpenOnPowerRestored)
            {
                StoryDirector.Instance.FacilityPowerRestored += OpenDoor;
            }

            if (OpenOnEvacPhase)
            {
                StoryDirector.Instance.PhaseChanged += (phaseVal) =>
                {
                    if ((TopDownDemo.Core.StoryPhase)phaseVal == TopDownDemo.Core.StoryPhase.Act3_HelipadEvacuation)
                    {
                        OpenDoor();
                    }
                };
            }
        }

        UpdateVisualState(false);
    }

    private void OnInteracted(Player player)
    {
        if (!IsOpen)
        {
            OpenDoor();
        }
    }

    public void OpenDoor()
    {
        if (IsOpen) return;
        if (!string.IsNullOrEmpty(RequiredStoryFlag) && StoryDirector.Instance?.HasFlag(RequiredStoryFlag) != true) return;
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
            StatusLight.Color = new Color(0.2f, 1.0f, 0.35f, 1.0f);
        }

        if (Interactable != null)
        {
            Interactable.IsActive = false;
        }

        if (DoorSprite != null)
        {
            _motionTween?.Kill();
            var tween = _motionTween = CreateTween();
            tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            tween.TweenProperty(DoorSprite, "position", _closedPosition + SlideOffset, 0.85);
            tween.Parallel().TweenProperty(DoorSprite, "modulate:a", 0.45f, 0.85);
        }

        AudioManager.Instance?.PlaySound(SoundType.DoorOpen, GlobalPosition);

        if (StoryDirector.Instance?.IsCampaign != true && (DoorId == "door_substation" || RequiredItemId == "keycard_substation"))
        {
            StoryDirector.Instance?.EmitSignal(
                StoryDirector.SignalName.ObjectiveUpdated,
                "MISSION 1: RESTORE SECTOR POWER",
                "Substation unlocked! Locate and activate the power generator."
            );
        }

        EmitSignal(SignalName.DoorOpened);
    }

    public void UnlockAndOpen()
    {
        if (Interactable != null)
        {
            Interactable.RequiredItemId = string.Empty;
        }
        OpenDoor();
    }

    public void RestoreOpen()
    {
        _motionTween?.Kill();
        IsOpen = true;
        if (Interactable != null) Interactable.IsActive = false;
        UpdateVisualState(false);
    }

    public bool CanCloseSafely()
    {
        if (!IsOpen || CollisionShape?.Shape == null) return true;
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = CollisionShape.Shape, Transform = CollisionShape.GlobalTransform,
            CollisionMask = 3, Exclude = new Godot.Collections.Array<Rid> { GetRid() }
        };
        foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 32))
            if (hit["collider"].AsGodotObject() is Player or Zombie or Boss_Unit9) return false;
        return true;
    }

    public void RestoreClosed()
    {
        _motionTween?.Kill();
        IsOpen = false;
        if (CollisionShape != null) CollisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
        if (LightOccluder != null) LightOccluder.Visible = true;
        if (StatusLight != null) StatusLight.Color = Colors.OrangeRed;
        if (Interactable != null) Interactable.IsActive = false;
        if (DoorSprite != null)
        {
            DoorSprite.Position = _closedPosition;
            DoorSprite.Modulate = new Color(DoorSprite.Modulate, 1);
        }
    }

    private void UpdateVisualState(bool animate)
    {
        if (StatusLight != null)
        {
            StatusLight.Color = IsOpen ? new Color(0.2f, 1.0f, 0.35f, 1.0f) : new Color(1.0f, 0.2f, 0.2f, 1.0f);
        }

        if (IsOpen)
        {
            if (CollisionShape != null) CollisionShape.Disabled = true;
            if (LightOccluder != null) LightOccluder.Visible = false;
            if (DoorSprite != null) DoorSprite.Position = _closedPosition + SlideOffset;
        }
    }
}
