using Godot;

namespace TopDownDemo.UI;

public partial class FacilityWallView : Node2D
{
    public Vector2 Size { get; set; }
    public Color WallColor { get; set; }

    public override void _Draw()
    {
        var bounds = new Rect2(-Size / 2,Size);
        DrawRect(bounds,WallColor);
        DrawRect(bounds.Grow(-3),WallColor.Lightened(0.14f),false,3);
        DrawRect(bounds.Grow(-8),WallColor.Darkened(0.4f),false,2);
        bool vertical = Size.Y > Size.X;
        float length = vertical ? Size.Y : Size.X;
        float thickness = vertical ? Size.X : Size.Y;
        for (float along = 64; along < length - 8; along += 128)
        {
            Vector2 a = vertical ? new Vector2(-thickness / 2 + 10,along - length / 2) : new Vector2(along - length / 2,-thickness / 2 + 10);
            Vector2 b = vertical ? a + new Vector2(thickness - 20,0) : a + new Vector2(0,thickness - 20);
            DrawLine(a,b,WallColor.Darkened(0.45f),3);
            DrawCircle(a + (b - a).Normalized() * 4,2,WallColor.Lightened(0.25f));
            DrawCircle(b - (b - a).Normalized() * 4,2,WallColor.Lightened(0.25f));
        }
    }
}
