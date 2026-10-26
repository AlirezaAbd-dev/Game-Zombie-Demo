using Godot;

namespace TopDownDemo.UI;

[Tool]
public partial class HelipadMarkingView : Node2D
{
    [Export] public float Radius { get; set; } = 260;
    public override void _Draw()
    {
        Color white = new(0.6f,0.62f,0.57f,0.85f);
        Color yellow = new(0.6f,0.49f,0.2f,0.8f);
        DrawArc(Vector2.Zero,Radius,0,Mathf.Tau,128,yellow,7,true);
        DrawArc(Vector2.Zero,Radius - 14,0,Mathf.Tau,128,white,2,true);
        DrawRect(new Rect2(-85,-120,28,240),white);
        DrawRect(new Rect2(57,-120,28,240),white);
        DrawRect(new Rect2(-57,-14,114,28),white);
        for (int i = 0; i < 4; i++)
        {
            Vector2 outward = Vector2.FromAngle(i * Mathf.Pi / 2);
            DrawLine(outward * (Radius + 8),outward * (Radius + 25),white,4);
        }
        DrawString(ThemeDB.FallbackFont,new Vector2(-70,196),"F7-H1",HorizontalAlignment.Center,140,22,white);
        Color wear = new(0.16f,0.17f,0.16f,0.45f);
        DrawLine(new Vector2(-82,-68),new Vector2(-58,-63),wear,2);
        DrawLine(new Vector2(59,53),new Vector2(82,62),wear,3);
        DrawLine(new Vector2(-25,-12),new Vector2(12,8),wear,2);
    }
}
