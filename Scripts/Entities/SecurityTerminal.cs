using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities;

public partial class SecurityTerminal : StaticBody2D
{
    [Signal]
    public delegate void TerminalAccessedEventHandler();

    [Export] public ItemData ResearchDriveItem { get; set; }
    [Export] public bool IsDownloaded { get; private set; } = false;

    [ExportGroup("Nodes")]
    [Export] public Sprite2D ScreenSprite { get; set; }
    [Export] public PointLight2D ScreenLight { get; set; }
    [Export] public InteractableComponent Interactable { get; set; }

    public override void _Ready()
    {
        ScreenSprite ??= GetNodeOrNull<Sprite2D>("%ScreenSprite");
        ScreenLight ??= GetNodeOrNull<PointLight2D>("%ScreenLight");
        Interactable ??= GetNodeOrNull<InteractableComponent>("%InteractableComponent");
        ResearchDriveItem ??= GD.Load<ItemData>("res://Resources/Items/Research_Drive.tres");

        if (Interactable != null)
        {
            Interactable.Interacted += OnInteracted;
        }
    }

    private void OnInteracted(Player player)
    {
        if (IsDownloaded) return;
        IsDownloaded = true;

        if (Interactable != null)
        {
            Interactable.IsActive = false;
        }

        if (ScreenLight != null)
        {
            ScreenLight.Color = new Color(0.2f, 1.0f, 0.4f, 1.0f);
            ScreenLight.Energy = 1.6f;
        }

        if (player?.InventoryComponent != null && ResearchDriveItem != null)
        {
            player.InventoryComponent.AddItem(ResearchDriveItem);
        }

        AudioManager.Instance?.PlaySound(SoundType.TerminalData, GlobalPosition);
        StoryDirector.Instance?.OnTerminalAccessed();
        EmitSignal(SignalName.TerminalAccessed);
    }
}
