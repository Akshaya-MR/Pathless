using UnityEngine;

namespace TheRacer
{
    /// <summary>Layout, timing and text constants shared by the neon HUD. Move strings into ThemeText if you prefer.</summary>
    public static class HudStyle
    {
        // Layout (reference resolution 1920x1080).
        public const float Margin = 40f;
        public const float Skew = 0.14f;
        public const float GhostSpread = 3f;
        public const float GhostAlpha = 0.6f;
        public const float PanelAlpha = 0.84f;
        public const float GlowAlpha = 0.5f;

        // Speedometer.
        public const float ArcSmoothTime = 0.09f;
        public const float BoostBlendRate = 3f;
        public const int RedlineSegments = 4;
        public const float RedlineStart = 0.88f;
        public const float RedlineHz = 3f;
        public const float BoostMix = 0.75f;

        // Flashes and pulses.
        public const float PositionFlashTime = 0.6f;
        public const float PositionPunch = 0.55f;
        public const float BestPulseTime = 1.2f;
        public const float BestPulsePunch = 0.4f;
        public const float ToastTime = 2.1f;
        public const float ToastPunch = 0.35f;
        public const float ToastPunchTime = 0.2f;
        public const float FinishAnimTime = 0.45f;
        public const float TrackSmoothTime = 0.25f;
        public const float WrongWayDelay = 1.5f;
        public const float WrongWayDot = -0.3f;

        // Zone banner.
        public const float ZoneSlideTime = 0.35f;
        public const float ZoneHoldTime = 2.2f;
        public const float ZoneFadeTime = 0.6f;
        public const float ZoneSlideDistance = 520f;
        public const float ZoneGlitchTime = 0.55f;
        public const float ZoneGlitchSpread = 14f;
        public const float ZoneBannerY = -170f;

        // Speed lines.
        public const float SpeedLineStart = 0.72f;
        public const float SpeedLineMaxAlpha = 0.55f;
        public const float SpeedLineTravel = 260f;
        public const float SpeedLineBlendRate = 4f;

        // Text.
        public const string PositionLabel = "POSITION";
        public const string FinishTitle = "RACE COMPLETE";
        public const string FinishTime = "TOTAL TIME";
        public const string FinishBest = "BEST LAP";

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>Cyan to magenta gradient used by the speed arc, digits and progress bar.</summary>
        public static Color SpeedColor(float t)
        {
            return Color.Lerp(ThemePalette.NeonCyan, ThemePalette.NeonMagenta, Mathf.Clamp01(t));
        }
    }
}
