using Godot;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities;

public partial class PropLocker : StaticBody2D
{
    [Export] public string PromptText { get; set; } = "HIDE IN LOCKER";

    private InteractableComponent _interactable;
    private bool _isPlayerInside = false;
    private Player _hiddenPlayer = null;

    public override void _Ready()
    {
        _interactable = GetNodeOrNull<InteractableComponent>("InteractableComponent");
        if (_interactable == null)
        {
            _interactable = new InteractableComponent
            {
                Name = "InteractableComponent",
                PromptText = PromptText
            };
            var colShape = new CollisionShape2D
            {
                Shape = new CircleShape2D { Radius = 75.0f }
            };
            _interactable.AddChild(colShape);
            AddChild(_interactable);
        }

        _interactable.Interacted += OnInteracted;
    }

    private void OnInteracted(Player player)
    {
        if (player == null) return;

        if (!_isPlayerInside)
        {
            // Enter locker
            _isPlayerInside = true;
            _hiddenPlayer = player;
            player.IsHiddenInLocker = true;
            player.Visible = false;
            player.CollisionLayer = 0;
            player.CollisionMask = 0;
            player.GlobalPosition = GlobalPosition + new Vector2(0, 10);
            _interactable.PromptText = "EXIT LOCKER";
            AudioManager.Instance?.PlaySound(SoundType.DoorOpen, GlobalPosition);
        }
        else
        {
            // Exit locker
            ExitLocker();
        }
    }

    public void ExitLocker()
    {
        if (!_isPlayerInside || _hiddenPlayer == null) return;

        _isPlayerInside = false;
        _hiddenPlayer.IsHiddenInLocker = false;
        _hiddenPlayer.Visible = true;
        _hiddenPlayer.CollisionLayer = 1;
        _hiddenPlayer.CollisionMask = 1 | 2;
        _hiddenPlayer.GlobalPosition = GlobalPosition + new Vector2(0, 45);
        _interactable.PromptText = PromptText;
        AudioManager.Instance?.PlaySound(SoundType.DoorOpen, GlobalPosition);
        _hiddenPlayer = null;
    }

    public override void _Process(double delta)
    {
        if (_isPlayerInside && _hiddenPlayer != null)
        {
            Vector2 input = _hiddenPlayer.GetMovementInput();
            if (input.LengthSquared() > 0.05f)
            {
                ExitLocker();
            }
        }
    }
}
