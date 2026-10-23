using System;
using Godot;

namespace TopDownDemo.Core;

public enum StoryPhase
{
    Act1_Blackout,
    Act1_PowerRestored,
    Act2_ContainmentBreach,
    Act2_TerminalDefense,
    Act3_HelipadEvacuation,
    Act3_BossBattle,
    MissionCompleted,
    MissionFailed
}

public class RadioTransmission
{
    public string Speaker { get; set; } = "HQ COMMS";
    public string Message { get; set; } = string.Empty;
    public float Duration { get; set; } = 5.0f;
    public Color SpeakerColor { get; set; } = new Color(0.2f, 0.85f, 1.0f);
}

public class MissionStats
{
    public float PlaytimeSeconds { get; set; }
    public int TotalKills { get; set; }
    public int ShotsFired { get; set; }
    public int ShotsHit { get; set; }
    public int DocumentsFound { get; set; }

    public float AccuracyPercentage => ShotsFired > 0 ? (float)ShotsHit / ShotsFired * 100f : 0f;

    public string CalculateRank()
    {
        // Survival rank based on completion time and kills
        if (PlaytimeSeconds < 480f && TotalKills >= 35) return "S";
        if (PlaytimeSeconds < 660f && TotalKills >= 25) return "A";
        if (PlaytimeSeconds < 840f) return "B";
        return "C";
    }
}
