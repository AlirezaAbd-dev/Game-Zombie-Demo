using Godot;

namespace TopDownDemo.Core;

public enum FloorMarkingStyle { None, Road, Utility, Clinical, Archive, Roof }

[GlobalClass]
public partial class RoomVisualProfile : Resource
{
    [Export] public ShaderMaterial FloorMaterial { get; set; }
    [Export] public FloorMarkingStyle Markings { get; set; }
    [Export] public Color PaintColor { get; set; } = new(0.5f,0.5f,0.4f,0.75f);
    [Export] public Color WallColor { get; set; } = new(0.19f,0.22f,0.23f);
    [Export] public string Department { get; set; } = "FOXTROT-7";
}
