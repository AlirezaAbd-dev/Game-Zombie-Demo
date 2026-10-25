using System;
using System.Collections.Generic;
using Godot;

namespace TopDownDemo.Core;

public static class DialogueDatabase
{
    private static readonly Dictionary<string, DialogueSequence> _sequences = new();

    static DialogueDatabase()
    {
        RegisterAllSequences();
    }

    public static DialogueSequence Get(string sequenceId)
    {
        if (_sequences.TryGetValue(sequenceId, out var seq))
        {
            return seq;
        }
        GD.PrintErr($"[DialogueDatabase] Sequence not found: {sequenceId}");
        return null;
    }

    private static void RegisterAllSequences()
    {
        // Act 1: Gate Arrival Intro
        var act1Intro = new DialogueSequence { SequenceId = "act1_intro" };
        act1Intro.Lines.Add(new DialogueLineResource
        {
            Speaker = "HQ COMMS",
            PortraitKey = "radio",
            Text = "Extraction Team Leader Nazari, satellite telemetry shows zero radio activity from Foxtrot-7 for twelve hours.",
        });
        act1Intro.Lines.Add(new DialogueLineResource
        {
            Speaker = "HQ COMMS",
            PortraitKey = "radio",
            Text = "The facility is under emergency lockdown. Enter the perimeter, locate survivors, and secure the biocontainment wing.",
        });
        act1Intro.Lines.Add(new DialogueLineResource
        {
            Speaker = "SARA",
            PortraitKey = "sara",
            Text = "Copy HQ. The storm is worsening. I'm approaching the security gatehouse now.",
        });
        _sequences["act1_intro"] = act1Intro;

        // Act 2: Sadeghi First Contact
        var act2Sadeghi = new DialogueSequence { SequenceId = "act2_sadeghi" };
        act2Sadeghi.Lines.Add(new DialogueLineResource
        {
            Speaker = "DR. SADEGHI",
            PortraitKey = "sadeghi",
            Text = "...someone there? Hello? This is Dr. Karim Sadeghi. I'm trapped in the archive subfloor. Who is this?!",
        });

        var act2ChoiceLine = new DialogueLineResource
        {
            Speaker = "SARA",
            PortraitKey = "sara",
            Text = "Radio transmission received. How do you respond?",
        };
        act2ChoiceLine.Choices.Add(new DialogueChoiceResource
        {
            ChoiceText = "Agent Nazari, Extraction team. How many survivors?",
            SetStoryFlag = StoryFlags.SADEGHI_WARM,
            NextLineIndex = 2
        });
        act2ChoiceLine.Choices.Add(new DialogueChoiceResource
        {
            ChoiceText = "Identify yourself. What happened to the security staff?",
            SetStoryFlag = StoryFlags.SADEGHI_COLD,
            NextLineIndex = 3
        });
        act2Sadeghi.Lines.Add(act2ChoiceLine);

        // Warm branch response (index 2)
        act2Sadeghi.Lines.Add(new DialogueLineResource
        {
            Speaker = "DR. SADEGHI",
            PortraitKey = "sadeghi",
            Text = "Sara... it's really you. Thank God. The guards didn't stand a chance. The test subjects broke containment in Sector 3.",
            NextLineIndex = 4
        });

        // Cold branch response (index 3)
        act2Sadeghi.Lines.Add(new DialogueLineResource
        {
            Speaker = "DR. SADEGHI",
            PortraitKey = "sadeghi",
            Text = "They're... gone. All of them. Please, Sara, there's no time for interrogations. They are hunting anything that breathes.",
            NextLineIndex = 4
        });

        // Common continuation (index 4)
        act2Sadeghi.Lines.Add(new DialogueLineResource
        {
            Speaker = "DR. SADEGHI",
            PortraitKey = "sadeghi",
            Text = "Head down through the guard barracks to reach the Biocontainment elevator. Be careful... they hear every footstep.",
        });
        _sequences["act2_sadeghi"] = act2Sadeghi;

        // Act 3: Finding Leila
        var act3Leila = new DialogueSequence { SequenceId = "act3_leila" };
        act3Leila.Lines.Add(new DialogueLineResource
        {
            Speaker = "SARA",
            PortraitKey = "sara",
            Text = "Found blood-stained research notes on the terminal. Junior researcher Leila Mohammadi was barricading blast door C.",
        });
        act3Leila.Lines.Add(new DialogueLineResource
        {
            Speaker = "DR. SADEGHI",
            PortraitKey = "sadeghi",
            Text = "Leila was my best student! Sara, her vitals are still showing on internal comms. She's trapped behind blast door C!",
        });

        var act3ChoiceLine = new DialogueLineResource
        {
            Speaker = "SARA",
            PortraitKey = "sara",
            Text = "Leila is trapped without oxygen filtration. What is your call?",
        };
        act3ChoiceLine.Choices.Add(new DialogueChoiceResource
        {
            ChoiceText = "Tell me where the keycard is. We are not leaving anyone behind.",
            SetStoryFlag = StoryFlags.LEILA_RESCUED,
            NextLineIndex = 3
        });
        act3ChoiceLine.Choices.Add(new DialogueChoiceResource
        {
            ChoiceText = "She's outside mission parameters. We secure the core first.",
            SetStoryFlag = StoryFlags.LEILA_ABANDONED,
            NextLineIndex = 4
        });
        act3Leila.Lines.Add(act3ChoiceLine);

        // Rescue branch (index 3)
        act3Leila.Lines.Add(new DialogueLineResource
        {
            Speaker = "DR. SADEGHI",
            PortraitKey = "sadeghi",
            Text = "Good. I knew your conscience wouldn't let you leave her. The Level-3 Biolab Keycard is in the security lockbox in the eastern archive.",
            NextLineIndex = 5
        });

        // Abandon branch (index 4)
        act3Leila.Lines.Add(new DialogueLineResource
        {
            Speaker = "DR. SADEGHI",
            PortraitKey = "sadeghi",
            Text = "...Understood. (Silence) If that's what orders dictate... God have mercy on us.",
            NextLineIndex = 5
        });

        act3Leila.Lines.Add(new DialogueLineResource
        {
            Speaker = "SARA",
            PortraitKey = "sara",
            Text = "Moving out to the archive sector.",
        });
        _sequences["act3_leila"] = act3Leila;

        // Act 4: Sadeghi Betrayal Revelation
        var act4Betrayal = new DialogueSequence { SequenceId = "act4_betrayal" };
        act4Betrayal.Lines.Add(new DialogueLineResource
        {
            Speaker = "SARA",
            PortraitKey = "sara",
            Text = "Sadeghi. I downloaded Outbreak Protocol Seven from the command mainframe. Containment didn't fail. You released them.",
        });
        act4Betrayal.Lines.Add(new DialogueLineResource
        {
            Speaker = "DR. SADEGHI",
            PortraitKey = "sadeghi",
            Text = "Sara... you don't understand the pressure Commander Ghorbani put on my research! He threatened to execute my daughter!",
        });

        var act4ChoiceLine = new DialogueLineResource
        {
            Speaker = "SARA",
            PortraitKey = "sara",
            Text = "The evidence of unauthorized biological warfare trials is in your hands.",
        };
        act4ChoiceLine.Choices.Add(new DialogueChoiceResource
        {
            ChoiceText = "I'm not your executioner. Head to the extraction helipad.",
            SetStoryFlag = StoryFlags.SADEGHI_TRUSTED,
            NextLineIndex = 3
        });
        act4ChoiceLine.Choices.Add(new DialogueChoiceResource
        {
            ChoiceText = "You orchestrated a massacre. You stay right here.",
            SetStoryFlag = StoryFlags.SADEGHI_DISTRUSTED,
            NextLineIndex = 4
        });
        act4ChoiceLine.Choices.Add(new DialogueChoiceResource
        {
            ChoiceText = "I found Ghorbani's secret orders. I know you were coerced. (Secret Ending)",
            SetStoryFlag = StoryFlags.SECRET_CONSPIRACY_UNLOCKED,
            RequiredFlag = StoryFlags.GHORBANI_FILES_FOUND,
            NextLineIndex = 5
        });
        act4Betrayal.Lines.Add(act4ChoiceLine);

        // Trust branch (index 3)
        act4Betrayal.Lines.Add(new DialogueLineResource
        {
            Speaker = "DR. SADEGHI",
            PortraitKey = "sadeghi",
            Text = "Thank you, Sara. I will disable the missile silo containment field from the console to help you reach the surface.",
            NextLineIndex = 6
        });

        // Distrust branch (index 4)
        act4Betrayal.Lines.Add(new DialogueLineResource
        {
            Speaker = "DR. SADEGHI",
            PortraitKey = "sadeghi",
            Text = "(Trembling) Then there's nothing left for me... but Unit-9 won't let either of us leave alive.",
            NextLineIndex = 6
        });

        // Secret conspiracy branch (index 5)
        act4Betrayal.Lines.Add(new DialogueLineResource
        {
            Speaker = "DR. SADEGHI",
            PortraitKey = "sadeghi",
            Text = "You... you recovered Ghorbani's personal black dossier?! The Ministry never knew the truth. We can expose the entire shadow project!",
            NextLineIndex = 6
        });

        act4Betrayal.Lines.Add(new DialogueLineResource
        {
            Speaker = "SARA",
            PortraitKey = "sara",
            Text = "Alert: Heavy seismic impact detected in the lower silo. Unit-9 is breaking free!",
        });
        _sequences["act4_betrayal"] = act4Betrayal;

        // Act 5: Boss Unit-9 Encounter
        var act5Boss = new DialogueSequence { SequenceId = "act5_boss" };
        act5Boss.Lines.Add(new DialogueLineResource
        {
            Speaker = "SARA",
            PortraitKey = "sara",
            Text = "The containment cylinder is completely shattered... Unit-9 is awake!",
        });
        act5Boss.Lines.Add(new DialogueLineResource
        {
            Speaker = "DR. SADEGHI",
            PortraitKey = "sadeghi",
            Text = "Aim for the exposed mutagen core in its upper torso! It will release corrosive bile when damaged!",
        });
        _sequences["act5_boss"] = act5Boss;
    }
}
