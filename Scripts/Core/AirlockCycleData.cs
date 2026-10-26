using Godot;

namespace TopDownDemo.Core;

public enum AirlockState { Idle, Sealing, Equalizing, Purging, Complete }

[GlobalClass]
public partial class AirlockCycleData : Resource
{
    [Export] public string ExhaustFlag { get; set; } = "decon_exhaust_online";
    [Export] public string LeakIsolatedFlag { get; set; } = "decon_leak_isolated";
    [Export] public string CompletionFlag { get; set; } = "decon_cycle_complete";
    [Export(PropertyHint.Range, "0.25,30")] public float SealSeconds { get; set; } = 0.8f;
    [Export(PropertyHint.Range, "0.25,30")] public float EqualizeSeconds { get; set; } = 3;
    [Export(PropertyHint.Range, "0.25,30")] public float PurgeSeconds { get; set; } = 3;
}
