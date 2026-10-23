using Godot;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities;

public partial class PowerGenerator : StaticBody2D
{
    [Signal]
    public delegate void GeneratorActivatedEventHandler();

    [Export] public bool IsOnline { get; private set; } = false;

    [ExportGroup("Nodes")]
    [Export] public Sprite2D GeneratorSprite { get; set; }
    [Export] public PointLight2D CoreLight { get; set; }
    [Export] public InteractableComponent Interactable { get; set; }
    [Export] public CpuParticles2D SparkParticles { get; set; }

    public override void _Ready()
    {
        GeneratorSprite ??= GetNodeOrNull<Sprite2D>("%GeneratorSprite");
        CoreLight ??= GetNodeOrNull<PointLight2D>("%CoreLight");
        Interactable ??= GetNodeOrNull<InteractableComponent>("%InteractableComponent");
        SparkParticles ??= GetNodeOrNull<CpuParticles2D>("%SparkParticles");

        if (Interactable != null)
        {
            Interactable.Interacted += OnInteracted;
        }

        if (CoreLight != null)
        {
            CoreLight.Energy = 0.2f;
            CoreLight.Color = new Color(0.2f, 0.4f, 0.8f, 1.0f);
        }
    }

    private void OnInteracted(Player player)
    {
        if (IsOnline) return;
        ActivateGenerator();
    }

    public void ActivateGenerator()
    {
        if (IsOnline) return;
        IsOnline = true;

        if (Interactable != null)
        {
            Interactable.IsActive = false;
        }

        if (SparkParticles != null)
        {
            SparkParticles.Emitting = true;
        }

        if (CoreLight != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(CoreLight, "energy", 2.2f, 0.4);
            tween.TweenProperty(CoreLight, "color", new Color(0.1f, 0.9f, 1.0f, 1.0f), 0.3);
            tween.TweenProperty(CoreLight, "energy", 1.5f, 0.5);
        }

        AudioManager.Instance?.PlaySound(SoundType.GeneratorStart, GlobalPosition);
        StoryDirector.Instance?.OnGeneratorActivated();
        EmitSignal(SignalName.GeneratorActivated);
    }
}
