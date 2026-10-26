namespace TopDownDemo.Core;

/// <summary>Serializable loot earned from a persistent enemy, independent of its freed scene node.</summary>
public class WorldDropData
{
    public string ScenePath { get; set; } = string.Empty;
    public string ItemPath { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public bool Collected { get; set; }
    // Null preserves the full authored box for older saves.
    public int? AmmoRoundsRemaining { get; set; }
}
