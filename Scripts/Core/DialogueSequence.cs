using System;
using Godot;

namespace TopDownDemo.Core;

[GlobalClass]
public partial class DialogueChoiceResource : Resource
{
    [Export] public string ChoiceText { get; set; } = string.Empty;
    [Export] public string SetStoryFlag { get; set; } = string.Empty;
    [Export] public string RequiredFlag { get; set; } = string.Empty;
    [Export] public int NextLineIndex { get; set; } = -1;
}

[GlobalClass]
public partial class DialogueLineResource : Resource
{
    [Export] public string Speaker { get; set; } = "SARA";
    [Export(PropertyHint.MultilineText)] public string Text { get; set; } = string.Empty;
    [Export] public string PortraitKey { get; set; } = "sara";
    [Export] public float TypewriterSpeed { get; set; } = 0.025f;
    [Export] public int NextLineIndex { get; set; } = -1;
    [Export] public Godot.Collections.Array<DialogueChoiceResource> Choices { get; set; } = new();
}

[GlobalClass]
public partial class DialogueSequence : Resource
{
    [Export] public string SequenceId { get; set; } = string.Empty;
    [Export] public Godot.Collections.Array<DialogueLineResource> Lines { get; set; } = new();
}
