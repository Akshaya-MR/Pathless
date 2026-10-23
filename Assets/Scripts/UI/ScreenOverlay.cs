using UnityEngine;
using UnityEngine.UI;

namespace TheRacer
{
    /// <summary>Procedural screen-space scanlines and a soft vignette.</summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class ScreenOverlay : MonoBehaviour
    {
        private Image _scanlines;
        private Image _vignette;
        private Sprite _scanSprite;
        private Sprite _vignetteSprite;
        private Texture2D _scanTexture;
        private Texture2D _vignetteTexture;
        private bool _lastReduceState;

        public void Build()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 90;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            _scanSprite = UIBuilder.CreateScanlineSprite(out _scanTexture);
            _scanlines = UIBuilder.CreateImage(transform, ThemeText.HudScanlinesName, _scanSprite, Color.white);
            Stretch(_scanlines.rectTransform);
            _scanlines.type = Image.Type.Simple;

            _vignetteSprite = UIBuilder.CreateVignetteSprite(out _vignetteTexture);
            _vignette = UIBuilder.CreateImage(transform, ThemeText.HudVignetteName, _vignetteSprite, Color.white);
            Stretch(_vignette.rectTransform);
            _lastReduceState = ThemeSettings.ReduceEffects;
            ApplyEffectsState();
        }

        private void Update()
        {
            if (_lastReduceState == ThemeSettings.ReduceEffects) return;
            ApplyEffectsState();
        }

        private void ApplyEffectsState()
        {
            bool enabled = !ThemeSettings.ReduceEffects;
            if (_scanlines != null) _scanlines.enabled = enabled;
            if (_vignette != null) _vignette.enabled = enabled;
            _lastReduceState = ThemeSettings.ReduceEffects;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void OnDestroy()
        {
            DestroyAsset(_scanSprite);
            DestroyAsset(_vignetteSprite);
            DestroyAsset(_scanTexture);
            DestroyAsset(_vignetteTexture);
        }

        private static void DestroyAsset(Object asset)
        {
            if (asset == null) return;
            if (Application.isPlaying) Destroy(asset);
            else DestroyImmediate(asset);
        }
    }
}
