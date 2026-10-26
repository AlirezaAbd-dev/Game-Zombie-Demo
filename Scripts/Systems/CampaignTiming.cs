using TopDownDemo.Core;

namespace TopDownDemo.Systems;

/// <summary>Checkpoint-based pacing counters; never used as a gameplay cooldown clock.</summary>
public static class CampaignTiming
{
    public static void Record(string scenePath,float delta,bool dialogue = false)
    {
        var data = SaveSystem.Instance?.ActiveSaveData;
        if (data == null || string.IsNullOrEmpty(scenePath) || delta <= 0 || !float.IsFinite(delta)) return;
        if (!data.SceneTimings.TryGetValue(scenePath,out var timing))
        { timing = new SceneTimingData(); data.SceneTimings.Add(scenePath,timing); }
        if (dialogue) { timing.DialogueSeconds += delta; data.DialoguePlaytimeSeconds += delta; }
        else timing.ActiveSeconds += delta;
    }
}
