using System.Collections.Generic;
using Godot;
using TopDownDemo.Entities;
using TopDownDemo.Core;

namespace TopDownDemo.Systems;

public partial class ProjectilePool : Node
{
    private static ProjectilePool _instance;
    public static ProjectilePool Instance
    {
        get => GodotObject.IsInstanceValid(_instance) ? _instance : null;
        private set => _instance = value;
    }

    [Export] public PackedScene BulletScene { get; set; }
    [Export] public int InitialPoolSize { get; set; } = 30;

    private readonly Queue<Bullet> _pool = new();

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public override void _Ready()
    {
        if (BulletScene == null)
        {
            BulletScene = GD.Load<PackedScene>("res://Prefabs/Bullet.tscn");
        }

        PrewarmPool();
    }

    private void PrewarmPool()
    {
        if (BulletScene == null) return;

        for (int i = 0; i < InitialPoolSize; i++)
        {
            var bullet = BulletScene.Instantiate<Bullet>();
            bullet.Visible = false;
            bullet.SetPhysicsProcess(false);
            bullet.SetProcess(false);
            AddChild(bullet);
            bullet.Deactivate();
            _pool.Enqueue(bullet);
        }
    }

    public Bullet Spawn(Vector2 globalPos, float globalRot, Node parent, float damage = 25.0f, float speed = 1400.0f, bool isEnemy = false, Vector2? launchOrigin = null, ShotReport shotReport = null)
    {
        Bullet bullet = null;
        while (_pool.Count > 0)
        {
            var candidate = _pool.Dequeue();
            if (IsInstanceValid(candidate))
            {
                bullet = candidate;
                break;
            }
        }

        if (bullet == null)
        {
            bullet = BulletScene.Instantiate<Bullet>();
        }
        else if (bullet.GetParent() != null)
        {
            bullet.GetParent().RemoveChild(bullet);
        }

        parent.AddChild(bullet);
        bullet.GlobalPosition = globalPos;
        bullet.GlobalRotation = globalRot;
        bullet.Activate(damage, speed, isEnemy, launchOrigin, shotReport);
        return bullet;
    }

    public void Despawn(Bullet bullet)
    {
        if (!IsInstanceValid(bullet) || !bullet.IsActive) return;

        bullet.Deactivate();
        // BodyEntered can run during physics-query flushing; return after that signal finishes.
        Callable.From(() =>
        {
            if (!IsInstanceValid(bullet) || !IsInsideTree()) return;
            if (bullet.GetParent() != null) bullet.Reparent(this);
            else AddChild(bullet);
            _pool.Enqueue(bullet);
        }).CallDeferred();
    }
}
