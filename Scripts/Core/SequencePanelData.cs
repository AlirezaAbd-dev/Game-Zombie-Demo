using Godot;

namespace TopDownDemo.Core;

[GlobalClass]
public partial class SequencePanelData : Resource
{
    [Export] public string Title { get; set; } = "MECHANICAL STARTUP";
    [Export(PropertyHint.MultilineText)] public string Instructions { get; set; } = string.Empty;
    [Export] public string[] Steps { get; set; } = System.Array.Empty<string>();
    [Export] public int[] CorrectOrder { get; set; } = System.Array.Empty<int>();
    [Export] public string CompletionFlag { get; set; } = string.Empty;
    [Export] public string FailureMessage { get; set; } = "INTERLOCK TRIPPED. SEQUENCE RESET.";
    [Export] public string SuccessMessage { get; set; } = "STARTUP COMPLETE";
}
