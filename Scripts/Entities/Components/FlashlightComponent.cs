using Godot;

namespace TopDownDemo.Entities.Components;

[GlobalClass]
public partial class FlashlightComponent : Node2D
{
    [Export] public PointLight2D Light { get; set; }
    [Export] public float BaseEnergy { get; set; } = 1.35f;
    [Export] public bool EnableFlicker { get; set; } = true;

    public override void _Ready()
    {
        if (Light == null)
        {
            Light = GetNodeOrNull<PointLight2D>("%Flashlight");
        }
    }

    public void UpdateFlicker()
    {
        if (Light == null || !EnableFlicker) return;

        double t = Time.GetTicksMsec() * 0.003;
        Light.Energy = BaseEnergy + (float)Mathf.Sin(t) * 0.04f + (float)Mathf.Sin(t * 2.7) * 0.02f;
    }

    public void SetLightState(bool active, float dimEnergy = 0.2f)
    {
        if (Light == null) return;
        Light.Energy = active ? BaseEnergy : dimEnergy;
    }
}
