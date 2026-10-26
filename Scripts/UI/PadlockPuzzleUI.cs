using System;
using Godot;
using TopDownDemo.Entities;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class PadlockPuzzleUI : Control
{
    private static PadlockPuzzleUI _instance;
    public static PadlockPuzzleUI Instance
    {
        get => GodotObject.IsInstanceValid(_instance) ? _instance : null;
        private set => _instance = value;
    }

    [Signal]
    public delegate void PuzzleSolvedEventHandler();

    [Signal]
    public delegate void PuzzleClosedEventHandler();

    private readonly Texture2D[] _symbolTextures = new Texture2D[8];
    private readonly int[] _dialIndices = new int[3] { 4, 3, 7 }; // Initial scrambled: Eagle, Fish, Serpent
    private readonly int[] _solution = new int[3] { 0, 1, 2 }; // Target: Crop, Beast, Child

    private int _selectedDialIndex = 0;
    private bool _isSolved = false;
    private bool _isClosing = false;

    private RiddleDoor _targetDoor;
    private Player _player;

    // UI Nodes
    private ColorRect _dimOverlay;
    private Control _lockWrapper;
    private TextureRect _woodBg;
    private TextureRect _padlockFrame;
    private Label _titleLabel;
    private Label _riddleLabel;
    private Label _statusLabel;
    private Button _exitButton;

    private readonly Control[] _dialContainers = new Control[3];
    private readonly TextureRect[] _dialBgs = new TextureRect[3];
    private readonly TextureRect[] _dialSymbols = new TextureRect[3];
    private readonly ReferenceRect[] _dialHighlights = new ReferenceRect[3];
    private readonly Button[] _upButtons = new Button[3];
    private readonly Button[] _downButtons = new Button[3];

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        LoadSymbolTextures();
        BuildUI();
    }

    private void LoadSymbolTextures()
    {
        string[] symbolNames =
        {
            "symbol_crop",
            "symbol_beast",
            "symbol_child",
            "symbol_fish",
            "symbol_eagle",
            "symbol_skull",
            "symbol_chalice",
            "symbol_serpent"
        };

        for (int i = 0; i < symbolNames.Length; i++)
        {
            string path = $"res://Assets/textures/{symbolNames[i]}.png";
            _symbolTextures[i] = GD.Load<Texture2D>(path);
        }
    }

    private void BuildUI()
    {
        AnchorsPreset = (int)LayoutPreset.FullRect;
        AnchorRight = 1.0f;
        AnchorBottom = 1.0f;
        MouseFilter = MouseFilterEnum.Stop;

        // Dark dim backdrop
        _dimOverlay = new ColorRect
        {
            Name = "DimOverlay",
            Color = new Color(0.0f, 0.0f, 0.0f, 0.78f),
            MouseFilter = MouseFilterEnum.Stop
        };
        _dimOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_dimOverlay);

        // Centered Lock Wrapper (820 x 560)
        _lockWrapper = new Control
        {
            Name = "LockWrapper",
            CustomMinimumSize = new Vector2(820, 560),
            MouseFilter = MouseFilterEnum.Stop
        };
        _lockWrapper.SetAnchorsPreset(LayoutPreset.Center);
        _lockWrapper.Position = new Vector2(-410, -280);
        AddChild(_lockWrapper);

        // Carved door wooden panel background
        var woodTex = GD.Load<Texture2D>("res://Assets/textures/puzzle_door_wood_bg.png");
        _woodBg = new TextureRect
        {
            Name = "WoodBg",
            Texture = woodTex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            CustomMinimumSize = new Vector2(800, 520),
            Position = new Vector2(10, 20),
            MouseFilter = MouseFilterEnum.Pass
        };
        _lockWrapper.AddChild(_woodBg);

        // Title Header
        _titleLabel = new Label
        {
            Name = "TitleLabel",
            Text = "ARCHAIC COMBINATION LOCK",
            Position = new Vector2(20, 36),
            Size = new Vector2(780, 28),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.55f, 1.0f));
        _titleLabel.AddThemeFontSizeOverride("font_size", 20);
        _lockWrapper.AddChild(_titleLabel);

        // Padlock Frame (560 x 380, centered horizontally)
        var frameTex = GD.Load<Texture2D>("res://Assets/textures/padlock_frame.png");
        _padlockFrame = new TextureRect
        {
            Name = "PadlockFrame",
            Texture = frameTex,
            CustomMinimumSize = new Vector2(560, 380),
            Position = new Vector2(130, 80),
            MouseFilter = MouseFilterEnum.Pass
        };
        _lockWrapper.AddChild(_padlockFrame);

        // 3 Dial Cylinders inside Padlock chamber
        var dialBgTex = GD.Load<Texture2D>("res://Assets/textures/padlock_dial_bg.png");
        float[] dialXOffsets = { 152.0f, 238.0f, 324.0f };
        float dialYOffset = 162.0f;

        for (int i = 0; i < 3; i++)
        {
            int dialIndex = i;
            var container = new Control
            {
                Name = $"DialContainer_{i}",
                CustomMinimumSize = new Vector2(84, 136),
                Position = new Vector2(dialXOffsets[i], dialYOffset),
                MouseFilter = MouseFilterEnum.Stop
            };
            _padlockFrame.AddChild(container);
            _dialContainers[i] = container;

            // Dial Cylinder Background
            var bg = new TextureRect
            {
                Name = "DialBg",
                Texture = dialBgTex,
                CustomMinimumSize = new Vector2(84, 136),
                MouseFilter = MouseFilterEnum.Pass
            };
            container.AddChild(bg);
            _dialBgs[i] = bg;

            // Symbol Texture (centered, 64x64)
            var symbol = new TextureRect
            {
                Name = "Symbol",
                Texture = _symbolTextures[_dialIndices[i]],
                CustomMinimumSize = new Vector2(64, 64),
                Position = new Vector2(10, 36),
                MouseFilter = MouseFilterEnum.Pass
            };
            container.AddChild(symbol);
            _dialSymbols[i] = symbol;

            // Selection Highlight Border
            var highlight = new ReferenceRect
            {
                Name = "Highlight",
                BorderColor = new Color(1.0f, 0.85f, 0.35f, 0.9f),
                BorderWidth = 2.5f,
                CustomMinimumSize = new Vector2(84, 136),
                EditorOnly = false,
                Visible = (i == _selectedDialIndex),
                MouseFilter = MouseFilterEnum.Ignore
            };
            container.AddChild(highlight);
            _dialHighlights[i] = highlight;

            // Up Arrow Button (above dial)
            var upBtn = new Button
            {
                Name = $"UpBtn_{i}",
                Text = "▲",
                CustomMinimumSize = new Vector2(60, 26),
                Position = new Vector2(dialXOffsets[i] + 12, dialYOffset - 30),
                FocusMode = FocusModeEnum.None
            };
            upBtn.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.45f, 1.0f));
            upBtn.Pressed += () => RotateDial(dialIndex, true);
            _padlockFrame.AddChild(upBtn);
            _upButtons[i] = upBtn;

            // Down Arrow Button (below dial)
            var downBtn = new Button
            {
                Name = $"DownBtn_{i}",
                Text = "▼",
                CustomMinimumSize = new Vector2(60, 26),
                Position = new Vector2(dialXOffsets[i] + 12, dialYOffset + 140),
                FocusMode = FocusModeEnum.None
            };
            downBtn.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.45f, 1.0f));
            downBtn.Pressed += () => RotateDial(dialIndex, false);
            _padlockFrame.AddChild(downBtn);
            _downButtons[i] = downBtn;

            // Click or scroll on container
            container.GuiInput += (ev) => OnDialGuiInput(dialIndex, ev);
        }

        // Faded Inscription / Riddle Clue Banner
        _riddleLabel = new Label
        {
            Name = "RiddleLabel",
            Text = "\"The harvest feeds the beast, and the beast protects the child.\"",
            Position = new Vector2(30, 468),
            Size = new Vector2(760, 24),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _riddleLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 0.55f, 0.9f));
        _riddleLabel.AddThemeFontSizeOverride("font_size", 15);
        _lockWrapper.AddChild(_riddleLabel);

        // Status & Navigation Hint
        _statusLabel = new Label
        {
            Name = "StatusLabel",
            Text = "[A] / [D] Select Dial   •   [W] / [S] Rotate Dial   •   [ESC] Step Away",
            Position = new Vector2(30, 496),
            Size = new Vector2(760, 22),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f, 0.85f));
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);
        _lockWrapper.AddChild(_statusLabel);

        // Step Away / Close Button
        _exitButton = new Button
        {
            Name = "ExitButton",
            Text = "STEP AWAY [ESC]",
            CustomMinimumSize = new Vector2(160, 32),
            Position = new Vector2(630, 508),
            FocusMode = FocusModeEnum.None
        };
        _exitButton.Pressed += Close;
        _lockWrapper.AddChild(_exitButton);
    }

    public void Open(RiddleDoor door, Player player)
    {
        if (Visible) return;

        _targetDoor = door;
        _player = player;
        _isSolved = false;
        _isClosing = false;

        _selectedDialIndex = 0;
        UpdateHighlightVisuals();
        RefreshDialDisplays();

        _statusLabel.Text = "[A] / [D] Select Dial   •   [W] / [S] Rotate Dial   •   [ESC] Step Away";
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f, 0.85f));

        if (_padlockFrame != null)
        {
            _padlockFrame.Position = new Vector2(130, 80);
            _padlockFrame.Modulate = Colors.White;
        }

        Visible = true;

        if (_player != null)
        {
            _player.IsInputLocked = true;
            Vector2 lockWorldPos = door.GlobalPosition + door.LockFocusOffset;
            _player.FocusCameraOn(lockWorldPos, 2.5f);
        }

        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void Close()
    {
        if (!Visible || _isClosing) return;
        _isClosing = true;
        ControlBindings.ReleaseGameplayActions();

        if (_player != null)
        {
            _player.ResetCameraFocus();
            _player.IsInputLocked = false;
        }

        Visible = false;
        _targetDoor = null;
        _player = null;
        _isClosing = false;

        EmitSignal(SignalName.PuzzleClosed);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || _isSolved) return;

        if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("interact"))
        {
            GetViewport().SetInputAsHandled();
            Close();
            return;
        }

        if (@event.IsActionPressed("ui_left") || @event.IsActionPressed("move_left"))
        {
            GetViewport().SetInputAsHandled();
            SelectDial((_selectedDialIndex + 2) % 3);
            return;
        }

        if (@event.IsActionPressed("ui_right") || @event.IsActionPressed("move_right"))
        {
            GetViewport().SetInputAsHandled();
            SelectDial((_selectedDialIndex + 1) % 3);
            return;
        }

        if (@event.IsActionPressed("ui_up") || @event.IsActionPressed("move_up"))
        {
            GetViewport().SetInputAsHandled();
            RotateDial(_selectedDialIndex, true);
            return;
        }

        if (@event.IsActionPressed("ui_down") || @event.IsActionPressed("move_down"))
        {
            GetViewport().SetInputAsHandled();
            RotateDial(_selectedDialIndex, false);
            return;
        }
    }

    private void OnDialGuiInput(int dialIndex, InputEvent @event)
    {
        if (!Visible || _isSolved) return;

        if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed)
        {
            SelectDial(dialIndex);

            if (mouseBtn.ButtonIndex == MouseButton.Left)
            {
                RotateDial(dialIndex, true);
            }
            else if (mouseBtn.ButtonIndex == MouseButton.WheelUp)
            {
                RotateDial(dialIndex, true);
            }
            else if (mouseBtn.ButtonIndex == MouseButton.WheelDown)
            {
                RotateDial(dialIndex, false);
            }
        }
    }

    private void SelectDial(int index)
    {
        _selectedDialIndex = Mathf.Clamp(index, 0, 2);
        UpdateHighlightVisuals();
    }

    private void UpdateHighlightVisuals()
    {
        for (int i = 0; i < 3; i++)
        {
            if (_dialHighlights[i] != null)
            {
                _dialHighlights[i].Visible = (i == _selectedDialIndex);
            }
        }
    }

    private void RotateDial(int dialIndex, bool forward)
    {
        if (_isSolved) return;

        SelectDial(dialIndex);

        int delta = forward ? 1 : -1;
        _dialIndices[dialIndex] = (_dialIndices[dialIndex] + delta + 8) % 8;

        AudioManager.Instance?.PlaySound(SoundType.PadlockClick, pitchScale: (float)GD.RandRange(0.96f, 1.04f));

        // Dial punch animation
        var symbol = _dialSymbols[dialIndex];
        if (symbol != null)
        {
            symbol.Texture = _symbolTextures[_dialIndices[dialIndex]];
            var tween = CreateTween();
            float punchY = forward ? -6.0f : 6.0f;
            symbol.Position = new Vector2(10, 36 + punchY);
            tween.TweenProperty(symbol, "position", new Vector2(10, 36), 0.08)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        }

        CheckSolution();
    }

    private void RefreshDialDisplays()
    {
        for (int i = 0; i < 3; i++)
        {
            if (_dialSymbols[i] != null && _symbolTextures[_dialIndices[i]] != null)
            {
                _dialSymbols[i].Texture = _symbolTextures[_dialIndices[i]];
            }
        }
    }

    private void CheckSolution()
    {
        if (_dialIndices[0] == _solution[0] &&
            _dialIndices[1] == _solution[1] &&
            _dialIndices[2] == _solution[2])
        {
            OnPuzzleSolved();
        }
    }

    private void OnPuzzleSolved()
    {
        _isSolved = true;

        AudioManager.Instance?.PlaySound(SoundType.PadlockUnlock);

        _statusLabel.Text = "COMBINATION MATCHED — LOCK UNSEALED!";
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1.0f, 0.45f, 1.0f));

        // Padlock spring release bounce tween
        if (_padlockFrame != null)
        {
            var tween = CreateTween();
            tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(_padlockFrame, "position", new Vector2(130, 96), 0.22);
            tween.Parallel().TweenProperty(_padlockFrame, "modulate", new Color(1.2f, 1.2f, 1.0f, 1.0f), 0.22);
        }

        var doorRef = _targetDoor;
        var timer = GetTree().CreateTimer(0.85f);
        timer.Timeout += () =>
        {
            Close();
            doorRef?.OnPuzzleSolved();
            EmitSignal(SignalName.PuzzleSolved);
        };
    }
}
