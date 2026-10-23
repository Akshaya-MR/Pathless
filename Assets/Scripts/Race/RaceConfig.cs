using System;
using UnityEngine;

namespace TheRacer
{
    /// <summary>Runtime race tuning shared by the player and two active rivals.</summary>
    [Serializable]
    public sealed class RaceConfig
    {
        [Serializable]
        public sealed class RaceDefinition
        {
            public string name = "NEON GUTTERS";
            [TextArea(1, 2)] public string briefing = "Run the gutters. Own the night.";
            [Min(1)] public int laps = 2;
            [Range(0f, 1.5f)] public float rivalSkill = 0.85f;
            [Min(0)] public int seed = 2087;
            [Range(0f, 1f)] public float rainChance;
        }

        [Min(100f)] public float raceDistance = 1500f;
        [Min(0.1f)] public float rivalSkillMultiplier = 1f;
        [Min(5f)] public float rubberBandDistance = 55f;
        [Range(0f, 0.06f)] public float rubberBandStrength = 0.06f;
        public RaceDefinition[] races =
        {
            new RaceDefinition { name = "NEON GUTTERS", briefing = "Run the gutters. Own the night.", laps = 2, rivalSkill = 0.85f, seed = 2087 },
            new RaceDefinition { name = "CHROME VEINS", briefing = "Cut through chrome and concrete.", laps = 3, rivalSkill = 1f, seed = 3099 },
            new RaceDefinition { name = "THE SPIRE RUN", briefing = "Climb higher. Come back alive.", laps = 3, rivalSkill = 1.1f, seed = 4111 }
        };
    }
}
