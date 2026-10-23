using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;

namespace TopDownDemo.Systems;

public partial class WorldManager : Node2D
{
    [Export] public float SpawnInterval { get; set; } = 3.0f;
    [Export] public int MaxZombies { get; set; } = 15;
    [Export] public PackedScene ZombieScene { get; set; }

    [ExportGroup("Zombie Variants")]
    [Export] public EnemyStats WalkerStats { get; set; }
    [Export] public EnemyStats RunnerStats { get; set; }
    [Export] public EnemyStats BruteStats { get; set; }

    [ExportGroup("World Nodes")]
    [Export] public CanvasModulate AmbientCanvasModulate { get; set; }
    [Export] public Node2D BossSpawnPoint { get; set; }

    private float _spawnTimer = 0.0f;
    private Player _player;
    private bool _bossSpawned = false;

    public override void _Ready()
    {
        ZombieScene ??= GD.Load<PackedScene>("res://Prefabs/Zombie.tscn");
        WalkerStats ??= GD.Load<EnemyStats>("res://Resources/Enemies/Zombie_Walker.tres");
        RunnerStats ??= GD.Load<EnemyStats>("res://Resources/Enemies/Zombie_Runner.tres");
        BruteStats ??= GD.Load<EnemyStats>("res://Resources/Enemies/Zombie_Brute.tres");

        AmbientCanvasModulate ??= GetNodeOrNull<CanvasModulate>("%CanvasModulate");
        BossSpawnPoint ??= GetNodeOrNull<Node2D>("%BossSpawnPoint");

        _player = GetTree().GetFirstNodeInGroup("player") as Player;

        if (StoryDirector.Instance != null)
        {
            StoryDirector.Instance.FacilityPowerRestored += OnFacilityPowerRestored;
            StoryDirector.Instance.PhaseChanged += OnStoryPhaseChanged;
        }

        // Initial emergency lighting
        if (AmbientCanvasModulate != null)
        {
            AmbientCanvasModulate.Color = new Color(0.18f, 0.07f, 0.10f, 1.0f);
        }
    }

    private void OnFacilityPowerRestored()
    {
        if (AmbientCanvasModulate != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(AmbientCanvasModulate, "color", new Color(0.85f, 0.90f, 0.95f, 1.0f), 1.8);
        }
    }

    private void OnStoryPhaseChanged(int phaseVal)
    {
        var phase = (StoryPhase)phaseVal;
        switch (phase)
        {
            case StoryPhase.Act1_Blackout:
                SpawnInterval = 9.0f;
                MaxZombies = 4;
                break;
            case StoryPhase.Act2_ContainmentBreach:
                SpawnInterval = 4.0f;
                MaxZombies = 10;
                break;
            case StoryPhase.Act2_TerminalDefense:
                SpawnInterval = 1.8f;
                MaxZombies = 14;
                break;
            case StoryPhase.Act3_HelipadEvacuation:
                SpawnInterval = 5.0f;
                MaxZombies = 8;
                break;
            case StoryPhase.Act3_BossBattle:
                SpawnInterval = 6.0f;
                MaxZombies = 5;
                break;
            case StoryPhase.MissionCompleted:
                MaxZombies = 0;
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_player) || _player.IsDead)
            return;

        if (StoryDirector.Instance != null && StoryDirector.Instance.CurrentPhase == StoryPhase.MissionCompleted)
            return;

        // Check if player entered Helipad zone to trigger boss
        if (!_bossSpawned && StoryDirector.Instance?.CurrentPhase == StoryPhase.Act3_HelipadEvacuation)
        {
            if (BossSpawnPoint != null && _player.GlobalPosition.DistanceTo(BossSpawnPoint.GlobalPosition) < 700.0f)
            {
                _bossSpawned = true;
                StoryDirector.Instance.SpawnBossAtPosition(BossSpawnPoint.GlobalPosition);
            }
        }

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
        if (currentZombies >= MaxZombies || ZombieScene == null || !IsInstanceValid(_player))
            return;

        var phase = StoryDirector.Instance?.CurrentPhase ?? StoryPhase.Act1_Blackout;

        float angle = (float)GD.RandRange(0.0, Mathf.Tau);
        float distance = (float)GD.RandRange(750.0, 950.0);
        Vector2 spawnPos = _player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

        var zombie = ZombieScene.Instantiate<Zombie>();
        zombie.GlobalPosition = spawnPos;

        EnemyStats chosenStats = WalkerStats;
        if (phase == StoryPhase.Act1_Blackout)
        {
            chosenStats = WalkerStats;
        }
        else if (phase == StoryPhase.Act2_TerminalDefense)
        {
            chosenStats = (GD.Randf() < 0.65f) ? RunnerStats : WalkerStats;
        }
        else
        {
            float roll = GD.Randf();
            chosenStats = roll switch
            {
                < 0.50f => WalkerStats,
                < 0.85f => RunnerStats,
                _ => BruteStats
            };
        }

        zombie.Stats = chosenStats;
        AddChild(zombie);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("quit"))
        {
            GetTree().Quit();
        }
        else if (@event.IsActionPressed("restart"))
        {
            if (_player == null || _player.IsDead || (StoryDirector.Instance != null && StoryDirector.Instance.CurrentPhase == StoryPhase.MissionCompleted))
            {
                GetTree().ReloadCurrentScene();
            }
        }
    }
}
