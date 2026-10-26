using Godot;

namespace TopDownDemo.Core;

[GlobalClass]
public partial class HelicopterExtractionData : Resource
{
    [Export] public string RequestFlag { get; set; } = "evac_beacon_online";
    [Export] public string ArrivedFlag { get; set; } = "evac_helicopter_arrived";
    [Export] public float ApproachSeconds { get; set; } = 12;
    [Export] public float BoardingSeconds { get; set; } = 1;
    [Export] public float DepartureSeconds { get; set; } = 5;
    [Export] public Vector2 ApproachOffset { get; set; } = new(-1400,-800);
    [Export] public Vector2 DepartureOffset { get; set; } = new(1400,-1000);
}
