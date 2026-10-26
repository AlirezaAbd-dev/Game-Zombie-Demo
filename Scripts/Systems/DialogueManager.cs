using System;
using System.Collections.Generic;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;

namespace TopDownDemo.Systems;

public partial class DialogueManager : CanvasLayer
{
    private static DialogueManager _instance;
    public static DialogueManager Instance
    {
        get => GodotObject.IsInstanceValid(_instance) ? _instance : null;
        private set => _instance = value;
    }

    [Signal]
    public delegate void DialogueStartedEventHandler();

    [Signal]
    public delegate void DialogueEndedEventHandler(int lastChoiceIndex);
    [Signal] public delegate void DialogueCancelledEventHandler();

    public bool IsDialogueActive { get; private set; } = false;
    public VBoxContainer ChoicesContainer => _choicesContainer;
    public int CurrentLineIndex { get => _currentLineIndex; set => _currentLineIndex = value; }

    private PanelContainer _dialogueBox;
    private TextureRect _portraitRect;
    private Label _speakerLabel;
    private RichTextLabel _dialogueText;
    private Label _promptLabel;
    private VBoxContainer _choicesContainer;

    private DialogueSequence _currentSequence;
    private int _currentLineIndex = 0;
    private Action<int> _onCompleteCallback;
    private int _lastChosenIndex = -1;
    private int _presentationId;
    private string _timingScenePath = string.Empty;
    private readonly System.Collections.Generic.Dictionary<string, bool> _choiceFlagOriginals = new();

    private float _typewriterTimer = 0.0f;
    private int _visibleCharacters = 0;
    private int _totalCharacters = 0;
    private bool _isTyping = false;
    private string _fullText = "";
    private bool _previousPaused;
    private bool _previousInputLock;
    private Input.MouseModeEnum _previousMouseMode;
    private Player _dialoguePlayer;

    private readonly Dictionary<string, Texture2D> _portraitCache = new();

    public override void _EnterTree()
    {
        Instance = this;
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        BuildUI();
        HideDialogue();
    }

    private void BuildUI()
    {
        // Lower-third root container
        var rootControl = new Control
        {
            Name = "DialogueRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        rootControl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(rootControl);

        _dialogueBox = new PanelContainer
        {
            CustomMinimumSize = new Vector2(980, 160)
        };
        _dialogueBox.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _dialogueBox.OffsetLeft = 150;
        _dialogueBox.OffsetRight = -150;
        _dialogueBox.OffsetBottom = -25;
        _dialogueBox.OffsetTop = -195;

        // Visual styling for dialogue box
        var styleBox = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.06f, 0.10f, 0.94f),
            BorderColor = new Color(0.85f, 0.65f, 0.25f, 0.85f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 16,
            ContentMarginBottom = 16
        };
        _dialogueBox.AddThemeStyleboxOverride("panel", styleBox);
        rootControl.AddChild(_dialogueBox);

        var hBox = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Begin
        };
        hBox.AddThemeConstantOverride("separation", 20);
        _dialogueBox.AddChild(hBox);

        // Portrait frame
        _portraitRect = new TextureRect
        {
            CustomMinimumSize = new Vector2(100, 100),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        hBox.AddChild(_portraitRect);

        // Text & choices column
        var vBox = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        vBox.AddThemeConstantOverride("separation", 8);
        hBox.AddChild(vBox);

        _speakerLabel = new Label
        {
            Text = "SPEAKER"
        };
        _speakerLabel.AddThemeFontSizeOverride("font_size", 20);
        _speakerLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.75f, 0.3f));
        vBox.AddChild(_speakerLabel);

        _dialogueText = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ScrollActive = false
        };
        _dialogueText.AddThemeFontSizeOverride("normal_font_size", 16);
        vBox.AddChild(_dialogueText);

        _choicesContainer = new VBoxContainer();
        _choicesContainer.AddThemeConstantOverride("separation", 6);
        vBox.AddChild(_choicesContainer);

        _promptLabel = new Label
        {
            Text = $"[{ControlBindings.Hint("interact")}] / [SPACE] Continue  ▶",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _promptLabel.AddThemeFontSizeOverride("font_size", 13);
        _promptLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f, 0.75f));
        vBox.AddChild(_promptLabel);
    }

    public void StartDialogue(DialogueSequence sequence, Action<int> onComplete = null)
    {
        if (IsDialogueActive) return;
        if (sequence == null || sequence.Lines.Count == 0)
        {
            onComplete?.Invoke(-1);
            return;
        }

        _currentSequence = sequence;
        _currentLineIndex = 0;
        _onCompleteCallback = onComplete;
        _lastChosenIndex = -1;
        _timingScenePath = CampaignActDirector.Instance != null ? GetTree().CurrentScene.SceneFilePath : string.Empty;
        _choiceFlagOriginals.Clear();
        IsDialogueActive = true;

        _previousPaused = GetTree().Paused;
        _previousMouseMode = Input.MouseMode;
        _dialoguePlayer = GetTree().GetFirstNodeInGroup("player") as Player;
        if (GodotObject.IsInstanceValid(_dialoguePlayer))
        {
            _previousInputLock = _dialoguePlayer.IsInputLocked;
            _dialoguePlayer.IsInputLocked = true;
            _dialoguePlayer.Velocity = Vector2.Zero;
        }
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        _dialogueBox.Visible = true;
        EmitSignal(SignalName.DialogueStarted);
        DisplayCurrentLine();
    }

    private void DisplayCurrentLine()
    {
        _presentationId++;
        if (_currentSequence == null || _currentLineIndex >= _currentSequence.Lines.Count)
        {
            EndDialogue();
            return;
        }

        var line = _currentSequence.Lines[_currentLineIndex];

        // Format speaker
        _speakerLabel.Text = line.Speaker.ToUpper();
        _speakerLabel.AddThemeColorOverride("font_color", GetSpeakerColor(line.Speaker));

        // Format portrait
        LoadPortrait(line.PortraitKey);

        // Clear choices
        foreach (Node child in _choicesContainer.GetChildren())
        {
            _choicesContainer.RemoveChild(child);
            child.QueueFree();
        }
        _choicesContainer.Visible = false;

        // Init typewriter
        _fullText = line.Text;
        _totalCharacters = _fullText.Length;
        _visibleCharacters = 0;
        _dialogueText.Text = "";
        _isTyping = true;
        _typewriterTimer = 0.0f;
        _promptLabel.Visible = false;
        _promptLabel.Text = $"[{ControlBindings.Hint("interact")}] / [SPACE] Continue  ▶";

        // Play typewriter chirp sound
        AudioManager.Instance?.PlaySound(SoundType.RadioChime);
    }

    private void LoadPortrait(string portraitKey)
    {
        if (string.IsNullOrEmpty(portraitKey))
        {
            _portraitRect.Visible = false;
            return;
        }

        _portraitRect.Visible = true;
        string path = $"res://Assets/textures/portraits/portrait_{portraitKey.ToLower()}.png";

        if (_portraitCache.TryGetValue(path, out var cachedTex))
        {
            _portraitRect.Texture = cachedTex;
            return;
        }

        if (ResourceLoader.Exists(path))
        {
            var tex = GD.Load<Texture2D>(path);
            _portraitCache[path] = tex;
            _portraitRect.Texture = tex;
        }
        else
        {
            // Placeholder fallback texture
            _portraitRect.Texture = GD.Load<Texture2D>("res://icon.svg");
        }
    }

    public override void _Process(double delta)
    {
        if (!IsDialogueActive) return;
        if (!_previousPaused) CampaignTiming.Record(_timingScenePath,(float)delta,dialogue:true);

        if (_isTyping)
        {
            _typewriterTimer += (float)delta;
            float speed = 0.02f;

            while (_typewriterTimer >= speed && _visibleCharacters < _totalCharacters)
            {
                _typewriterTimer -= speed;
                _visibleCharacters++;
                _dialogueText.Text = _fullText.Substring(0, _visibleCharacters);
            }

            if (_visibleCharacters >= _totalCharacters)
            {
                _isTyping = false;
                OnLineFinishedTyping();
            }
        }
    }

    private void OnLineFinishedTyping()
    {
        var line = _currentSequence.Lines[_currentLineIndex];

        if (line.Choices != null && line.Choices.Count > 0)
        {
            _promptLabel.Visible = false;
            _choicesContainer.Visible = true;

            int displayNum = 1;
            for (int i = 0; i < line.Choices.Count; i++)
            {
                var choice = line.Choices[i];
                if (!string.IsNullOrEmpty(choice.RequiredFlag))
                {
                    if (StoryDirector.Instance == null || !StoryDirector.Instance.HasFlag(choice.RequiredFlag))
                    {
                        continue; // Hide choice if requirement is not met
                    }
                }

                int choiceIndex = i;
                int presentation = _presentationId;

                var btn = new Button
                {
                    Text = $"[{displayNum++}] {choice.ChoiceText}",
                    Alignment = HorizontalAlignment.Left
                };
                btn.AddThemeFontSizeOverride("font_size", 14);

                var styleNormal = new StyleBoxFlat
                {
                    BgColor = new Color(0.12f, 0.16f, 0.24f, 0.9f),
                    BorderColor = new Color(0.4f, 0.6f, 0.8f, 0.5f),
                    BorderWidthBottom = 1,
                    BorderWidthTop = 1,
                    BorderWidthLeft = 1,
                    BorderWidthRight = 1,
                    CornerRadiusBottomLeft = 4,
                    CornerRadiusBottomRight = 4,
                    CornerRadiusTopLeft = 4,
                    CornerRadiusTopRight = 4,
                    ContentMarginLeft = 12,
                    ContentMarginRight = 12,
                    ContentMarginTop = 6,
                    ContentMarginBottom = 6
                };
                var styleHover = new StyleBoxFlat
                {
                    BgColor = new Color(0.22f, 0.32f, 0.48f, 0.95f),
                    BorderColor = new Color(0.9f, 0.75f, 0.3f, 1.0f),
                    BorderWidthBottom = 2,
                    BorderWidthTop = 2,
                    BorderWidthLeft = 2,
                    BorderWidthRight = 2,
                    CornerRadiusBottomLeft = 4,
                    CornerRadiusBottomRight = 4,
                    CornerRadiusTopLeft = 4,
                    CornerRadiusTopRight = 4,
                    ContentMarginLeft = 12,
                    ContentMarginRight = 12,
                    ContentMarginTop = 6,
                    ContentMarginBottom = 6
                };

                btn.AddThemeStyleboxOverride("normal", styleNormal);
                btn.AddThemeStyleboxOverride("hover", styleHover);
                btn.AddThemeStyleboxOverride("focus", styleHover);

                btn.Pressed += () =>
                {
                    if (IsDialogueActive && presentation == _presentationId) OnChoiceSelected(choiceIndex, choice);
                };
                _choicesContainer.AddChild(btn);
            }

            // Focus the first button for gamepad/keyboard accessibility
            if (_choicesContainer.GetChildCount() > 0)
            {
                ((Button)_choicesContainer.GetChild(0)).GrabFocus();
            }
            else
            {
                _choicesContainer.Visible = false;
                _promptLabel.Visible = true;
            }
        }
        else
        {
            _promptLabel.Visible = true;
        }
    }

    private void OnChoiceSelected(int index, DialogueChoiceResource choice)
    {
        _lastChosenIndex = index;
        ChangeChoiceFlag(choice.ClearStoryFlag, false);
        foreach (string flag in choice.ClearStoryFlags) ChangeChoiceFlag(flag, false);

        // Apply story flag if specified
        if (!string.IsNullOrEmpty(choice.SetStoryFlag) && StoryDirector.Instance != null)
        {
            ChangeChoiceFlag(choice.SetStoryFlag, true);
            GD.Print($"[DialogueManager] Story flag set: {choice.SetStoryFlag}");
        }

        AudioManager.Instance?.PlaySound(SoundType.PickupCollect);

        if (choice.NextLineIndex >= 0)
        {
            _currentLineIndex = choice.NextLineIndex;
            DisplayCurrentLine();
        }
        else
        {
            _currentLineIndex++;
            DisplayCurrentLine();
        }
    }

    private void ChangeChoiceFlag(string flag, bool enabled)
    {
        var story = StoryDirector.Instance;
        if (story == null || string.IsNullOrWhiteSpace(flag)) return;
        _choiceFlagOriginals.TryAdd(flag, story.HasFlag(flag));
        if (enabled) story.SetFlag(flag); else story.RemoveFlag(flag);
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsDialogueActive) return;

        if (@event.IsEcho())
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        // Choice shortcuts belong to the conversation even when the same key is
        // assigned to Interact or Dash during normal gameplay.
        if (_choicesContainer.Visible && @event is InputEventKey number && number.Pressed)
        {
            int choiceIndex = number.Keycode switch
            {
                Key.Key1 => 0, Key.Key2 => 1, Key.Key3 => 2, Key.Key4 => 3, _ => -1
            };
            if (choiceIndex >= 0 && choiceIndex < _choicesContainer.GetChildCount())
            {
                GetViewport().SetInputAsHandled();
                _choicesContainer.GetChild<Button>(choiceIndex).EmitSignal(BaseButton.SignalName.Pressed);
                return;
            }
        }

        if (@event.IsActionPressed("interact") || @event.IsActionPressed("dash") ||
            (@event is InputEventKey key && key.Pressed && (key.Keycode == Key.Space || key.Keycode == Key.Enter)))
        {
            if (_isTyping)
            {
                _isTyping = false;
                _visibleCharacters = _totalCharacters;
                _dialogueText.Text = _fullText;
                OnLineFinishedTyping();
                GetViewport().SetInputAsHandled();
            }
            else if (!_choicesContainer.Visible)
            {
                var line = _currentSequence.Lines[_currentLineIndex];
                _currentLineIndex = line.NextLineIndex >= 0 ? line.NextLineIndex : _currentLineIndex + 1;
                GetViewport().SetInputAsHandled();
                DisplayCurrentLine();
            }
            // Visible choices retain GUI keyboard/gamepad focus and mouse input.
        }
        // Other always-processing menus must not replace this conversation's pause.
        if (IsDialogueActive && (@event.IsActionPressed("toggle_inventory")
            || @event.IsActionPressed("toggle_map") || @event.IsActionPressed("pause_game")
            || @event.IsActionPressed("restart")))
            GetViewport().SetInputAsHandled();
    }

    public void EndDialogue()
    {
        if (!IsDialogueActive) return;
        _presentationId++;
        _choiceFlagOriginals.Clear();
        var callback = _onCompleteCallback;
        _onCompleteCallback = null;
        _currentSequence = null;
        HideDialogue();
        ReleaseGameplay();
        EmitSignal(SignalName.DialogueEnded, _lastChosenIndex);
        callback?.Invoke(_lastChosenIndex);
    }

    public void CancelDialogue()
    {
        if (!IsDialogueActive) return;
        _presentationId++;
        var story = StoryDirector.Instance;
        if (story != null)
            foreach (var original in _choiceFlagOriginals)
                if (original.Value) story.SetFlag(original.Key); else story.RemoveFlag(original.Key);
        _choiceFlagOriginals.Clear();
        HideDialogue();
        _onCompleteCallback = null;
        _currentSequence = null;
        ReleaseGameplay();
        EmitSignal(SignalName.DialogueCancelled);
    }

    private void ReleaseGameplay()
    {
        if (GodotObject.IsInstanceValid(_dialoguePlayer)) _dialoguePlayer.IsInputLocked = _previousInputLock;
        _dialoguePlayer = null;
        ControlBindings.ReleaseGameplayActions();
        GetTree().Paused = _previousPaused;
        Input.MouseMode = _previousMouseMode;
    }

    public override void _ExitTree()
    {
        CancelDialogue();
        if (_instance == this) _instance = null;
    }

    private void HideDialogue()
    {
        IsDialogueActive = false;
        if (_dialogueBox != null)
        {
            _dialogueBox.Visible = false;
        }
    }

    private static Color GetSpeakerColor(string speaker)
    {
        return speaker.ToUpperInvariant() switch
        {
            "SARA" => new Color(0.35f, 0.85f, 1.0f),       // Cyan
            "DR. SADEGHI" or "SADEGHI" => new Color(0.95f, 0.75f, 0.3f), // Amber
            "LEILA" => new Color(0.35f, 0.95f, 0.55f),     // Emerald
            "COMMANDER GHORBANI" or "GHORBANI" => new Color(0.95f, 0.25f, 0.25f), // Crimson
            "THE ARCHIVIST" => new Color(0.8f, 0.6f, 0.95f), // Ghostly purple
            _ => new Color(0.9f, 0.9f, 0.9f)
        };
    }
}
