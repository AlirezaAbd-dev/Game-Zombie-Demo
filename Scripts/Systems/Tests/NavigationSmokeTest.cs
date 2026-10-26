using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities;

namespace TopDownDemo.Systems.Tests;

public partial class NavigationSmokeTest : Node
{
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        var tree = GetTree();
        try
        {
            tree.CurrentScene = null;
            var saves = SaveSystem.EnsureInstance(this);
            saves.CreateNewGame(0,DifficultyLevel.Survivor);
            saves.ActiveSaveData.Flags.Add("unit9_released");
            saves.ActiveSaveData.Flags.Add("silo_backup_online");
            if (tree.ChangeSceneToFile("res://Scenes/Act5_Arena.tscn") != Error.Ok) throw new Exception("Scene failed to load.");
            for (int i = 0; i < 5; i++) await ToSignal(tree,SceneTree.SignalName.ProcessFrame);
            var player = tree.GetFirstNodeInGroup("player") as Player;
            var boss = tree.CurrentScene.GetNode<Boss_Unit9>("BossUnit9");
            foreach (Node node in tree.GetNodesInGroup("zombies")) if (node is Zombie zombie) zombie.SetPhysicsProcess(false);
            player.IsInputLocked = true; player.IsInvulnerable = true;
            player.GlobalPosition = new Vector2(700,1100);
            boss.GlobalPosition = new Vector2(350,1100);
            for (int frame = 0; frame < 480 && boss.GlobalPosition.DistanceTo(player.GlobalPosition) > 110; frame++)
            {
                await ToSignal(tree,SceneTree.SignalName.PhysicsFrame);
                await ToSignal(tree,SceneTree.SignalName.ProcessFrame);
            }
            if (boss.GlobalPosition.X <= 590 || boss.GlobalPosition.DistanceTo(player.GlobalPosition) > 110)
                throw new Exception($"Boss remained trapped behind silo column: {boss.GlobalPosition}.");
            GD.Print("PASS boss navigates around an authored column into attack range");
            tree.CurrentScene.QueueFree();
            await ToSignal(tree.CreateTimer(0.2),SceneTreeTimer.SignalName.Timeout);
            GD.Print("NAVIGATION SMOKE PASS"); tree.Quit();
        }
        catch (Exception error) { GD.PushError("NAVIGATION SMOKE FAIL: " + error); tree.Quit(1); }
    }
}
