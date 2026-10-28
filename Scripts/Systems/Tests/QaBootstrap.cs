using System;
using Godot;

namespace TopDownDemo.Systems.Tests;

/// <summary>Release templates disable CLI scene overrides. QA exports dispatch a bounded suite list instead.</summary>
public partial class QaBootstrap : Node
{
    private static bool _dispatched;
    public override void _Ready()
    {
        if (_dispatched || !OS.HasFeature("qa")) return;
        string argument = Array.Find(OS.GetCmdlineUserArgs(), value => value.StartsWith("--qa-suite=", StringComparison.Ordinal));
        if (argument == null) return;
        _dispatched = true;
        string suite = argument["--qa-suite=".Length..];
        string scene = suite switch
        {
            "Environment" or "Progression" or "Settings" or "Controls" or "Dialogue" or "Audio"
                or "Weapon" or "Persistence" or "Supply" or "Combat" or "Timing" or "Navigation" or "Campaign" or "Exit"
                => $"res://Scenes/Tests/{suite}Smoke.tscn",
            "FreshGate" => "res://Scenes/Tests/FreshGatePlaytest.tscn",
            _ => null
        };
        Callable.From(() =>
        {
            if (scene == null)
            {
                GD.PrintErr("Unknown QA suite: " + suite);
                GetTree().Quit(2);
                return;
            }
            GD.Print("PACKAGED QA SUITE: " + suite);
            if (GetTree().ChangeSceneToFile(scene) != Error.Ok) GetTree().Quit(1);
        }).CallDeferred();
    }
}
