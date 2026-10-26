using System;
using System.Collections.Generic;

namespace TopDownDemo.Core;

public sealed class PlayerSettingsData
{
    public float MasterVolume { get; set; } = 1;
    public float MusicVolume { get; set; } = 1;
    public float EffectsVolume { get; set; } = 1;
    public float CameraShake { get; set; } = 1;
    public bool CameraLookAhead { get; set; } = true;
    public bool FlashlightFlicker { get; set; } = true;
    public bool Fullscreen { get; set; }
    public bool VSync { get; set; } = true;

    public Dictionary<string, int> Bindings { get; set; } = new();

    public PlayerSettingsData Copy()
    {
        var copy = (PlayerSettingsData)MemberwiseClone();
        copy.Bindings = Bindings == null ? new() : new(Bindings);
        return copy;
    }

    public void Normalize()
    {
        MasterVolume = Unit(MasterVolume);
        MusicVolume = Unit(MusicVolume);
        EffectsVolume = Unit(EffectsVolume);
        CameraShake = Unit(CameraShake);
    }

    private static float Unit(float value) => float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 1;
}
