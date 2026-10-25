using System;
using Godot;

namespace TopDownDemo.Core;

public enum CutsceneStepType
{
    LockPlayer,
    UnlockPlayer,
    CameraPan,
    CameraZoom,
    Dialogue,
    FadeToBlack,
    FadeFromBlack,
    ShowTitleCard,
    PlaySound,
    ChangeMusic,
    Wait
}

[GlobalClass]
public partial class CutsceneStep : Resource
{
    [Export] public CutsceneStepType StepType { get; set; } = CutsceneStepType.Wait;
    [Export] public Vector2 TargetPosition { get; set; } = Vector2.Zero;
    [Export] public Vector2 TargetZoom { get; set; } = new Vector2(1.5f, 1.5f);
    [Export] public float Duration { get; set; } = 1.0f;
    [Export] public string TitleText { get; set; } = "";
    [Export] public string SubtitleText { get; set; } = "";
    [Export] public string MusicTrackId { get; set; } = "";
    [Export] public DialogueSequence DialogueRef { get; set; }
}

[GlobalClass]
public partial class CutsceneSequence : Resource
{
    [Export] public string CutsceneId { get; set; } = "";
    [Export] public Godot.Collections.Array<CutsceneStep> Steps { get; set; } = new();
}
