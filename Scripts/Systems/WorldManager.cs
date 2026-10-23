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
        if (phase == StoryPhase.Act1_Blackout)
            return; // In Act 1, only pre-placed zombies exist so player can explore without unfair spawns

        Vector2? clearSpawnPos = FindClearSpawnPosition();
        if (!clearSpawnPos.HasValue)
            return;

        Vector2 spawnPos = clearSpawnPos.Value;
        TriggerSpawnEmergenceVfx(spawnPos);

        var zombie = ZombieScene.Instantiate<Zombie>();
        zombie.GlobalPosition = spawnPos;

        // Emergence visual fade-in
        zombie.Modulate = new Color(1, 1, 1, 0.2f);
        var fadeInTween = zombie.CreateTween();
        fadeInTween.TweenProperty(zombie, "modulate:a", 1.0f, 0.35);

        EnemyStats chosenStats = WalkerStats;
        if (phase == StoryPhase.Act2_TerminalDefense)
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

    private void TriggerSpawnEmergenceVfx(Vector2 pos)
    {
        // Metallic vent clatter / grate break audio
        AudioManager.Instance?.PlaySound(SoundType.VentBreak, pos, (float)GD.RandRange(0.9, 1.1));

        // Dust & steam burst particles
        var dust = new CpuParticles2D
        {
            GlobalPosition = pos,
            Emitting = true,
            OneShot = true,
            Explosiveness = 0.90f,
            Amount = 16,
            Lifetime = 0.40f,
            Spread = 180.0f,
            InitialVelocityMin = 80.0f,
            InitialVelocityMax = 180.0f,
            ScaleAmountMin = 3.0f,
            ScaleAmountMax = 6.0f,
            Color = new Color(0.65f, 0.65f, 0.70f, 0.75f)
        };
        AddChild(dust);

        var timer = GetTree().CreateTimer(0.6f);
        timer.Timeout += () => dust.QueueFree();
    }

    // =========================================================================
    // SECTOR PLAYABLE GEOMETRY & BOUNDS
    // =========================================================================
    public static readonly Rect2 Sector1Bounds = new Rect2(180, 190, 840, 380);
    public static readonly Rect2 Sector2Bounds = new Rect2(1080, 190, 840, 320);
    public static readonly Rect2 Sector3Bounds = new Rect2(180, -530, 840, 660);
    public static readonly Rect2 Sector4Bounds = new Rect2(180, -1570, 840, 980);

    public static bool IsInsidePlayableArea(Vector2 pos, float margin = 0.0f)
    {
        if (Sector1Bounds.Grow(-margin).HasPoint(pos)) return true;
        if (Sector2Bounds.Grow(-margin).HasPoint(pos)) return true;
        if (Sector3Bounds.Grow(-margin).HasPoint(pos)) return true;
        if (Sector4Bounds.Grow(-margin).HasPoint(pos)) return true;

        // Corridor between Sector 1 and Sector 3 (around blast door at 600, 160) - 180px gateway
        Rect2 corridorS1S3 = new Rect2(490, 115, 220, 90);
        if (corridorS1S3.Grow(-margin).HasPoint(pos)) return true;

        // Corridor between Sector 3 and Sector 4 (around cargo gate at 600, -560) - 180px gateway
        Rect2 corridorS3S4 = new Rect2(490, -605, 220, 90);
        if (corridorS3S4.Grow(-margin).HasPoint(pos)) return true;

        // Doorway between Sector 1 and Sector 2 (around blast door at 1050, 350) - 180px gateway
        Rect2 doorS1S2 = new Rect2(1005, 250, 90, 200);
        if (doorS1S2.Grow(-margin).HasPoint(pos)) return true;

        return false;
    }

    public static Vector2 ClampToPlayableBounds(Vector2 pos)
    {
        if (IsInsidePlayableArea(pos))
            return pos;

        if (pos.Y < -560.0f)
        {
            // Sector 4: Helipad Arena
            return new Vector2(
                Mathf.Clamp(pos.X, Sector4Bounds.Position.X + 25.0f, Sector4Bounds.End.X - 25.0f),
                Mathf.Clamp(pos.Y, Sector4Bounds.Position.Y + 25.0f, Sector4Bounds.End.Y - 25.0f)
            );
        }
        else if (pos.Y < 160.0f)
        {
            // Sector 3: Central Bio-Lab
            return new Vector2(
                Mathf.Clamp(pos.X, Sector3Bounds.Position.X + 25.0f, Sector3Bounds.End.X - 25.0f),
                Mathf.Clamp(pos.Y, Sector3Bounds.Position.Y + 25.0f, Sector3Bounds.End.Y - 25.0f)
            );
        }
        else
        {
            if (pos.X > 1050.0f)
            {
                // Sector 2: Auxiliary Substation
                return new Vector2(
                    Mathf.Clamp(pos.X, Sector2Bounds.Position.X + 25.0f, Sector2Bounds.End.X - 25.0f),
                    Mathf.Clamp(pos.Y, Sector2Bounds.Position.Y + 25.0f, Sector2Bounds.End.Y - 25.0f)
                );
            }
            else
            {
                // Sector 1: Checkpoint
                return new Vector2(
                    Mathf.Clamp(pos.X, Sector1Bounds.Position.X + 25.0f, Sector1Bounds.End.X - 25.0f),
                    Mathf.Clamp(pos.Y, Sector1Bounds.Position.Y + 25.0f, Sector1Bounds.End.Y - 25.0f)
                );
            }
        }
    }

    private Vector2? FindClearSpawnPosition()
    {
        if (!IsInstanceValid(_player)) return null;

        var phase = StoryDirector.Instance?.CurrentPhase ?? StoryPhase.Act1_Blackout;
        Vector2 playerPos = _player.GlobalPosition;

        // Choose eligible sectors based on current narrative act and unlocked doors
        var eligibleSectors = new System.Collections.Generic.List<Rect2>();

        if (phase == StoryPhase.Act3_HelipadEvacuation || phase == StoryPhase.Act3_BossBattle)
        {
            // Act 3: Helipad Arena
            eligibleSectors.Add(Sector4Bounds);
        }
        else if (phase == StoryPhase.Act2_TerminalDefense)
        {
            // Act 2 Defense: Bio-Lab & South hallway
            eligibleSectors.Add(Sector3Bounds);
            eligibleSectors.Add(Sector1Bounds);
        }
        else if (phase == StoryPhase.Act2_ContainmentBreach || phase == StoryPhase.Act1_PowerRestored)
        {
            // Act 2 Exploration: Bio-Lab, Substation, Spawn room
            eligibleSectors.Add(Sector3Bounds);
            eligibleSectors.Add(Sector2Bounds);
            eligibleSectors.Add(Sector1Bounds);
        }
        else
        {
            // Act 1 Blackout: hand-placed zombies only
            return null;
        }

        // 1. Prioritize Infiltration Points (Vent Shafts & Breach Points)
        var ventNodes = GetTree().GetNodesInGroup("infiltration_point");
        var candidateVents = new System.Collections.Generic.List<Vector2>();

        foreach (var node in ventNodes)
        {
            if (node is Node2D n2d && IsInstanceValid(n2d))
            {
                Vector2 vPos = n2d.GlobalPosition;
                bool inSector = false;
                foreach (var sector in eligibleSectors)
                {
                    if (sector.HasPoint(vPos))
                    {
                        inSector = true;
                        break;
                    }
                }

                if (inSector && vPos.DistanceTo(playerPos) >= 160.0f)
                {
                    if (phase == StoryPhase.Act2_TerminalDefense && vPos.DistanceTo(new Vector2(600, -250)) < 150.0f)
                        continue;

                    candidateVents.Add(vPos);
                }
            }
        }

        if (candidateVents.Count > 0)
        {
            int idx = (int)(GD.Randi() % candidateVents.Count);
            Vector2 chosenVent = candidateVents[idx];
            Vector2 jittered = chosenVent + new Vector2((float)GD.RandRange(-15, 15), (float)GD.RandRange(-15, 15));
            return jittered;
        }

        // 2. Fallback to clear sector area
        for (int i = 0; i < 20; i++)
        {
            Rect2 targetSector = eligibleSectors[(int)(GD.Randi() % eligibleSectors.Count)];

            float candX = (float)GD.RandRange(targetSector.Position.X + 45.0f, targetSector.End.X - 45.0f);
            float candY = (float)GD.RandRange(targetSector.Position.Y + 45.0f, targetSector.End.Y - 45.0f);
            Vector2 candidate = new Vector2(candX, candY);

            if (candidate.DistanceTo(playerPos) < 220.0f)
                continue;

            if (phase == StoryPhase.Act2_TerminalDefense && candidate.DistanceTo(new Vector2(600, -250)) < 160.0f)
                continue;

            if (IsPositionClear(candidate, 40.0f))
            {
                return candidate;
            }
        }

        return null;
    }

    private bool IsPositionClear(Vector2 pos, float radius = 45.0f)
    {
        var spaceState = GetWorld2D()?.DirectSpaceState;
        if (spaceState == null) return true;

        var shapeParams = new PhysicsShapeQueryParameters2D
        {
            Shape = new CircleShape2D { Radius = radius },
            Transform = new Transform2D(0.0f, pos),
            CollisionMask = 1 // Obstacles layer
        };

        var results = spaceState.IntersectShape(shapeParams, 1);
        return results.Count == 0;
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
