using Godot;
using TopDownDemo.Systems;

namespace TopDownDemo.Entities.Components;

/// <summary>Opt-in persistence. IDs are authored and scoped to the containing level.</summary>
public partial class WorldStateComponent : Node
{
    [Export] public string StableId { get; set; } = string.Empty;
    public string StateKey => GetTree().CurrentScene.SceneFilePath + "::" + StableId;

    public override void _Ready()
    {
        if (string.IsNullOrWhiteSpace(StableId))
        {
            GD.PushError($"World state requires a stable ID: {GetPath()}");
            return;
        }
        AddToGroup("persistent_world_objects");
        if (GetParent() is Zombie enemy) enemy.PersistentDropId = StateKey;
        GetParent().Ready += BindAndRestore;
    }

    private void BindAndRestore()
    {
        var data = SaveSystem.Instance?.ActiveSaveData;
        if (data == null) return;
        bool completed = data.CompletedWorldObjects.Contains(StateKey);
        switch (GetParent())
        {
            case Pickup pickup:
                pickup.Collected += MarkCompleted;
                pickup.AmmoRemainingChanged += OnAmmoRemainingChanged;
                if (!completed && data.PickupAmmoRemaining.TryGetValue(StateKey,out int remaining))
                {
                    pickup.RestoreAmmoRounds(remaining);
                    if (remaining <= 0) { MarkCompleted(); completed = true; }
                }
                if (completed) pickup.QueueFree();
                break;
            case Zombie zombie:
                zombie.Died += MarkCompleted;
                if (completed) zombie.QueueFree();
                else if (data.EnemyHealth.TryGetValue(StateKey, out float hp))
                    zombie.HealthComponent.SetCurrentHealth(hp);
                break;
            case BlastDoor door:
                door.DoorOpened += MarkCompleted;
                if (completed) door.RestoreOpen();
                break;
            case PowerGenerator generator:
                generator.GeneratorActivated += MarkCompleted;
                if (completed) generator.RestoreOnline();
                break;
            case Boss_Unit9 boss:
                boss.BossDied += MarkCompleted;
                if (completed) boss.QueueFree();
                else if (data.EnemyHealth.TryGetValue(StateKey, out float bossHp))
                    boss.HealthComponent.SetCurrentHealth(bossHp);
                break;
        }
    }

    private void MarkCompleted()
    {
        SaveSystem.Instance?.ActiveSaveData.CompletedWorldObjects.Add(StateKey);
    }

    private void OnAmmoRemainingChanged(int remaining)
    {
        if (SaveSystem.Instance != null) SaveSystem.Instance.ActiveSaveData.PickupAmmoRemaining[StateKey] = remaining;
    }

    public void Capture()
    {
        if (GetParent() is Zombie liveZombie && !liveZombie.IsDead && SaveSystem.Instance != null)
            SaveSystem.Instance.ActiveSaveData.EnemyHealth[StateKey] = liveZombie.HealthComponent.CurrentHealth;
        if (GetParent() is Boss_Unit9 boss && !boss.IsDead && SaveSystem.Instance != null)
            SaveSystem.Instance.ActiveSaveData.EnemyHealth[StateKey] = boss.HealthComponent.CurrentHealth;
    }
}
