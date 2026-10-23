using System;
using UnityEngine;

namespace TheRacer
{
    public enum AIPersonalityType { Aggressive, Cautious, ShortcutLover, Drifter, Balanced }

    /// <summary>Tuning values that shape a rival's line, pace, and risk tolerance.</summary>
    [Serializable]
    public sealed class AIPersonality
    {
        [Range(0f, 1f)] public float aggression;
        [Range(0.5f, 1.2f)] public float cornerSpeedFactor;
        [Range(0f, 1f)] public float driftiness;
        [Range(0f, 1f)] public float shortcutBias;
        [Min(2f)] public float lookAheadBase;
        [Range(0.75f, 1.2f)] public float topSpeedFactor;
        [Range(0f, 0.5f)] public float mistakeChance;

        /// <summary>Returns a fresh preset so racers never share mutable tuning data.</summary>
        public static AIPersonality CreatePreset(AIPersonalityType type)
        {
            switch (type)
            {
                case AIPersonalityType.Aggressive:
                    return new AIPersonality { aggression = 0.92f, cornerSpeedFactor = 0.98f, driftiness = 0.18f, shortcutBias = 0.2f, lookAheadBase = 9f, topSpeedFactor = 1.04f, mistakeChance = 0.08f };
                case AIPersonalityType.Cautious:
                    return new AIPersonality { aggression = 0.2f, cornerSpeedFactor = 0.78f, driftiness = 0.08f, shortcutBias = 0.05f, lookAheadBase = 15f, topSpeedFactor = 0.96f, mistakeChance = 0.025f };
                case AIPersonalityType.ShortcutLover:
                    return new AIPersonality { aggression = 0.58f, cornerSpeedFactor = 0.88f, driftiness = 0.24f, shortcutBias = 0.95f, lookAheadBase = 12f, topSpeedFactor = 1f, mistakeChance = 0.07f };
                case AIPersonalityType.Drifter:
                    return new AIPersonality { aggression = 0.52f, cornerSpeedFactor = 0.83f, driftiness = 0.88f, shortcutBias = 0.3f, lookAheadBase = 11f, topSpeedFactor = 0.99f, mistakeChance = 0.08f };
                default:
                    return new AIPersonality { aggression = 0.48f, cornerSpeedFactor = 0.9f, driftiness = 0.3f, shortcutBias = 0.25f, lookAheadBase = 12f, topSpeedFactor = 1f, mistakeChance = 0.04f };
            }
        }
    }
}
