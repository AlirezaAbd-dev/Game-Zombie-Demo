using System.Collections.Generic;
using Godot;

public partial class DecalManager : Node2D
{
    public static DecalManager? Instance { get; private set; }

    [Export] public int MaxDecals { get; set; } = 200;

    private readonly Queue<Node2D> _decalQueue = new();

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }
        Instance = this;
        ZIndex = -1;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SpawnBloodSplatter(Vector2 globalPos, Vector2 direction)
    {
        int vertexCount = (int)GD.RandRange(12, 16);
        Vector2[] points = new Vector2[vertexCount];
        Vector2 dir = direction.LengthSquared() > 0.01f ? direction.Normalized() : Vector2.Right;
        float baseAngle = dir.Angle();

        for (int i = 0; i < vertexCount; i++)
        {
            float angle = (i / (float)vertexCount) * Mathf.Tau;
            float alignment = Mathf.Max(0f, Mathf.Cos(angle - baseAngle));
            float r = (float)GD.RandRange(8.0, 18.0) + alignment * (float)GD.RandRange(8.0, 24.0);
            points[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }

        var poly = new Polygon2D
        {
            Polygon = points,
            Color = new Color(0.38f, 0.04f, 0.06f, 0.75f),
            GlobalPosition = globalPos,
            ZIndex = -1
        };

        AddChild(poly);
        EnqueueDecal(poly);
    }

    public void SpawnScorchMark(Vector2 globalPos, float radius)
    {
        int vertexCount = (int)GD.RandRange(14, 18);
        Vector2[] points = new Vector2[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            float angle = (i / (float)vertexCount) * Mathf.Tau;
            float r = radius * (float)GD.RandRange(0.75, 1.25);
            points[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }

        var poly = new Polygon2D
        {
            Polygon = points,
            Color = new Color(0.05f, 0.05f, 0.07f, 0.82f),
            GlobalPosition = globalPos,
            ZIndex = -1
        };

        AddChild(poly);
        EnqueueDecal(poly);
    }

    private void EnqueueDecal(Node2D decal)
    {
        _decalQueue.Enqueue(decal);
        while (_decalQueue.Count > MaxDecals)
        {
            var old = _decalQueue.Dequeue();
            if (IsInstanceValid(old))
            {
                old.QueueFree();
            }
        }
    }

    public void ClearDecals()
    {
        while (_decalQueue.Count > 0)
        {
            var decal = _decalQueue.Dequeue();
            if (IsInstanceValid(decal))
            {
                decal.QueueFree();
            }
        }
    }
}
