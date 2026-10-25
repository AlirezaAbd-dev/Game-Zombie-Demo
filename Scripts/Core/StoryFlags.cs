using System;

namespace TopDownDemo.Core;

public enum DifficultyLevel
{
    Survivor = 0,    // Normal: 1.0x HP, Normal ammo, 1.0x Enemy damage, Generous stealth
    Nightmare = 1,   // Hard: 0.75x HP, -40% ammo, 1.5x Enemy damage, +30% enemies, Strict stealth
    Researcher = 2   // Extreme: 0.5x HP, -60% ammo, 2.0x Enemy damage, +60% enemies, Unforgiving
}

public enum StoryAct
{
    None = 0,
    Act1_Arrival = 1,
    Act2_Bunker = 2,
    Act3_Biocontainment = 3,
    Act4_Descent = 4,
    Act5_Extraction = 5,
    Credits = 6
}

public enum EndingType
{
    None = 0,
    Escape_A = 1,       // Defeat Unit-9 + Trust Sadeghi + Leila survived (Bittersweet hope)
    BurnItDown_B = 2,   // Defeat Unit-9 + Distrust Sadeghi (Pyrrhic victory)
    TheWeight_C = 3,    // Leila dies + Trust Sadeghi (Somber guilt)
    ArchivistTruth_D = 4 // Secret Ending: Archivist journal + Ghorbani conspiracy files (Government truth)
}

public static class StoryFlags
{
    // Act 1 Flags
    public const string GATE_OVERRIDDEN = "gate_overridden";
    public const string FACILITY_POWER_RESTORED = "facility_power_restored";

    // Act 2 Flags
    public const string SADEGHI_FIRST_CONTACT = "sadeghi_first_contact";
    public const string SADEGHI_WARM = "sadeghi_warm";
    public const string SADEGHI_COLD = "sadeghi_cold";

    // Act 3 Flags
    public const string LEILA_NOTE_FOUND = "leila_note_found";
    public const string BIOLAB_KEYCARD_FOUND = "biolab_keycard_found";
    public const string LEILA_RESCUED = "leila_rescued";
    public const string LEILA_ABANDONED = "leila_abandoned";
    public const string FOUND_ARCHIVIST = "found_archivist";
    public const string ARCHIVIST_JOURNAL_COLLECTED = "archivist_journal_collected";

    // Act 4 Flags
    public const string GHORBANI_FILES_FOUND = "ghorbani_files_found";
    public const string OUTBREAK_PROTOCOL_REVEALED = "outbreak_protocol_revealed";
    public const string SADEGHI_TRUSTED = "sadeghi_trusted";
    public const string SADEGHI_DISTRUSTED = "sadeghi_distrusted";
    public const string SECRET_CONSPIRACY_UNLOCKED = "secret_conspiracy_unlocked";
    public const string SADEGHI_CONFRONTED = "sadeghi_confronted";

    // Act 5 & Endings
    public const string UNIT9_ENCOUNTERED = "unit9_encountered";
    public const string UNIT9_DEFEATED = "unit9_defeated";
    public const string ENDING_TRIGGERED = "ending_triggered";
    public const string ACTIVE_ENDING = "active_ending";
}
