using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;
using TopDownDemo.Entities.Components;

namespace TopDownDemo.Systems;

public partial class CampaignLootDirector : Node
{
    public static CampaignLootDirector Instance { get; private set; }
    private PackedScene _pickupScene;
    private readonly System.Collections.Generic.HashSet<string> _spawnedDrops = new();

    public override void _EnterTree() => Instance = this;
    public override void _Ready()
    {
        _pickupScene = GD.Load<PackedScene>("res://Prefabs/Pickup.tscn");
        CallDeferred(nameof(RestoreDrops));
    }

    private void RestoreDrops()
    {
        if (SaveSystem.Instance == null) return;
        string scenePath = GetTree().CurrentScene.SceneFilePath;
        foreach (var drop in SaveSystem.Instance.ActiveSaveData.WorldDrops)
            if (!drop.Value.Collected && drop.Value.ScenePath == scenePath) CreatePickup(drop.Key,drop.Value);
    }

    public void SpawnEnemyDrop(string enemyId, ItemData item, Vector2 position, int? ammoRounds = null)
    {
        if (SaveSystem.Instance == null || item == null || string.IsNullOrEmpty(item.ResourcePath)) return;
        string id = enemyId + "::loot";
        var data = SaveSystem.Instance.ActiveSaveData;
        if (data.WorldDrops.ContainsKey(id)) return;
        var record = new WorldDropData { ScenePath = GetTree().CurrentScene.SceneFilePath,
            ItemPath = item.ResourcePath, X = position.X, Y = position.Y,
            AmmoRoundsRemaining = ammoRounds ?? (item.Type == ItemType.Ammo ? (int)item.Value : null) };
        data.WorldDrops.Add(id,record);
        // Record immediately so a checkpoint in the death frame cannot lose deferred loot.
        Callable.From(() =>
        {
            if (IsInstanceValid(this) && IsInsideTree() && !record.Collected) CreatePickup(id,record);
        }).CallDeferred();
    }

    private void CreatePickup(string id,WorldDropData record)
    {
        if (_pickupScene == null || _spawnedDrops.Contains(id) || SaveSystem.Instance == null
            || !SaveSystem.Instance.ActiveSaveData.WorldDrops.TryGetValue(id,out record) || record.Collected
            || record.ScenePath != GetTree().CurrentScene.SceneFilePath || !ResourceLoader.Exists(record.ItemPath)) return;
        var item = GD.Load<ItemData>(record.ItemPath);
        if (item == null) return;
        if (item.Type == ItemType.Ammo && !record.AmmoRoundsRemaining.HasValue)
            record.AmmoRoundsRemaining = item.LegacyAmmoRounds > 0 ? item.LegacyAmmoRounds : (int)item.Value;
        if (item.Type == ItemType.Ammo && record.AmmoRoundsRemaining == 0) { record.Collected = true; return; }
        var pickup = _pickupScene.Instantiate<Pickup>();
        pickup.Item = item;
        pickup.AddChild(new PersistentDropComponent { DropId = id });
        _spawnedDrops.Add(id);
        GetTree().CurrentScene.AddChild(pickup);
        pickup.GlobalPosition = new Vector2(record.X,record.Y);
    }

    public override void _ExitTree() { if (Instance == this) Instance = null; }
}
