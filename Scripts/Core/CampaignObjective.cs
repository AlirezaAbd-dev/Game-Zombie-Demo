using Godot;

namespace TopDownDemo.Core;

[GlobalClass]
public partial class CampaignObjective : Resource
{
    [Export] public string CompletionFlag { get; set; } = string.Empty;
    [Export] public string RequiredFlag { get; set; } = string.Empty;
    [Export] public string ExcludedFlag { get; set; } = string.Empty;
    [Export] public string Title { get; set; } = string.Empty;
    [Export(PropertyHint.MultilineText)] public string Instruction { get; set; } = string.Empty;
    [Export] public Vector2 Waypoint { get; set; }
}
