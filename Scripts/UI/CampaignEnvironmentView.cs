using Godot;
using TopDownDemo.Core;
using TopDownDemo.Systems;

namespace TopDownDemo.UI;

public partial class CampaignEnvironmentView : Node2D
{
    public CampaignActDirector Director { get; set; }
    public int StyledFloors { get; private set; }
    public int StyledWalls { get; private set; }

    public override void _Ready()
    {
        if (Director == null) { GD.PushError("Campaign environment requires its room director."); return; }
        foreach (Node node in GetTree().GetNodesInGroup("campaign_floor"))
        {
            if (node is not ColorRect floor) continue;
            CampaignRoomData room = FindRoom(floor.GetGlobalRect());
            if (room?.VisualProfile?.FloorMaterial == null)
            { GD.PushError($"Missing authored floor profile for {floor.Name}."); continue; }
            floor.Material = room.VisualProfile.FloorMaterial;
            floor.ZIndex = -10;
            var markings = new CampaignRoomMarkingsView { Name = "Markings_" + StyledFloors, Data = room, Position = room.Bounds.Position };
            AddChild(markings);
            StyledFloors++;
        }
        foreach (Node node in GetTree().GetNodesInGroup("campaign_room_label"))
            if (node is CanvasItem item) item.Visible = false;
        foreach (Node node in GetTree().GetNodesInGroup("campaign_walls"))
        {
            if (node is not StaticBody2D wall) continue;
            foreach (Node child in wall.GetChildren())
            {
                if (child is not CollisionShape2D collision || collision.Shape is not RectangleShape2D rectangle) continue;
                Color tint = new(0.19f,0.22f,0.23f);
                foreach (var room in Director.Rooms)
                    if (room.VisualProfile != null && room.Bounds.Grow(48).HasPoint(wall.GlobalPosition))
                    { tint = room.VisualProfile.WallColor; break; }
                wall.AddChild(new FacilityWallView { Name = "WallPanels", Size = rectangle.Size,
                    WallColor = tint, Position = collision.Position, Rotation = collision.Rotation });
                StyledWalls++;
                break;
            }
        }
    }

    private CampaignRoomData FindRoom(Rect2 bounds)
    {
        foreach (var room in Director.Rooms)
            if (room.Bounds.Position.IsEqualApprox(bounds.Position) && room.Bounds.Size.IsEqualApprox(bounds.Size)) return room;
        return null;
    }
}
