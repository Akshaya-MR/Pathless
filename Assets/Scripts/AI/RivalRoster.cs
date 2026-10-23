using UnityEngine;

namespace TheRacer
{
    /// <summary>Named rival data; the current build activates the first two entries only.</summary>
    public static class RivalRoster
    {
        public const int ActiveRivalCount = 2;

        public static readonly RivalDefinition[] All =
        {
            new RivalDefinition(ThemeText.RivalViperHandle, ThemeText.RivalViperCrew, ThemeText.RivalViperTaunt,
                ThemePalette.AlertRed, true, true, AIPersonalityType.Aggressive, ThemeText.BlackRaceTouringResourcePath),
            new RivalDefinition(ThemeText.RivalKoiHandle, ThemeText.RivalKoiCrew, ThemeText.RivalKoiTaunt,
                ThemePalette.NeonMagenta, false, false, AIPersonalityType.Balanced, ThemeText.SilverCarResourcePath),
            new RivalDefinition(ThemeText.RivalHexaHandle, ThemeText.RivalHexaCrew, ThemeText.RivalHexaTaunt,
                ThemePalette.ElectricPurple, true, false, AIPersonalityType.Cautious, ThemeText.BlackRaceTouringResourcePath),
            new RivalDefinition(ThemeText.RivalDocHandle, ThemeText.RivalDocCrew, ThemeText.RivalDocTaunt,
                ThemePalette.AcidYellow, false, true, AIPersonalityType.ShortcutLover, ThemeText.FlyingTransportResourcePath),
            new RivalDefinition(ThemeText.RivalZeroHandle, ThemeText.RivalZeroCrew, ThemeText.RivalZeroTaunt,
                ThemePalette.ToxicGreen, true, true, AIPersonalityType.Drifter, ThemeText.BlackRaceTouringResourcePath)
        };
    }

    /// <summary>Immutable identity and car appearance for one roster slot.</summary>
    public sealed class RivalDefinition
    {
        public string Handle { get; }
        public string Crew { get; }
        public string Taunt { get; }
        public Color NeonColor { get; }
        public bool Spoiler { get; }
        public bool Widebody { get; }
        public AIPersonalityType Personality { get; }
        public string ModelResourcePath { get; }

        public RivalDefinition(string handle, string crew, string taunt, Color neonColor,
            bool spoiler, bool widebody, AIPersonalityType personality, string modelResourcePath)
        {
            Handle = handle; Crew = crew; Taunt = taunt; NeonColor = neonColor;
            Spoiler = spoiler; Widebody = widebody; Personality = personality; ModelResourcePath = modelResourcePath;
        }

        public CarVisualProfile CreateVisualProfile()
        {
            return new CarVisualProfile
            {
                bodyType = CarBodyType.StreetWedge,
                bodyColor = ThemePalette.PanelDark,
                neonColor = NeonColor,
                spoiler = Spoiler,
                widebody = Widebody,
                handle = Handle,
                modelResourcePath = ModelResourcePath,
                modelYawCorrection = ModelResourcePath == ThemeText.SilverCarResourcePath ? 180f : 0f
            };
        }
    }
}
