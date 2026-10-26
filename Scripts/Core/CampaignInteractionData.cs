using Godot;

namespace TopDownDemo.Core;

[GlobalClass]
public partial class CampaignInteractionData : Resource
{
    [Export] public string Prompt { get; set; } = "EXAMINE";
    [Export] public string RequiredFlag { get; set; } = string.Empty;
    [Export] public string RequiredItemId { get; set; } = string.Empty;
    [Export] public string LockedMessage { get; set; } = "COMPLETE THE CURRENT OBJECTIVE FIRST";
    [Export] public string CompletionFlag { get; set; } = string.Empty;
    [Export] public string[] AdditionalFlags { get; set; } = System.Array.Empty<string>();
    [Export] public string DialogueId { get; set; } = string.Empty;
    [Export] public ItemData GrantedItem { get; set; }
    [Export] public string Speaker { get; set; } = "SARA";
    [Export(PropertyHint.MultilineText)] public string Message { get; set; } = string.Empty;
    [Export] public bool IsCheckpoint { get; set; }
    [Export] public bool Repeatable { get; set; }
    [Export] public bool RestoresPower { get; set; }
}
