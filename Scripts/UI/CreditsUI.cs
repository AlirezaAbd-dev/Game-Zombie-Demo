using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class CreditsUI : Control
{
    private Label _endingTitleLabel;
    private Label _endingDescLabel;
    private VBoxContainer _creditsContainer;
    private Button _btnReturn;
    private float _scrollOffset = 0.0f;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        BuildUI();
        DisplayEndingStatus();

        AudioManager.Instance?.PlayMusic("ending_escape", 2.0f);
    }

    private void BuildUI()
    {
        var bg = new ColorRect
        {
            Color = new Color(0.02f, 0.03f, 0.05f, 1.0f)
        };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var vBox = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        vBox.AddThemeConstantOverride("separation", 18);
        center.AddChild(vBox);

        var gameTitle = new Label
        {
            Text = "BLACKSITE ZERO",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        gameTitle.AddThemeFontSizeOverride("font_size", 42);
        gameTitle.AddThemeColorOverride("font_color", new Color(0.95f, 0.25f, 0.25f));
        vBox.AddChild(gameTitle);

        _endingTitleLabel = new Label
        {
            Text = "MISSION CONCLUDED",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _endingTitleLabel.AddThemeFontSizeOverride("font_size", 22);
        _endingTitleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.8f, 0.35f));
        vBox.AddChild(_endingTitleLabel);

        _endingDescLabel = new Label
        {
            Text = "Debriefing in progress...",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(620, 60)
        };
        _endingDescLabel.AddThemeFontSizeOverride("font_size", 15);
        _endingDescLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        vBox.AddChild(_endingDescLabel);

        var separator = new HSeparator();
        vBox.AddChild(separator);

        var creditsText = new Label
        {
            Text = "Created by Senior Software Architect & Lead Game Developer\n" +
                   "Original Resident Evil Inspired Survival Horror System\n" +
                   "Procedural Horror Audio Synthesizer & Dynamic Atmosphere\n" +
                   "Engine: Godot 4.x C# (.NET 8)",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        creditsText.AddThemeFontSizeOverride("font_size", 14);
        creditsText.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
        vBox.AddChild(creditsText);

        _btnReturn = new Button
        {
            Text = "MAIN MENU",
            CustomMinimumSize = new Vector2(200, 46),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _btnReturn.Pressed += OnReturnPressed;
        vBox.AddChild(_btnReturn);
    }

    private void DisplayEndingStatus()
    {
        if (StoryDirector.Instance == null) return;

        bool unit9Defeated = StoryDirector.Instance.HasFlag(StoryFlags.UNIT9_DEFEATED);
        bool sadeghiTrusted = StoryDirector.Instance.HasFlag(StoryFlags.SADEGHI_TRUSTED);
        bool leilaRescued = StoryDirector.Instance.HasFlag(StoryFlags.LEILA_RESCUED);
        bool secretConspiracy = StoryDirector.Instance.HasFlag(StoryFlags.SECRET_CONSPIRACY_UNLOCKED);

        if (secretConspiracy)
        {
            _endingTitleLabel.Text = "ENDING D: THE ARCHIVIST'S TRUTH (SECRET ENDING)";
            _endingDescLabel.Text = "Commander Ghorbani's black dossier was delivered to international oversight. Dr. Sadeghi was granted asylum, and the Blacksite Foxtrot-7 project was permanently struck from the record.";
        }
        else if (unit9Defeated && sadeghiTrusted && leilaRescued)
        {
            _endingTitleLabel.Text = "ENDING A: ESCAPE (GOOD ENDING)";
            _endingDescLabel.Text = "Sara and Leila reached the extraction helicopter as the containment blast doors sealed behind them. Dr. Sadeghi stayed behind to ensure the silo self-destruct engaged.";
        }
        else if (unit9Defeated && !sadeghiTrusted)
        {
            _endingTitleLabel.Text = "ENDING B: BURN IT DOWN (PYRRHIC ENDING)";
            _endingDescLabel.Text = "Unit-9 was destroyed, but Dr. Sadeghi was left in the darkness. Sara evacuated alone before thermite charges leveled the underground bunker.";
        }
        else
        {
            _endingTitleLabel.Text = "ENDING C: THE WEIGHT (SOMBER ENDING)";
            _endingDescLabel.Text = "The surviving extraction team evacuated into the stormy night. The memories of those left behind in the dark will haunt Sara forever.";
        }
    }

    private void OnReturnPressed()
    {
        SceneTransitionManager.Instance?.TransitionToScene(
            "res://Scenes/MainMenu.tscn",
            "BLACKSITE ZERO",
            "Main Terminal"
        );
    }
}
