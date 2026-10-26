using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class MainMenuUI : Control
{
    private Button _btnNewGame;
    private Button _btnContinue;
    private Button _btnLoadGame;
    private Button _btnCredits;
    private Button _btnQuit;

    private SaveSlotUI _saveSlotUI;
    private DifficultySelectUI _difficultySelectUI;
    private SettingsUI _settingsUI;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;

        BindControls();
        BuildModals();

        // Play atmospheric main menu military intro music
        AudioManager.Instance?.PlayMusic("intro_military", 1.5f);

        // Check if continue is available
        bool hasAnySave = SaveSystem.Instance.HasSave(0) || SaveSystem.Instance.HasSave(1) || SaveSystem.Instance.HasSave(2);
        if (_btnContinue != null)
        {
            _btnContinue.Disabled = !hasAnySave;
        }
    }

    private void BindControls()
    {
        _btnNewGame = GetNodeOrNull<Button>("%BtnNewGame");
        _btnContinue = GetNodeOrNull<Button>("%BtnContinue");
        _btnLoadGame = GetNodeOrNull<Button>("%BtnLoadGame");
        _btnCredits = GetNodeOrNull<Button>("%BtnCredits");
        _btnQuit = GetNodeOrNull<Button>("%BtnQuit");

        if (_btnNewGame != null) _btnNewGame.Pressed += OnNewGamePressed;
        if (_btnContinue != null) _btnContinue.Pressed += OnContinuePressed;
        if (_btnLoadGame != null) _btnLoadGame.Pressed += OnLoadGamePressed;
        if (_btnCredits != null) _btnCredits.Pressed += OnCreditsPressed;
        if (_btnQuit != null) _btnQuit.Pressed += OnQuitPressed;
    }

    private void BuildModals()
    {
        _saveSlotUI = new SaveSlotUI
        {
            Name = "SaveSlotUI",
            Visible = false
        };
        AddChild(_saveSlotUI);

        _difficultySelectUI = new DifficultySelectUI
        {
            Name = "DifficultySelectUI",
            Visible = false
        };
        AddChild(_difficultySelectUI);
        _settingsUI = new SettingsUI { Name = "SettingsUI" };
        AddChild(_settingsUI);
        var settingsButton = GetNodeOrNull<Button>("%BtnSettings");
        if (settingsButton != null) settingsButton.Pressed += _settingsUI.Open;
    }

    private void OnNewGamePressed()
    {
        AudioManager.Instance?.PlaySound(SoundType.ItemUnlock);
        _difficultySelectUI.TargetSlotIndex = 0;
        _difficultySelectUI.Visible = true;
    }

    private void OnContinuePressed()
    {
        // Load most recently saved slot
        int latestSlot = -1;
        DateTime latestTime = DateTime.MinValue;

        for (int i = 0; i < SaveSystem.MaxSlots; i++)
        {
            var info = SaveSystem.Instance.GetSlotInfo(i);
            if (info != null && info.SavedAt > latestTime)
            {
                latestTime = info.SavedAt;
                latestSlot = i;
            }
        }

        if (latestSlot >= 0 && SaveSystem.Instance.Load(latestSlot))
        {
            AudioManager.Instance?.PlaySound(SoundType.RadioChime);
            var data = SaveSystem.Instance.ActiveSaveData;
            SceneTransitionManager.Instance?.TransitionToScene(
                data.CurrentScenePath,
                data.CurrentAct.ToString().Replace('_', ' ').ToUpper(),
                "Continuing classified mission..."
            );
        }
    }

    private void OnLoadGamePressed()
    {
        AudioManager.Instance?.PlaySound(SoundType.DoorOpen);
        _saveSlotUI.CurrentMode = SaveSlotUI.Mode.Load;
        _saveSlotUI.RefreshSlots();
        _saveSlotUI.Visible = true;
    }

    private void OnCreditsPressed()
    {
        AudioManager.Instance?.PlaySound(SoundType.RadioChime);
        SceneTransitionManager.Instance?.TransitionToScene(
            "res://Scenes/Credits.tscn",
            "BLACKSITE ZERO",
            "Credits & Debriefing"
        );
    }

    private void OnQuitPressed()
    {
        ApplicationExit.Request(GetTree());
    }
}
