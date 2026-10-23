using Godot;

namespace TopDownDemo.UI;

public partial class PauseMenuUI : Control
{
    [ExportGroup("UI Nodes")]
    [Export] public Button ResumeButton { get; set; }
    [Export] public Button InventoryButton { get; set; }
    [Export] public Button MapButton { get; set; }
    [Export] public Button RestartButton { get; set; }
    [Export] public Button QuitButton { get; set; }

    [ExportGroup("External Menus")]
    [Export] public InventoryUI InventoryMenu { get; set; }
    [Export] public MiniMapUI MapMenu { get; set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        ResumeButton ??= GetNodeOrNull<Button>("%ResumeButton");
        InventoryButton ??= GetNodeOrNull<Button>("%InventoryButton");
        MapButton ??= GetNodeOrNull<Button>("%MapButton");
        RestartButton ??= GetNodeOrNull<Button>("%RestartButton");
        QuitButton ??= GetNodeOrNull<Button>("%QuitButton");

        InventoryMenu ??= GetParent()?.GetNodeOrNull<InventoryUI>("%InventoryUI");
        MapMenu ??= GetParent()?.GetNodeOrNull<MiniMapUI>("%MiniMapUI");

        if (ResumeButton != null) ResumeButton.Pressed += TogglePause;
        if (InventoryButton != null) InventoryButton.Pressed += OnInventoryPressed;
        if (MapButton != null) MapButton.Pressed += OnMapPressed;
        if (RestartButton != null) RestartButton.Pressed += OnRestartPressed;
        if (QuitButton != null) QuitButton.Pressed += OnQuitPressed;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel") || (@event is InputEventKey key && key.Pressed && !key.Echo && (key.Keycode == Key.Escape || key.Keycode == Key.P)))
        {
            // If inventory or map is open, close them first
            if (InventoryMenu != null && InventoryMenu.Visible)
            {
                InventoryMenu.ToggleInventory();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (MapMenu != null && MapMenu.IsFullscreen)
            {
                MapMenu.ToggleFullscreenMap();
                GetViewport().SetInputAsHandled();
                return;
            }

            TogglePause();
            GetViewport().SetInputAsHandled();
        }
    }

    public void TogglePause()
    {
        Visible = !Visible;
        GetTree().Paused = Visible;
    }

    private void OnInventoryPressed()
    {
        Visible = false;
        InventoryMenu?.ToggleInventory();
    }

    private void OnMapPressed()
    {
        Visible = false;
        MapMenu?.ToggleFullscreenMap();
    }

    private void OnRestartPressed()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
