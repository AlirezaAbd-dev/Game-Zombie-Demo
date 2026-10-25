using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities;

public partial class NPC : CharacterBody2D
{
    [Export] public string CharacterName { get; set; } = "Dr. Sadeghi";
    [Export] public string PromptText { get; set; } = "TALK";
    [Export] public DialogueSequence DefaultDialogue { get; set; }
    [Export] public Texture2D SpriteTexture { get; set; }
    [Export] public Color CharacterColor { get; set; } = new Color(0.95f, 0.75f, 0.3f);

    private Sprite2D _sprite;
    private InteractableComponent _interactable;
    private float _idleAnimPhase = 0.0f;
    private Vector2 _baseScale = new(3.0f, 3.0f);

    public override void _Ready()
    {
        AddToGroup("npcs");

        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite == null)
        {
            _sprite = new Sprite2D
            {
                Name = "Sprite2D",
                TextureFilter = TextureFilterEnum.Nearest,
                Scale = _baseScale
            };
            AddChild(_sprite);
        }

        if (SpriteTexture != null)
        {
            _sprite.Texture = SpriteTexture;
        }

        _interactable = GetNodeOrNull<InteractableComponent>("InteractableComponent");
        if (_interactable == null)
        {
            _interactable = new InteractableComponent
            {
                Name = "InteractableComponent",
                PromptText = $"{PromptText} [{CharacterName}]"
            };
            var col = new CollisionShape2D
            {
                Shape = new CircleShape2D { Radius = 85.0f }
            };
            _interactable.AddChild(col);
            AddChild(_interactable);
        }

        _interactable.Interacted += OnInteracted;
        _idleAnimPhase = (float)GD.RandRange(0.0, Mathf.Tau);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _idleAnimPhase += dt * 2.5f;

        // Subtle breathing animation
        if (_sprite != null)
        {
            float scaleY = 1.0f + Mathf.Sin(_idleAnimPhase) * 0.035f;
            float scaleX = 1.0f - Mathf.Sin(_idleAnimPhase) * 0.02f;
            _sprite.Scale = new Vector2(_baseScale.X * scaleX, _baseScale.Y * scaleY);
        }

        // Face towards player when player is nearby
        Player player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null && GodotObject.IsInstanceValid(player))
        {
            float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
            if (dist < 180.0f)
            {
                Vector2 dir = (player.GlobalPosition - GlobalPosition).Normalized();
                Rotation = Mathf.LerpAngle(Rotation, dir.Angle(), dt * 6.0f);
            }
        }
    }

    protected virtual void OnInteracted(Player player)
    {
        if (DefaultDialogue != null && DialogueManager.Instance != null && !DialogueManager.Instance.IsDialogueActive)
        {
            DialogueManager.Instance.StartDialogue(DefaultDialogue);
        }
    }
}
