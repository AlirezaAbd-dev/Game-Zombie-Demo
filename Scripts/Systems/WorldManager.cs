using Godot;
using TopDownDemo.Entities;

namespace TopDownDemo.Systems;

public partial class WorldManager : Node2D
{
    [Export] public float SpawnInterval { get; set; } = 2.5f;
    [Export] public int MaxZombies { get; set; } = 25;
    [Export] public PackedScene ZombieScene { get; set; }

    private float _spawnTimer = 0.0f;
    private Player _player;

    public override void _Ready()
    {
        ZombieScene ??= GD.Load<PackedScene>("res://Prefabs/Zombie.tscn");
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_player) || _player.IsDead)
            return;

        _spawnTimer += (float)delta;
        if (_spawnTimer >= SpawnInterval)
        {
            _spawnTimer = 0.0f;
            SpawnZombieWave();
        }
    }

    private void SpawnZombieWave()
    {
        int currentZombies = GetTree().GetNodesInGroup("zombies").Count;
        if (currentZombies >= MaxZombies || ZombieScene == null)
            return;

        int spawnCount = (int)GD.RandRange(1, 2);
        for (int i = 0; i < spawnCount; i++)
        {
            if (!IsInstanceValid(_player)) break;

            float angle = (float)GD.RandRange(0.0, Mathf.Tau);
            float distance = (float)GD.RandRange(750.0, 950.0);
            Vector2 spawnPos = _player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            var zombie = ZombieScene.Instantiate<CharacterBody2D>();
            zombie.GlobalPosition = spawnPos;
            AddChild(zombie);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("quit"))
        {
            GetTree().Quit();
        }
        else if (@event.IsActionPressed("restart"))
        {
            if (_player == null || _player.IsDead)
            {
                GetTree().ReloadCurrentScene();
            }
        }
    }
}
