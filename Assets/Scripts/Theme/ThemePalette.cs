using UnityEngine;

namespace TheRacer
{
    /// <summary>Shared colors for the NEO-KOWLOON 2087 visual identity.</summary>
    public static class ThemePalette
    {
        public static readonly Color Void = Hex(0x07040F);
        public static readonly Color PanelDark = Hex(0x0E0A1C);
        public static readonly Color NeonCyan = Hex(0x00F0FF);
        public static readonly Color NeonMagenta = Hex(0xFF2BD6);
        public static readonly Color ElectricPurple = Hex(0x8A2BFF);
        public static readonly Color AcidYellow = Hex(0xF7FF3C);
        public static readonly Color ToxicGreen = Hex(0x39FF88);
        public static readonly Color AlertRed = Hex(0xFF2A4D);
        public static readonly Color NoEmission = new Color(0f, 0f, 0f, 1f);
        public static readonly Color HudText = Hex(0xD8FCFF);
        public static readonly Color HudMuted = Hex(0x78AAB5);

        /// <summary>Returns an HDR color suitable for an emissive material.</summary>
        public static Color Neon(Color color, float intensity)
        {
            return new Color(color.r * intensity, color.g * intensity, color.b * intensity, color.a);
        }

        private static Color Hex(uint value)
        {
            const float channel = 1f / 255f;
            return new Color(
                ((value >> 16) & 255) * channel,
                ((value >> 8) & 255) * channel,
                (value & 255) * channel,
                1f);
        }
    }
}
