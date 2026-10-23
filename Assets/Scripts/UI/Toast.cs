using UnityEngine;
using UnityEngine.UI;

namespace TheRacer
{
    /// <summary>Single replaceable toast; messages sent before HUD setup are retained.</summary>
    public static class Toast
    {
        private static HUD _hud;
        private static SynthwaveHUD _synthwaveHud;
        private static string _pendingText;
        private static Color _pendingColor;

        public static void Show(string text, Color color)
        {
            if (_synthwaveHud != null) _synthwaveHud.ShowToast(text, color);
            else if (_hud != null) _hud.ShowToast(text, color);
            else { _pendingText = text; _pendingColor = color; }
        }

        internal static void Attach(HUD hud)
        {
            _hud = hud;
            if (!string.IsNullOrEmpty(_pendingText))
            {
                hud.ShowToast(_pendingText, _pendingColor);
                _pendingText = null;
            }
        }

        internal static void Attach(SynthwaveHUD hud)
        {
            _synthwaveHud = hud;
            if (!string.IsNullOrEmpty(_pendingText))
            {
                hud.ShowToast(_pendingText, _pendingColor);
                _pendingText = null;
            }
        }

        internal static void Detach(HUD hud)
        {
            if (_hud == hud) _hud = null;
        }

        internal static void Detach(SynthwaveHUD hud)
        {
            if (_synthwaveHud == hud) _synthwaveHud = null;
        }
    }
}
