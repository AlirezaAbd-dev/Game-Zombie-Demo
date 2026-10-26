using System;
using Godot;

namespace TopDownDemo.Systems;

/// <summary>Stop live playback while the audio server still has time to drain it.</summary>
public static class ApplicationExit
{
    private static SceneTree _tree;
    private static bool _requested;

    public static void Initialize(SceneTree tree)
    {
        if (_tree == tree) return;
        _tree = tree;
        _requested = false;
        tree.AutoAcceptQuit = false;
        tree.Root.CloseRequested += () => Request(tree);
    }

    public static void Request(SceneTree tree)
    {
        Initialize(tree);
        if (_requested) return;
        _requested = true;
        Callable.From(() => Finish(tree)).CallDeferred();
    }

    private static async void Finish(SceneTree tree)
    {
        try
        {
            DialogueManager.Instance?.CancelDialogue();
            tree.Paused = true;
            ControlBindings.ReleaseGameplayActions();
            if (SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.ProcessMode = Node.ProcessModeEnum.Disabled;
            AudioManager.Instance?.StopAllPlayback();
            var scene = tree.CurrentScene;
            tree.CurrentScene = null;
            scene?.QueueFree();
            // Audio playback stops are processed asynchronously. Quitting in the
            // button's signal callback can destroy the server before those stops drain.
            await tree.ToSignal(tree.CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            tree.Quit();
        }
        catch (Exception error)
        {
            GD.PushError("Application shutdown failed: " + error);
            tree.Quit(1);
        }
    }
}
