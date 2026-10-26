using Godot;

namespace TopDownDemo.UI;

public partial class HelicopterView : Node2D
{
    private float _rotor;
    private readonly Vector2[] _body = { new(-35,-130),new(35,-130),new(65,-70),new(60,80),new(25,115),
        new(12,235),new(-12,235),new(-25,115),new(-60,80),new(-65,-70) };
    private readonly Vector2[] _glass = { new(-30,-115),new(30,-115),new(49,-65),new(-49,-65) };
    public override void _Ready() => Material = new CanvasItemMaterial { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded };
    public override void _Process(double delta) { _rotor += (float)delta * 28; QueueRedraw(); }
    public override void _Draw()
    {
        DrawCircle(new Vector2(18,28),100,new Color(0,0,0,0.45f));
        DrawLine(new Vector2(-80,-60),new Vector2(-80,120),new Color(0.3f,0.35f,0.37f),9);
        DrawLine(new Vector2(80,-60),new Vector2(80,120),new Color(0.3f,0.35f,0.37f),9);
        DrawColoredPolygon(_body,new Color(0.22f,0.3f,0.25f));
        DrawColoredPolygon(_glass,new Color(0.2f,0.65f,0.7f));
        DrawRect(new Rect2(-28,-45,56,90),new Color(0.13f,0.18f,0.15f));
        DrawLine(new Vector2(-48,200),new Vector2(48,200),new Color(0.38f,0.44f,0.4f),12);
        DrawLine(new Vector2(60,-10),new Vector2(60,65),new Color(0.3f,0.95f,0.6f),5);
        DrawCircle(Vector2.Zero,16,new Color(0.1f,0.14f,0.12f));
        Vector2 rotor = Vector2.FromAngle(_rotor) * 205;
        Vector2 cross = rotor.Rotated(Mathf.Pi * 0.5f);
        DrawLine(-rotor,rotor,new Color(0.55f,0.6f,0.57f,0.85f),8);
        DrawLine(-cross,cross,new Color(0.55f,0.6f,0.57f,0.85f),8);
        DrawCircle(new Vector2(-60,45),5,Colors.Red);
        DrawCircle(new Vector2(60,45),5,Colors.LightGreen);
    }
}
