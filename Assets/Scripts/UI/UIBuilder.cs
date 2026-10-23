using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace TheRacer
{
    /// <summary>Creates reusable legacy UI building blocks for the runtime HUD.</summary>
    public static class UIBuilder
    {
        private static Sprite _panelSprite;
        private static Sprite _ringSprite;
        private static Sprite _circleSprite;
        private static Sprite _whiteSprite;
        private static Sprite _outlineSprite;
        private static Sprite _bracketSprite;
        private static Sprite _glowSprite;

        public static Canvas CreateCanvas(Transform parent, int sortingOrder = 100)
        {
            GameObject root = new GameObject("Neon HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            EnsureEventSystem();
            return canvas;
        }

        public static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite != null ? sprite : GetWhiteSprite();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image Outline(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            Image image = CreateImage(parent, name, GetOutlineSprite(), color);
            SetStretch(image.rectTransform, anchorMin, anchorMax);
            image.type = Image.Type.Sliced;
            return image;
        }

        public static Image SoftGlow(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            Image image = CreateImage(parent, name, GetGlowSprite(), color);
            SetStretch(image.rectTransform, anchorMin, anchorMax);
            return image;
        }

        public static void CornerBrackets(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color, float size = 24f)
        {
            CreateCorner(parent, name + " TL", anchorMin.x, anchorMax.y, new Vector2(0f, 1f), Vector3.one, size, color);
            CreateCorner(parent, name + " TR", anchorMax.x, anchorMax.y, new Vector2(1f, 1f), new Vector3(-1f, 1f, 1f), size, color);
            CreateCorner(parent, name + " BL", anchorMin.x, anchorMin.y, new Vector2(0f, 0f), new Vector3(1f, -1f, 1f), size, color);
            CreateCorner(parent, name + " BR", anchorMax.x, anchorMin.y, new Vector2(1f, 0f), new Vector3(-1f, -1f, 1f), size, color);
        }

        private static void CreateCorner(Transform parent, string name, float x, float y, Vector2 pivot, Vector3 scale, float size, Color color)
        {
            Image image = CreateImage(parent, name, GetBracketSprite(), color);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(x, y);
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(size, size);
            rect.localScale = scale;
        }

        private static void SetStretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        public static Image Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color color)
        {
            GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            Image image = panelObject.GetComponent<Image>();
            image.sprite = GetPanelSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            SkewEffect skew = panelObject.AddComponent<SkewEffect>();
            skew.Configure(HudStyle.Skew, rect);
            return image;
        }

        public static Text Text(Transform parent, string name, string value, int size, Color color, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value.ToUpperInvariant();
            text.raycastTarget = false;
            return text;
        }

        public static Image Bar(Transform parent, string name, Color color)
        {
            Image fill = Panel(parent, name, Vector2.zero, Vector2.one, Vector2.zero, color);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            return fill;
        }

        public static Image Ring(Transform parent, string name, Vector2 anchor, Vector2 size, Color color, bool filled)
        {
            GameObject ringObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            ringObject.transform.SetParent(parent, false);
            RectTransform rect = ringObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.anchoredPosition = Vector2.zero; rect.sizeDelta = size;
            Image image = ringObject.GetComponent<Image>();
            image.sprite = GetRingSprite(); image.color = color; image.raycastTarget = false;
            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Top;
                image.fillClockwise = false;
            }
            return image;
        }

        public static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            const int side = 128;
            const float center = (side - 1) * 0.5f;
            Texture2D texture = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "HUD Circle Texture", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color32[] pixels = new Color32[side * side];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    float dx = x - center; float dy = y - center;
                    float edge = center - Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * side + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(edge + 0.5f) * 255f));
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            _circleSprite = Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f), 100f);
            _circleSprite.name = "HUD Circle";
            return _circleSprite;
        }

        public static Sprite RingSprite() => GetRingSprite();

        public static Button Button(Transform parent, string name, string label, Color color)
        {
            Image image = Panel(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180f, 52f), color);
            Button button = image.gameObject.AddComponent<Button>();
            Text text = Text(image.transform, name + " Label", label, 18, ThemePalette.HudText, TextAnchor.MiddleCenter);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            button.targetGraphic = image;
            return button;
        }

        public static void Release()
        {
            ReleaseSprite(ref _panelSprite);
            ReleaseSprite(ref _ringSprite);
            ReleaseSprite(ref _circleSprite);
            ReleaseSprite(ref _whiteSprite);
            ReleaseSprite(ref _outlineSprite);
            ReleaseSprite(ref _bracketSprite);
            ReleaseSprite(ref _glowSprite);
        }

        private static void ReleaseSprite(ref Sprite sprite)
        {
            if (sprite == null) return;
            Texture2D texture = sprite.texture;
            if (Application.isPlaying) { Object.Destroy(sprite); if (texture != null) Object.Destroy(texture); }
            else { Object.DestroyImmediate(sprite); if (texture != null) Object.DestroyImmediate(texture); }
            sprite = null;
        }

        private static Sprite GetRingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int side = 128;
            const float center = (side - 1) * 0.5f;
            Texture2D texture = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Speedometer Ring Texture", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color32[] pixels = new Color32[side * side];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    float dx = x - center; float dy = y - center;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Min(radius - 51f, 62f - radius);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(edge + 0.5f) * 255f);
                    pixels[y * side + x] = new Color32(255, 255, 255, alpha);
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            _ringSprite = Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f), 100f);
            _ringSprite.name = "Speedometer Ring";
            return _ringSprite;
        }

        public static Sprite CreateScanlineSprite(out Texture2D texture)
        {
            const int width = 2;
            const int height = 256;
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "HUD Scanline Texture", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Color[] pixels = new Color[width * height];
            Color line = new Color(1f, 1f, 1f, 0.006f);
            Color transparent = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = y % 2 == 0 ? line : transparent;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "HUD Scanlines";
            return sprite;
        }

        public static Sprite CreateVignetteSprite(out Texture2D texture)
        {
            const int side = 128;
            texture = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "HUD Vignette Texture", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color[] pixels = new Color[side * side];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    float dx = (x / (float)(side - 1) - 0.5f) * 2f;
                    float dy = (y / (float)(side - 1) - 0.5f) * 2f;
                    float edge = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.48f) / 0.55f);
                    pixels[y * side + x] = new Color(0f, 0f, 0f, edge * edge * 0.38f);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "HUD Soft Vignette";
            return sprite;
        }

        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "HUD White Pixel" };
            texture.SetPixel(0, 0, Color.white); texture.Apply(false, true);
            _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return _whiteSprite;
        }

        private static Sprite GetOutlineSprite()
        {
            if (_outlineSprite != null) return _outlineSprite;
            const int side = 32;
            Texture2D texture = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "HUD Thin Outline Texture" };
            Color[] pixels = new Color[side * side];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                    pixels[y * side + x] = x < 2 || x >= side - 2 || y < 2 || y >= side - 2 ? Color.white : Color.clear;
            texture.SetPixels(pixels); texture.Apply(false, true);
            _outlineSprite = Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6f, 6f, 6f, 6f));
            return _outlineSprite;
        }

        private static Sprite GetBracketSprite()
        {
            if (_bracketSprite != null) return _bracketSprite;
            const int side = 32;
            Texture2D texture = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "HUD Corner Bracket Texture" };
            Color[] pixels = new Color[side * side];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                    pixels[y * side + x] = x < 3 || y >= side - 3 ? Color.white : Color.clear;
            texture.SetPixels(pixels); texture.Apply(false, true);
            _bracketSprite = Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f), 100f);
            return _bracketSprite;
        }

        private static Sprite GetGlowSprite()
        {
            if (_glowSprite != null) return _glowSprite;
            const int side = 64;
            Texture2D texture = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "HUD Inner Glow Texture" };
            Color[] pixels = new Color[side * side];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    float dx = (x / (float)(side - 1) - 0.5f) * 2f;
                    float dy = (y / (float)(side - 1) - 0.5f) * 2f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2f) * 0.3f;
                    pixels[y * side + x] = new Color(1f, 1f, 1f, alpha);
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            _glowSprite = Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f), 100f);
            return _glowSprite;
        }

        private static Sprite GetPanelSprite()
        {
            if (_panelSprite != null) return _panelSprite;
            const int side = 32;
            Texture2D texture = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "HUD Chamfer Texture", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color32[] pixels = new Color32[side * side];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    bool cut = (x < 5 && y < 5 && x + y < 8) || (x >= side - 5 && y < 5 && side - 1 - x + y < 8)
                        || (x < 5 && y >= side - 5 && x + side - 1 - y < 8) || (x >= side - 5 && y >= side - 5 && 2 * side - 2 - x - y < 8);
                    pixels[y * side + x] = cut ? new Color32(255, 255, 255, 0) : new Color32(255, 255, 255, 255);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _panelSprite = Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(9f, 9f, 9f, 9f));
            _panelSprite.name = "HUD Chamfer Nine Slice";
            return _panelSprite;
        }

        private static void EnsureEventSystem()
        {
            EventSystem current = EventSystem.current;
            GameObject eventObject = current != null ? current.gameObject : new GameObject(ThemeText.HudEventSystemName, typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            if (eventObject.GetComponent<InputSystemUIInputModule>() == null)
                eventObject.AddComponent<InputSystemUIInputModule>();
            StandaloneInputModule legacy = eventObject.GetComponent<StandaloneInputModule>();
            if (legacy != null) legacy.enabled = false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (eventObject.GetComponent<StandaloneInputModule>() == null)
                eventObject.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
