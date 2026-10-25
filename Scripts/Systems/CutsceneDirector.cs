using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;

namespace TopDownDemo.Systems;

public partial class CutsceneDirector : Node
{
    private static CutsceneDirector _instance;
    public static CutsceneDirector Instance
    {
        get => GodotObject.IsInstanceValid(_instance) ? _instance : null;
        private set => _instance = value;
    }

    [Signal]
    public delegate void CutsceneStartedEventHandler(string cutsceneId);

    [Signal]
    public delegate void CutsceneEndedEventHandler(string cutsceneId);

    public bool IsPlayingCutscene { get; private set; } = false;

    private CutsceneSequence _activeSequence;
    private int _currentStepIndex = 0;
    private Action _onCompleteCallback;
    private Camera2D _gameCamera;
    private Vector2 _originalCamPos;
    private Vector2 _originalCamZoom;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public void PlayCutscene(CutsceneSequence sequence, Action onComplete = null)
    {
        if (sequence == null || sequence.Steps.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        _activeSequence = sequence;
        _currentStepIndex = 0;
        _onCompleteCallback = onComplete;
        IsPlayingCutscene = true;

        _gameCamera = GetViewport().GetCamera2D();
        if (_gameCamera != null)
        {
            _originalCamPos = _gameCamera.GlobalPosition;
            _originalCamZoom = _gameCamera.Zoom;
        }

        EmitSignal(SignalName.CutsceneStarted, sequence.CutsceneId);
        ExecuteNextStep();
    }

    private void ExecuteNextStep()
    {
        if (_activeSequence == null || _currentStepIndex >= _activeSequence.Steps.Count)
        {
            EndCutscene();
            return;
        }

        var step = _activeSequence.Steps[_currentStepIndex];
        _currentStepIndex++;

        switch (step.StepType)
        {
            case CutsceneStepType.LockPlayer:
                LockPlayerInput(true);
                ExecuteNextStep();
                break;

            case CutsceneStepType.UnlockPlayer:
                LockPlayerInput(false);
                ExecuteNextStep();
                break;

            case CutsceneStepType.CameraPan:
                if (_gameCamera != null)
                {
                    Tween panTween = CreateTween();
                    panTween.TweenProperty(_gameCamera, "global_position", step.TargetPosition, step.Duration)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    panTween.TweenCallback(Callable.From(ExecuteNextStep));
                }
                else
                {
                    ExecuteNextStep();
                }
                break;

            case CutsceneStepType.CameraZoom:
                if (_gameCamera != null)
                {
                    Tween zoomTween = CreateTween();
                    zoomTween.TweenProperty(_gameCamera, "zoom", step.TargetZoom, step.Duration)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    zoomTween.TweenCallback(Callable.From(ExecuteNextStep));
                }
                else
                {
                    ExecuteNextStep();
                }
                break;

            case CutsceneStepType.FadeToBlack:
                SceneTransitionManager.Instance?.FadeToBlack(step.Duration, ExecuteNextStep);
                break;

            case CutsceneStepType.FadeFromBlack:
                SceneTransitionManager.Instance?.FadeFromBlack(step.Duration, ExecuteNextStep);
                break;

            case CutsceneStepType.Dialogue:
                if (step.DialogueRef != null && DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.StartDialogue(step.DialogueRef, (choice) =>
                    {
                        ExecuteNextStep();
                    });
                }
                else
                {
                    ExecuteNextStep();
                }
                break;

            case CutsceneStepType.ChangeMusic:
                if (!string.IsNullOrEmpty(step.MusicTrackId) && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayMusic(step.MusicTrackId, step.Duration);
                }
                ExecuteNextStep();
                break;

            case CutsceneStepType.Wait:
                GetTree().CreateTimer(step.Duration).Timeout += ExecuteNextStep;
                break;

            default:
                ExecuteNextStep();
                break;
        }
    }

    private void LockPlayerInput(bool locked)
    {
        Player player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null && GodotObject.IsInstanceValid(player))
        {
            player.IsInputLocked = locked;
        }
    }

    public void EndCutscene()
    {
        string seqId = _activeSequence?.CutsceneId ?? "";
        IsPlayingCutscene = false;
        _activeSequence = null;

        // Restore camera
        if (_gameCamera != null && GodotObject.IsInstanceValid(_gameCamera))
        {
            Player player = GetTree().GetFirstNodeInGroup("player") as Player;
            if (player != null && GodotObject.IsInstanceValid(player))
            {
                Tween restoreTween = CreateTween();
                restoreTween.TweenProperty(_gameCamera, "global_position", player.GlobalPosition, 0.5f);
                restoreTween.Parallel().TweenProperty(_gameCamera, "zoom", _originalCamZoom != Vector2.Zero ? _originalCamZoom : new Vector2(1.5f, 1.5f), 0.5f);
            }
        }

        LockPlayerInput(false);
        EmitSignal(SignalName.CutsceneEnded, seqId);
        _onCompleteCallback?.Invoke();
        _onCompleteCallback = null;
    }
}
