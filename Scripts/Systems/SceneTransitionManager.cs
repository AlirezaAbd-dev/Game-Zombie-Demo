using System;
using Godot;
using TopDownDemo.Entities;

namespace TopDownDemo.Systems;

public partial class SceneTransitionManager : CanvasLayer
{
    private static SceneTransitionManager _instance;
    public static SceneTransitionManager Instance
    {
        get => GodotObject.IsInstanceValid(_instance) ? _instance : null;
        private set => _instance = value;
    }

    private ColorRect _fadeRect;
    private Label _titleLabel;
    private Label _subtitleLabel;
    private Control _titleContainer;
    private bool _isTransitioning = false;
    public bool IsTransitioning => _isTransitioning;

    public override void _EnterTree()
    {
        if (GodotObject.IsInstanceValid(_instance) && _instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        Layer = 120; // High layer above all HUD and dialogue
        ProcessMode = ProcessModeEnum.Always;

        var tree = GetTree();
        if (tree?.Root != null && GetParent() != tree.Root)
        {
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(this) && IsInsideTree() && GetParent() != null && GetParent() != tree.Root)
                {
                    GetParent().RemoveChild(this);
                    tree.Root.AddChild(this);
                }
            }).CallDeferred();
        }
    }

    public override void _Ready()
    {
        BuildOverlayUI();
    }

    private void BuildOverlayUI()
    {
        if (_fadeRect != null && GodotObject.IsInstanceValid(_fadeRect)) return;

        // Full screen black overlay
        _fadeRect = new ColorRect
        {
            Color = new Color(0, 0, 0, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _fadeRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_fadeRect);

        _titleContainer = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0)
        };
        _titleContainer.SetAnchorsPreset(Control.LayoutPreset.Center);
        AddChild(_titleContainer);

        _titleLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 34);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.2f, 0.2f));
        _titleContainer.AddChild(_titleLabel);

        _subtitleLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _subtitleLabel.AddThemeFontSizeOverride("font_size", 18);
        _subtitleLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f, 0.85f));
        _titleContainer.AddChild(_subtitleLabel);
    }

    public void TransitionToScene(string scenePath, string title = "", string subtitle = "", float duration = 0.5f, Action onSceneReady = null)
    {
        if (_isTransitioning || !IsInsideTree()) return;
        var tree = GetTree();
        DialogueManager.Instance?.CancelDialogue();
        Engine.TimeScale = 1.0;
        GetTree().Paused = true;
        _isTransitioning = true;

        if (_fadeRect == null || !GodotObject.IsInstanceValid(_fadeRect))
        {
            BuildOverlayUI();
        }

        _fadeRect.MouseFilter = Control.MouseFilterEnum.Stop;
        _titleLabel.Text = title;
        _subtitleLabel.Text = subtitle;

        Tween tween = CreateTween();
        // Step 1: Fade to black
        tween.TweenProperty(_fadeRect, "color:a", 1.0f, duration);

        if (!string.IsNullOrEmpty(title))
        {
            tween.TweenInterval(0.2f);
            tween.TweenProperty(_titleContainer, "modulate:a", 1.0f, 0.4f);
            tween.TweenInterval(1.6f);
            tween.TweenProperty(_titleContainer, "modulate:a", 0.0f, 0.4f);
        }

        tween.TweenCallback(Callable.From(() =>
        {
            Error err = tree.ChangeSceneToFile(scenePath);
            if (err != Error.Ok)
            {
                GD.PrintErr($"[SceneTransitionManager] Failed to load scene: {scenePath} (Error: {err})");
                if (GodotObject.IsInstanceValid(_fadeRect))
                {
                    _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
                }
                _isTransitioning = false;
                ControlBindings.ReleaseGameplayActions();
                tree.Paused = false;
                return;
            }

            // After change scene, hook next frame to find player and apply active save data
            Callable.From(() =>
            {
                var curTree = GetTree() ?? tree;
                Player player = curTree?.GetFirstNodeInGroup("player") as Player;
                if (player != null && SaveSystem.Instance != null)
                {
                    SaveSystem.Instance.ApplySaveDataToLoadedScene(player);
                }

                onSceneReady?.Invoke();

                // Step 2: Fade from black
                if (GodotObject.IsInstanceValid(this) && IsInsideTree() && GodotObject.IsInstanceValid(_fadeRect))
                {
                    Tween fadeOutTween = CreateTween();
                    fadeOutTween.TweenProperty(_fadeRect, "color:a", 0.0f, duration);
                    fadeOutTween.TweenCallback(Callable.From(() =>
                    {
                        if (GodotObject.IsInstanceValid(_fadeRect))
                        {
                            _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
                        }
                        _isTransitioning = false;
                        ControlBindings.ReleaseGameplayActions();
                        tree.Paused = false;
                    }));
                }
                else
                {
                    _isTransitioning = false;
                    ControlBindings.ReleaseGameplayActions();
                    tree.Paused = false;
                }
            }).CallDeferred();
        }));
    }

    public void FadeToBlack(float duration = 0.5f, Action onComplete = null)
    {
        if (_fadeRect == null) BuildOverlayUI();
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Stop;
        Tween tween = CreateTween();
        tween.TweenProperty(_fadeRect, "color:a", 1.0f, duration);
        if (onComplete != null)
        {
            tween.TweenCallback(Callable.From(onComplete));
        }
    }

    public void FadeFromBlack(float duration = 0.5f, Action onComplete = null)
    {
        if (_fadeRect == null) BuildOverlayUI();
        Tween tween = CreateTween();
        tween.TweenProperty(_fadeRect, "color:a", 0.0f, duration);
        tween.TweenCallback(Callable.From(() =>
        {
            _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
            onComplete?.Invoke();
        }));
    }
}
