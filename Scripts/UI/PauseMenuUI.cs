using Godot;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class PauseMenuUI : Control
{
    [ExportGroup("UI Nodes")]
    [Export] public Button ResumeButton { get; set; }
    [Export] public Button SaveButton { get; set; }
    [Export] public Button LoadButton { get; set; }
    [Export] public Button InventoryButton { get; set; }
    [Export] public Button MapButton { get; set; }
    [Export] public Button RestartButton { get; set; }
    [Export] public Button MainMenuButton { get; set; }
    [Export] public Button QuitButton { get; set; }

    [ExportGroup("External Menus")]
    [Export] public InventoryUI InventoryMenu { get; set; }
    [Export] public MiniMapUI MapMenu { get; set; }

    private SaveSlotUI _saveSlotUI;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        ResumeButton ??= GetNodeOrNull<Button>("%ResumeButton");
        SaveButton ??= GetNodeOrNull<Button>("%SaveButton");
        LoadButton ??= GetNodeOrNull<Button>("%LoadButton");
        InventoryButton ??= GetNodeOrNull<Button>("%InventoryButton");
        MapButton ??= GetNodeOrNull<Button>("%MapButton");
        RestartButton ??= GetNodeOrNull<Button>("%RestartButton");
        MainMenuButton ??= GetNodeOrNull<Button>("%MainMenuButton");
        QuitButton ??= GetNodeOrNull<Button>("%QuitButton");

        InventoryMenu ??= GetParent()?.GetNodeOrNull<InventoryUI>("%InventoryUI");
        MapMenu ??= GetParent()?.GetNodeOrNull<MiniMapUI>("%MiniMapUI");

        _saveSlotUI = new SaveSlotUI
        {
            Name = "SaveSlotUI",
            Visible = false
        };
        AddChild(_saveSlotUI);

        if (ResumeButton != null) ResumeButton.Pressed += TogglePause;
        if (SaveButton != null) SaveButton.Pressed += OnSavePressed;
        if (LoadButton != null) LoadButton.Pressed += OnLoadPressed;
        if (InventoryButton != null) InventoryButton.Pressed += OnInventoryPressed;
        if (MapButton != null) MapButton.Pressed += OnMapPressed;
        if (RestartButton != null) RestartButton.Pressed += OnRestartPressed;
        if (MainMenuButton != null) MainMenuButton.Pressed += OnMainMenuPressed;
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
        Input.MouseMode = Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Hidden;
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
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        GetTree().ReloadCurrentScene();
    }

    private void OnSavePressed()
    {
        if (_saveSlotUI != null)
        {
            _saveSlotUI.CurrentMode = SaveSlotUI.Mode.Save;
            _saveSlotUI.RefreshSlots();
            _saveSlotUI.Visible = true;
        }
    }

    private void OnLoadPressed()
    {
        if (_saveSlotUI != null)
        {
            _saveSlotUI.CurrentMode = SaveSlotUI.Mode.Load;
            _saveSlotUI.RefreshSlots();
            _saveSlotUI.Visible = true;
        }
    }

    private void OnMainMenuPressed()
    {
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        SceneTransitionManager.Instance?.TransitionToScene(
            "res://Scenes/MainMenu.tscn",
            "BLACKSITE ZERO",
            "Returning to Main Menu..."
        );
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
