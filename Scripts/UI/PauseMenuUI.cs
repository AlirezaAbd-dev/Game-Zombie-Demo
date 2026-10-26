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
    public Button SettingsButton { get; private set; }
    public SettingsUI SettingsMenu { get; private set; }

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
        if (ResumeButton == null) BuildMenu();
        SettingsButton ??= AddButton(ResumeButton.GetParent(), "SettingsButton", "SETTINGS & CONTROLS");

        InventoryMenu ??= GetParent()?.GetNodeOrNull<InventoryUI>("%InventoryUI");
        MapMenu ??= GetParent()?.GetNodeOrNull<MiniMapUI>("%MiniMapUI");

        _saveSlotUI = new SaveSlotUI
        {
            Name = "SaveSlotUI",
            Visible = false
        };
        AddChild(_saveSlotUI);
        SettingsMenu = new SettingsUI { Name = "SettingsUI" };
        AddChild(SettingsMenu);
        SettingsButton.Pressed += SettingsMenu.Open;

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
        if ((@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("pause_game")) && !@event.IsEcho())
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
        if (SettingsMenu.Visible || _saveSlotUI.Visible) return;
        if (!Visible && GetTree().Paused) return;
        Visible = !Visible;
        if (!Visible) ControlBindings.ReleaseGameplayActions();
        GetTree().Paused = Visible;
        Input.MouseMode = Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Hidden;
        if (Visible) ResumeButton?.GrabFocus();
    }

    private void BuildMenu()
    {
        var background = new ColorRect { Color = new Color(0.02f, 0.03f, 0.05f, 0.94f) };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var rows = new VBoxContainer { CustomMinimumSize = new Vector2(370, 0) };
        rows.AddThemeConstantOverride("separation", 8);
        center.AddChild(rows);
        var title = new Label { Text = "MISSION PAUSED", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        rows.AddChild(title);
        ResumeButton = AddButton(rows, "ResumeButton", "RESUME [ESC]");
        SaveButton = AddButton(rows, "SaveButton", "SAVE CHECKPOINT");
        LoadButton = AddButton(rows, "LoadButton", "LOAD CHECKPOINT");
        InventoryButton = AddButton(rows, "InventoryButton", "INVENTORY");
        MapButton = AddButton(rows, "MapButton", "TACTICAL MAP");
        SettingsButton = AddButton(rows, "SettingsButton", "SETTINGS & CONTROLS");
        RestartButton = AddButton(rows, "RestartButton", "RESTART CHECKPOINT");
        MainMenuButton = AddButton(rows, "MainMenuButton", "MAIN MENU");
        QuitButton = AddButton(rows, "QuitButton", "QUIT GAME");
    }

    private static Button AddButton(Node parent, string id, string text)
    {
        var button = new Button { Name = id, Text = text, CustomMinimumSize = new Vector2(0, 40) };
        parent.AddChild(button);
        return button;
    }

    private void OnInventoryPressed()
    {
        Visible = false;
        GetTree().Paused = false;
        InventoryMenu?.ToggleInventory();
    }

    private void OnMapPressed()
    {
        Visible = false;
        GetTree().Paused = false;
        MapMenu?.ToggleFullscreenMap();
    }

    private void OnRestartPressed()
    {
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        if (CampaignActDirector.Instance != null) CampaignActDirector.Instance.RestartCheckpoint();
        else GetTree().ReloadCurrentScene();
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
        ApplicationExit.Request(GetTree());
    }
}
