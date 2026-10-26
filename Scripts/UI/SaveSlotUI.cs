using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class SaveSlotUI : Control
{
    [Signal]
    public delegate void SlotSelectedEventHandler(int slotIndex);

    [Signal]
    public delegate void ClosedEventHandler();

    public enum Mode
    {
        Load,
        Save
    }

    public Mode CurrentMode { get; set; } = Mode.Load;

    private VBoxContainer _slotContainer;
    private Button _closeButton;
    private Label _title;
    private Control _previousFocus;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUI();
        RefreshSlots();
        VisibilityChanged += () =>
        {
            if (Visible)
            {
                _previousFocus = GetViewport().GuiGetFocusOwner();
                _closeButton.GrabFocus();
            }
            else if (IsInstanceValid(_previousFocus) && _previousFocus.IsVisibleInTree()) _previousFocus.GrabFocus();
        };
    }

    private void BuildUI()
    {
        // Dark translucent background
        var bg = new ColorRect
        {
            Color = new Color(0.02f, 0.03f, 0.05f, 0.92f),
            MouseFilter = MouseFilterEnum.Stop
        };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(720, 520)
        };
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.09f, 0.14f, 0.98f),
            BorderColor = new Color(0.85f, 0.65f, 0.25f, 0.85f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 20,
            ContentMarginBottom = 20
        };
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        center.AddChild(panel);

        var vBox = new VBoxContainer();
        vBox.AddThemeConstantOverride("separation", 16);
        panel.AddChild(vBox);

        _title = new Label
        {
            Text = CurrentMode == Mode.Save ? "SELECT SAVE SLOT" : "LOAD GAME",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _title.AddThemeFontSizeOverride("font_size", 24);
        _title.AddThemeColorOverride("font_color", new Color(0.95f, 0.75f, 0.3f));
        vBox.AddChild(_title);

        _slotContainer = new VBoxContainer();
        _slotContainer.AddThemeConstantOverride("separation", 12);
        vBox.AddChild(_slotContainer);

        _closeButton = new Button
        {
            Text = "BACK [ESC]",
            CustomMinimumSize = new Vector2(160, 40),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _closeButton.Pressed += OnClosePressed;
        vBox.AddChild(_closeButton);
    }

    public void RefreshSlots()
    {
        if (_title != null) _title.Text = CurrentMode == Mode.Save ? "SELECT SAVE SLOT" : "LOAD GAME";
        if (_slotContainer == null) return;

        foreach (Node child in _slotContainer.GetChildren())
        {
            child.QueueFree();
        }

        for (int i = 0; i < SaveSystem.MaxSlots; i++)
        {
            int slotIndex = i;
            var data = SaveSystem.Instance.GetSlotInfo(slotIndex);

            var slotCard = new PanelContainer
            {
                CustomMinimumSize = new Vector2(660, 95)
            };
            var cardStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.10f, 0.14f, 0.20f, 0.9f),
                BorderColor = new Color(0.35f, 0.5f, 0.7f, 0.4f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                ContentMarginLeft = 16,
                ContentMarginRight = 16,
                ContentMarginTop = 12,
                ContentMarginBottom = 12
            };
            slotCard.AddThemeStyleboxOverride("panel", cardStyle);

            var hBox = new HBoxContainer();
            hBox.AddThemeConstantOverride("separation", 16);
            slotCard.AddChild(hBox);

            var infoBox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            hBox.AddChild(infoBox);

            if (data != null)
            {
                var slotHeader = new Label
                {
                    Text = $"SLOT {slotIndex + 1}: {data.CurrentAct.ToString().Replace('_', ' ').ToUpper()}"
                };
                slotHeader.AddThemeFontSizeOverride("font_size", 18);
                slotHeader.AddThemeColorOverride("font_color", new Color(0.95f, 0.8f, 0.4f));
                infoBox.AddChild(slotHeader);

                float duration = data.PlaytimeSeconds + data.DialoguePlaytimeSeconds;
                int mins = (int)(duration / 60.0f);
                int secs = (int)(duration % 60.0f);
                var details = new Label
                {
                    Text = $"Difficulty: {data.Difficulty} | HP: {(int)data.PlayerHealth} | Time: {mins:D2}:{secs:D2} | Saved: {data.SavedAt.ToLocalTime():yyyy-MM-dd HH:mm}"
                };
                details.AddThemeFontSizeOverride("font_size", 13);
                details.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
                infoBox.AddChild(details);
            }
            else
            {
                var emptyLabel = new Label
                {
                    Text = $"SLOT {slotIndex + 1}: [EMPTY ARCHIVE]"
                };
                emptyLabel.AddThemeFontSizeOverride("font_size", 16);
                emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                infoBox.AddChild(emptyLabel);
            }

            var btnAction = new Button
            {
                Text = CurrentMode == Mode.Save ? "SAVE" : (data != null ? "LOAD" : "EMPTY"),
                CustomMinimumSize = new Vector2(100, 42),
                Disabled = CurrentMode == Mode.Load && data == null
            };
            btnAction.Pressed += () => OnSlotActionPressed(slotIndex);
            hBox.AddChild(btnAction);

            if (data != null)
            {
                var btnDelete = new Button
                {
                    Text = "DELETE",
                    CustomMinimumSize = new Vector2(80, 42)
                };
                btnDelete.Pressed += () =>
                {
                    SaveSystem.Instance.DeleteSave(slotIndex);
                    RefreshSlots();
                };
                hBox.AddChild(btnDelete);
            }

            _slotContainer.AddChild(slotCard);
        }
    }

    private void OnSlotActionPressed(int slotIndex)
    {
        EmitSignal(SignalName.SlotSelected, slotIndex);

        if (CurrentMode == Mode.Load)
        {
            if (SaveSystem.Instance.Load(slotIndex))
            {
                var data = SaveSystem.Instance.ActiveSaveData;
                Visible = false;
                SceneTransitionManager.Instance?.TransitionToScene(
                    data.CurrentScenePath,
                    data.CurrentAct.ToString().Replace('_', ' ').ToUpper(),
                    "Restoring classified operative state..."
                );
            }
        }
        else if (CurrentMode == Mode.Save)
        {
            Player player = GetTree().GetFirstNodeInGroup("player") as Player;
            string scenePath = GetTree().CurrentScene?.SceneFilePath ?? "res://Scenes/Act1_Gate.tscn";
            StoryAct act = StoryDirector.Instance?.CurrentAct ?? StoryAct.Act1_Arrival;

            if (SaveSystem.Instance.Save(slotIndex, player, scenePath, act))
            {
                AudioManager.Instance?.PlaySound(SoundType.ItemUnlock);
                RefreshSlots();
            }
        }
    }

    private void OnClosePressed()
    {
        Visible = false;
        EmitSignal(SignalName.Closed);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("quit"))
        {
            OnClosePressed();
            GetViewport().SetInputAsHandled();
        }
    }
}
