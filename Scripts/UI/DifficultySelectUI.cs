using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class DifficultySelectUI : Control
{
    [Signal]
    public delegate void DifficultySelectedEventHandler(int difficultyIndex);

    [Signal]
    public delegate void CancelledEventHandler();

    public int TargetSlotIndex { get; set; } = 0;

    public override void _Ready()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        var bg = new ColorRect
        {
            Color = new Color(0.02f, 0.03f, 0.05f, 0.95f),
            MouseFilter = MouseFilterEnum.Stop
        };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var vBox = new VBoxContainer();
        vBox.AddThemeConstantOverride("separation", 24);
        center.AddChild(vBox);

        var title = new Label
        {
            Text = "SELECT DIFFICULTY LEVEL",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.75f, 0.3f));
        vBox.AddChild(title);

        var cardsHBox = new HBoxContainer();
        cardsHBox.AddThemeConstantOverride("separation", 20);
        vBox.AddChild(cardsHBox);

        cardsHBox.AddChild(CreateDifficultyCard(
            "SURVIVOR",
            "RECOMMENDED FOR FIRST PLAYTHROUGH",
            "• Standard HP & Ammo capacity\n• Balanced enemy damage (1.0x)\n• Generous stealth detection\n• Full HUD and tactical prompts",
            new Color(0.3f, 0.75f, 0.95f),
            DifficultyLevel.Survivor
        ));

        cardsHBox.AddChild(CreateDifficultyCard(
            "NIGHTMARE",
            "SURVIVAL HORROR VETERAN",
            "• Reduced ammo drops (-40%)\n• High enemy damage (1.5x)\n• +30% enemy population\n• Strict stealth vision & hearing",
            new Color(0.95f, 0.65f, 0.25f),
            DifficultyLevel.Nightmare
        ));

        cardsHBox.AddChild(CreateDifficultyCard(
            "RESEARCHER",
            "UNFORGIVING EXTRACTION",
            "• Scarce ammo supplies (-60%)\n• Lethal enemy damage (2.0x)\n• +60% aggressive infected horde\n• Brutal stealth: single error fatal",
            new Color(0.95f, 0.25f, 0.25f),
            DifficultyLevel.Researcher
        ));

        var backBtn = new Button
        {
            Text = "CANCEL [ESC]",
            CustomMinimumSize = new Vector2(160, 42),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        backBtn.Pressed += () =>
        {
            Visible = false;
            EmitSignal(SignalName.Cancelled);
        };
        vBox.AddChild(backBtn);
    }

    private Control CreateDifficultyCard(string title, string tag, string details, Color accentColor, DifficultyLevel level)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(250, 320)
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.11f, 0.16f, 0.95f),
            BorderColor = accentColor,
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 20,
            ContentMarginBottom = 16
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var vBox = new VBoxContainer();
        vBox.AddThemeConstantOverride("separation", 12);
        panel.AddChild(vBox);

        var titleLabel = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        titleLabel.AddThemeColorOverride("font_color", accentColor);
        vBox.AddChild(titleLabel);

        var tagLabel = new Label
        {
            Text = tag,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        tagLabel.AddThemeFontSizeOverride("font_size", 11);
        tagLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        vBox.AddChild(tagLabel);

        var descLabel = new Label
        {
            Text = details,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        descLabel.AddThemeFontSizeOverride("font_size", 13);
        descLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        vBox.AddChild(descLabel);

        var btn = new Button
        {
            Text = $"START {title}",
            CustomMinimumSize = new Vector2(0, 44)
        };
        btn.Pressed += () => OnDifficultyChosen(level);
        vBox.AddChild(btn);

        return panel;
    }

    private void OnDifficultyChosen(DifficultyLevel level)
    {
        Visible = false;
        EmitSignal(SignalName.DifficultySelected, (int)level);

        SaveSystem.Instance.CreateNewGame(TargetSlotIndex, level);

        SceneTransitionManager.Instance?.TransitionToScene(
            "res://Scenes/Act1_Gate.tscn",
            "ACT 1: ARRIVAL",
            "Alborz Mountain Perimeter — 02:40 AM"
        );
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("quit"))
        {
            Visible = false;
            EmitSignal(SignalName.Cancelled);
            GetViewport().SetInputAsHandled();
        }
    }
}
