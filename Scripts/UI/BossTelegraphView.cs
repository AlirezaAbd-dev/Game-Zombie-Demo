using Godot;
using TopDownDemo.Entities;
using TopDownDemo.Entities.Components;

namespace TopDownDemo.UI;

public partial class BossTelegraphView : Node2D
{
    [Export] public Boss_Unit9 Boss { get; set; }
    public override void _Ready()
    {
        Boss ??= GetParent() as Boss_Unit9;
        ZIndex = 1; // Authored floor CanvasItems occupy layer zero.
        Material = new CanvasItemMaterial { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded };
        if (Boss == null) GD.PushError("Boss warning view requires a boss.");
    }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (!IsInstanceValid(Boss) || Boss.IsDead || !Boss.IsEncounterActive || Boss.Combat == null) return;
        var combat = Boss.Combat;
        float angle = combat.LockedDirection.Angle() - GlobalRotation;
        if (combat.Phase == BossAttackPhase.MeleeWindup)
        {
            float radius = Boss.AttackRange + 25;
            var color = new Color(1, 0.55f, 0.1f, 0.8f);
            DrawArc(Vector2.Zero, radius, angle - combat.Data.SweepHalfAngle, angle + combat.Data.SweepHalfAngle, 24, color, 5);
            DrawLine(Vector2.Zero, Vector2.FromAngle(angle - combat.Data.SweepHalfAngle) * radius, color, 3);
            DrawLine(Vector2.Zero, Vector2.FromAngle(angle + combat.Data.SweepHalfAngle) * radius, color, 3);
            DrawArc(Vector2.Zero, radius * combat.Progress, angle - combat.Data.SweepHalfAngle, angle + combat.Data.SweepHalfAngle, 24, color, 2);
        }
        else if (combat.Phase == BossAttackPhase.AcidWindup)
        {
            for (int i = -1; i <= 1; i++)
                DrawLine(Vector2.Zero, Vector2.FromAngle(angle + i * 0.25f) * 450, new Color(0.4f, 1, 0.2f, 0.7f), 3);
        }
        else if (combat.Phase == BossAttackPhase.Recovery)
            DrawArc(Vector2.Zero, 65, 0, Mathf.Tau, 32, new Color(0.2f, 0.9f, 0.9f, 0.6f), 3);
    }
}
