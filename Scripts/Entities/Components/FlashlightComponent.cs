using Godot;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities.Components;

[GlobalClass]
public partial class FlashlightComponent : Node2D
{
    [Export] public PointLight2D Light { get; set; }
    [Export] public float BaseEnergy { get; set; } = 1.35f;
    [Export] public bool EnableFlicker { get; set; } = true;
    private bool _active = true;
    private float _dimEnergy = 0.2f;

    public override void _Ready()
    {
        if (Light == null)
        {
            Light = GetNodeOrNull<PointLight2D>("%Flashlight");
        }
    }

    public void UpdateFlicker()
    {
        if (Light == null) return;
        if (!_active) { Light.Energy = _dimEnergy; return; }
        if (!EnableFlicker || !PlayerSettings.Current.FlashlightFlicker) { Light.Energy = BaseEnergy; return; }

        double t = Time.GetTicksMsec() * 0.003;
        Light.Energy = BaseEnergy + (float)Mathf.Sin(t) * 0.04f + (float)Mathf.Sin(t * 2.7) * 0.02f;
    }

    public void SetLightState(bool active, float dimEnergy = 0.2f)
    {
        _active = active;
        _dimEnergy = dimEnergy;
        if (Light == null) return;
        Light.Energy = active ? BaseEnergy : dimEnergy;
    }
}
