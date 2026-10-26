using Godot;

namespace TopDownDemo.Core;

[GlobalClass]
public partial class PowerRoutingData : Resource
{
    [Export] public string Title { get; set; } = "AUXILIARY POWER DISTRIBUTION";
    [Export(PropertyHint.MultilineText)] public string Instructions { get; set; } = string.Empty;
    [Export] public string[] Circuits { get; set; } = new[] { "VENTILATION", "GATE MOTOR", "FLOODLIGHTS" };
    [Export] public int RequiredMask { get; set; } = 3;
    [Export] public string CompletionFlag { get; set; } = StoryFlags.FACILITY_POWER_RESTORED;
    [Export] public bool RestoresFacilityPower { get; set; } = true;
    [Export(PropertyHint.MultilineText)] public string FailureMessage { get; set; } = "OVERLOAD: isolate nonessential lighting; keep air and the gate motor online.";
    [Export] public string Speaker { get; set; } = "DR. SADEGHI";
    [Export(PropertyHint.MultilineText)] public string SuccessMessage { get; set; } = "Auxiliary power is stable. The gate control is responding. Enter the north gatehouse and override the bunker lockdown.";
}
