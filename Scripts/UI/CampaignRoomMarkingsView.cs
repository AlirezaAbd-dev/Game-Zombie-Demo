using Godot;
using TopDownDemo.Core;

namespace TopDownDemo.UI;

/// <summary>Paint, flush drains and paper debris have no collision or per-frame work.</summary>
public partial class CampaignRoomMarkingsView : Node2D
{
    public CampaignRoomData Data { get; set; }
    private Font _font;
    private string _code;
    public override void _Ready()
    {
        ZIndex = -9;
        _font = ThemeDB.FallbackFont;
        _code = Data.AreaCode + "  /  " + Data.VisualProfile.Department;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Data?.VisualProfile == null || _font == null) return;
        Vector2 size = Data.Bounds.Size;
        var profile = Data.VisualProfile;
        Color paint = profile.PaintColor;
        Vector2 text = Data.StencilOffset;
        float maxWidth = Mathf.Max(100,size.X - text.X - 64);
        DrawString(_font,text,_code,HorizontalAlignment.Left,maxWidth,16,paint);
        DrawString(_font,text + new Vector2(0,32),Data.Title,HorizontalAlignment.Left,maxWidth,28,paint);
        DrawLine(text + new Vector2(0,45),text + new Vector2(Mathf.Min(maxWidth,340),45),paint,2);

        switch (profile.Markings)
        {
            case FloorMarkingStyle.Road:
                for (float x = 160; x < size.X - 160; x += 180)
                    DrawLine(new Vector2(x,size.Y * 0.54f),new Vector2(x + 90,size.Y * 0.54f),paint,5);
                DrawLine(new Vector2(96,size.Y - 100),new Vector2(size.X - 96,size.Y - 100),paint,3);
                break;
            case FloorMarkingStyle.Utility:
                DrawHazardStrip(new Vector2(80,size.Y - 100),Mathf.Min(size.X - 160,640),paint);
                DrawDrain(new Vector2(size.X - 120,160));
                break;
            case FloorMarkingStyle.Clinical:
                DrawLine(new Vector2(68,180),new Vector2(68,size.Y - 100),new Color(0.2f,0.34f,0.3f,0.7f),6);
                DrawDrain(new Vector2(size.X - 100,size.Y - 100));
                break;
            case FloorMarkingStyle.Archive:
                // Loose documents lie flat; these are stains/debris rather than impassable props.
                for (int i = 0; i < 8; i++)
                {
                    Vector2 p = new(140 + i * 31,size.Y - 150 + (i % 3) * 15);
                    DrawRect(new Rect2(p,new Vector2(15,20)),new Color(0.39f,0.36f,0.28f,0.7f));
                    DrawLine(p + new Vector2(3,5),p + new Vector2(11,5),new Color(0.12f,0.13f,0.12f),1);
                }
                break;
            case FloorMarkingStyle.Roof:
                DrawLine(new Vector2(100,size.Y - 70),new Vector2(size.X - 100,size.Y - 70),paint,3);
                DrawString(_font,new Vector2((size.X - 460) / 2,size.Y - 43),"VULTURE-9  /  STAND CLEAR DURING LANDING",HorizontalAlignment.Center,460,18,paint);
                break;
        }
    }

    private void DrawHazardStrip(Vector2 origin,float width,Color paint)
    {
        if (width <= 0) return;
        DrawRect(new Rect2(origin,new Vector2(width,20)),new Color(0.07f,0.075f,0.07f,0.8f));
        for (float x = 8; x < width - 24; x += 32)
            DrawLine(origin + new Vector2(x,18),origin + new Vector2(x + 18,2),paint,8);
    }

    private void DrawDrain(Vector2 center)
    {
        DrawRect(new Rect2(center - new Vector2(32,24),new Vector2(64,48)),new Color(0.055f,0.065f,0.065f));
        DrawRect(new Rect2(center - new Vector2(32,24),new Vector2(64,48)),new Color(0.25f,0.27f,0.26f),false,2);
        for (int i = -3; i <= 3; i++)
            DrawLine(center + new Vector2(i * 7,-19),center + new Vector2(i * 7,19),new Color(0.17f,0.19f,0.18f),3);
    }
}
