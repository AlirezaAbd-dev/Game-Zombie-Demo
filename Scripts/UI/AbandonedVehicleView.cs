using Godot;

namespace TopDownDemo.UI;

[Tool]
public partial class AbandonedVehicleView : Node2D
{
    public override void _Draw()
    {
        Color frame = new(0.1f,0.12f,0.105f);
        Color body = new(0.25f,0.29f,0.20f);
        Color trim = new(0.37f,0.39f,0.29f);
        Color glass = new(0.09f,0.17f,0.19f);
        DrawRect(new Rect2(-74,-107,148,222),new Color(0.015f,0.02f,0.015f,0.7f));
        foreach (float side in new[] { -80f, 64f })
        {
            DrawRect(new Rect2(side,-84,16,46),new Color(0.025f,0.03f,0.025f));
            DrawRect(new Rect2(side,56,16,46),new Color(0.025f,0.03f,0.025f));
        }
        DrawRect(new Rect2(-64,-114,128,228),frame);
        DrawRect(new Rect2(-59,-108,118,216),body);
        DrawRect(new Rect2(-54,-105,108,60),body.Darkened(0.12f));
        for (int i = -2; i <= 2; i++) DrawLine(new Vector2(i * 15,-94),new Vector2(i * 15,-58),frame,2);
        DrawRect(new Rect2(-50,-36,100,22),glass);
        DrawLine(new Vector2(-49,-35),new Vector2(48,-35),trim,2);
        DrawRect(new Rect2(-54,-9,108,42),body.Lightened(0.06f));
        DrawLine(new Vector2(-52,31),new Vector2(52,31),frame,3);
        DrawRect(new Rect2(-50,42,100,61),new Color(0.19f,0.22f,0.16f));
        DrawRect(new Rect2(-39,45,78,52),new Color(0.27f,0.28f,0.2f));
        DrawLine(new Vector2(-23,43),new Vector2(-23,100),frame,5);
        DrawLine(new Vector2(23,43),new Vector2(23,100),frame,5);
        DrawLine(new Vector2(-60,-41),new Vector2(-60,35),trim,2);
        DrawLine(new Vector2(60,-41),new Vector2(60,35),trim,2);
        DrawRect(new Rect2(-53,-111,18,7),new Color(0.65f,0.59f,0.36f));
        DrawRect(new Rect2(35,-111,18,7),new Color(0.42f,0.4f,0.3f));
        DrawRect(new Rect2(-55,105,12,5),new Color(0.4f,0.07f,0.035f));
        DrawRect(new Rect2(43,105,12,5),new Color(0.4f,0.07f,0.035f));
        DrawLine(new Vector2(-29,4),new Vector2(-5,4),new Color(0.46f,0.45f,0.32f),3);
    }
}
