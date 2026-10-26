using Godot;
using TopDownDemo.Entities;

namespace TopDownDemo.UI;

public partial class CoolantTrapView : Node2D
{
    [Export] public CoolantTrapController Controller { get; set; }
    [Export] public float Radius { get; set; } = 280;
    public override void _Ready() { if (Controller == null) GD.PushError("Coolant view requires a trap controller."); }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (!IsInstanceValid(Controller)) return;
        float burst = Controller.BurstVisibility;
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 48, new Color(0.2f,0.85f,0.95f,0.35f), 3);
        if (burst > 0)
        {
            DrawCircle(Vector2.Zero, Radius, new Color(0.4f,0.9f,1,burst * 0.3f));
            DrawArc(Vector2.Zero, Radius * (1 - burst), 0, Mathf.Tau, 48, new Color(0.7f,1,1,burst), 7);
        }
    }
}
