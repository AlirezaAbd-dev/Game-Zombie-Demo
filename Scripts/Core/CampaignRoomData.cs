using Godot;

namespace TopDownDemo.Core;

[GlobalClass]
public partial class CampaignRoomData : Resource
{
    [Export] public string Title { get; set; } = string.Empty;
    [Export] public Rect2 Bounds { get; set; }
    [Export] public RoomVisualProfile VisualProfile { get; set; }
    [Export] public string AreaCode { get; set; } = string.Empty;
    [Export] public Vector2 StencilOffset { get; set; } = new(96,96);
}
