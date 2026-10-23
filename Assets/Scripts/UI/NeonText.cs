using UnityEngine;
using UnityEngine.UI;

namespace TheRacer
{
    /// <summary>Text with magenta/cyan ghost layers behind it for a chromatic-aberration look.</summary>
    public sealed class NeonText
    {
        private readonly RectTransform _root;
        private readonly Text _main;
        private readonly RectTransform _ghostA;
        private readonly RectTransform _ghostB;

        public RectTransform Root { get { return _root; } }
        public Text Main { get { return _main; } }

        public NeonText(Transform parent, string name, string value, int size, Color color, TextAnchor anchor,
            Vector2 min, Vector2 max, RectTransform skewReference)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            _root.anchorMin = min;
            _root.anchorMax = max;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            Color ghostA = HudStyle.WithAlpha(ThemePalette.NeonMagenta, HudStyle.GhostAlpha);
            Color ghostB = HudStyle.WithAlpha(ThemePalette.NeonCyan, HudStyle.GhostAlpha);
            _ghostA = Make(name + " GhostA", value, size, ghostA, anchor, skewReference).rectTransform;
            _ghostB = Make(name + " GhostB", value, size, ghostB, anchor, skewReference).rectTransform;
            _main = Make(name + " Main", value, size, color, anchor, skewReference);
            SetSpread(HudStyle.GhostSpread);
        }

        private Text Make(string name, string value, int size, Color color, TextAnchor anchor, RectTransform skewReference)
        {
            Text text = UIBuilder.Text(_root, name, value, size, color, anchor);
            text.fontStyle = FontStyle.Bold;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            if (skewReference != null)
            {
                SkewEffect effect = text.gameObject.AddComponent<SkewEffect>();
                effect.Configure(HudStyle.Skew, skewReference);
            }
            return text;
        }

        /// <summary>Sets the text on all layers. Callers must only call this when the value changed.</summary>
        public void Set(string value)
        {
            _main.text = value;
            _ghostA.GetComponent<Text>().text = value;
            _ghostB.GetComponent<Text>().text = value;
        }

        public void SetColor(Color color)
        {
            _main.color = color;
        }

        /// <summary>Sets how far the ghost layers are pushed apart, in pixels.</summary>
        public void SetSpread(float pixels)
        {
            _ghostA.anchoredPosition = new Vector2(-pixels, 0f);
            _ghostB.anchoredPosition = new Vector2(pixels, 0f);
        }

        public void SetActive(bool active)
        {
            if (_root.gameObject.activeSelf != active) _root.gameObject.SetActive(active);
        }
    }
}
