using Godot;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities.Components;

public partial class PersistentDropComponent : Node
{
    [Export] public string DropId { get; set; } = string.Empty;
    public override void _Ready()
    {
        if (GetParent() is not Pickup pickup || string.IsNullOrEmpty(DropId))
        { GD.PushError("Persistent loot requires a pickup and stable drop ID."); return; }
        pickup.Collected += OnCollected;
        pickup.AmmoRemainingChanged += OnAmmoRemainingChanged;
        if (SaveSystem.Instance?.ActiveSaveData.WorldDrops.TryGetValue(DropId,out var record) == true
            && record.AmmoRoundsRemaining.HasValue) pickup.RestoreAmmoRounds(record.AmmoRoundsRemaining.Value);
    }
    private void OnAmmoRemainingChanged(int remaining)
    {
        if (SaveSystem.Instance?.ActiveSaveData.WorldDrops.TryGetValue(DropId,out var record) == true)
            record.AmmoRoundsRemaining = remaining;
    }
    private void OnCollected()
    {
        if (SaveSystem.Instance?.ActiveSaveData.WorldDrops.TryGetValue(DropId,out var record) == true)
            record.Collected = true;
    }
}
