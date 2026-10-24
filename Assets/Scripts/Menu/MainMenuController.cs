using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheRacer
{
    /// <summary>Builds and owns the PATHLESS main menu overlay and its race handoff.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string EnabledKey = "Pathless_MenuEnabled";
        private static bool _menuOpen;
        private static bool _hasShownMenu;
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 1) != 0;
            set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static bool IsMenuOpen => _menuOpen;
        public static bool HasShownMenu { get => _hasShownMenu; set => _hasShownMenu = value; }

        private readonly List<RectTransform> _rainStreaks = new List<RectTransform>();
        private GameBootstrap _bootstrap;
        private SynthwaveHUD _hud;
        private CarSelectorMenu _selector;
        private Canvas _canvas;
        private RectTransform _contentRoot;
        private GameObject _mainPanel;
        private GameObject _settingsPanel;
        private Text _carTitle;
        private Text _carDescription;
        private Text _counter;
        private Text _muteLabel;
        private Text _subtitle;
        private Text _glitchTitle;
        private readonly List<Image> _dots = new List<Image>();
        private readonly List<Image> _statFills = new List<Image>();
        private readonly float[] _statTargets = new float[4];
        private RawImage _carPreview;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLock;
        private bool _ready;
        private bool _starting;
        private bool _settingsOpen;
        private float _nextGlitch;
        private float _glitchRemaining;
        private Vector2 _titlePosition;
        private Texture2D _scanlineTexture;
        private Texture2D _vignetteTexture;
        private Image _fadeImage;

        public void Initialize(GameBootstrap bootstrap)
        {
            if (!Enabled || HasShownMenu) return;
            _bootstrap = bootstrap;
            _hud = bootstrap.GetComponentInChildren<SynthwaveHUD>(true);
            _previousCursorVisible = Cursor.visible;
            _previousCursorLock = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            _menuOpen = true;
            Time.timeScale = 1f;
            BuildInterface();
            _selector = gameObject.AddComponent<CarSelectorMenu>();
            _selector.SelectionChanged += RefreshSelection;
            _selector.Initialize(_carPreview);
            _nextGlitch = Time.unscaledTime + Random.Range(3f, 6f);
            _ready = true;
        }

        private void BuildInterface()
        {
            _canvas = UIBuilder.CreateCanvas(transform, 500);
            _canvas.gameObject.name = "PATHLESS Main Menu Canvas";
            RectTransform root = _canvas.transform as RectTransform;
            GameObject content = new GameObject("Main menu visual content", typeof(RectTransform));
            _contentRoot = content.GetComponent<RectTransform>();
            _contentRoot.SetParent(root, false);
            Stretch(_contentRoot);
            Image background = UIBuilder.CreateImage(_contentRoot, "Menu background", null, MenuColors.Background);
            Stretch(background.rectTransform);

            BuildStarfield(_contentRoot);
            BuildRain(_contentRoot);
            _vignetteTexture = BuildVignetteTexture();
            RawImage vignette = CreateTextureOverlay(_contentRoot, "Menu vignette", _vignetteTexture, .5f);
            vignette.raycastTarget = false;
            _scanlineTexture = BuildScanlineTexture();
            RawImage scanlines = CreateTextureOverlay(_contentRoot, "Menu scanlines", _scanlineTexture, .06f);
            scanlines.raycastTarget = false;

            _glitchTitle = MakeText(_contentRoot, "PATHLESS", 64, MenuColors.Cyan, TextAnchor.MiddleLeft,
                new Vector2(.045f, .84f), new Vector2(.54f, .97f));
            Shadow titleShadow = _glitchTitle.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = MenuColors.Magenta;
            titleShadow.effectDistance = new Vector2(4f, -3f);
            _titlePosition = _glitchTitle.rectTransform.anchoredPosition;
            _subtitle = MakeText(_contentRoot, "OWN THE NIGHT  //  NEO-KOWLOON 2087", 16, MenuColors.Orange,
                TextAnchor.MiddleLeft, new Vector2(.05f, .80f), new Vector2(.55f, .86f));

            _mainPanel = new GameObject("Main menu content", typeof(RectTransform));
            RectTransform main = _mainPanel.GetComponent<RectTransform>();
            main.SetParent(_contentRoot, false);
            Stretch(main);

            Image previewFrame = UIBuilder.CreateImage(main, "Car preview frame", null, new Color(.035f, .055f, .095f, .93f));
            SetAnchors(previewFrame.rectTransform, new Vector2(.29f, .31f), new Vector2(.72f, .76f));
            UIBuilder.Outline(previewFrame.transform, "Preview cyan frame", Vector2.zero, Vector2.one, MenuColors.Cyan);
            _carPreview = new GameObject("Selected car preview", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _carPreview.transform.SetParent(previewFrame.transform, false);
            Stretch(_carPreview.rectTransform, 3f);
            _carPreview.color = Color.white;
            _carPreview.raycastTarget = false;

            AddButton(main, "‹", new Vector2(.25f, .52f), new Vector2(88f, 115f), () => _selector?.Move(-1), false, 38);
            AddButton(main, "›", new Vector2(.76f, .52f), new Vector2(88f, 115f), () => _selector?.Move(1), false, 38);

            _carTitle = MakeText(main, "SILVER COUPE", 30, MenuColors.White, TextAnchor.MiddleCenter,
                new Vector2(.27f, .245f), new Vector2(.74f, .30f));
            _carDescription = MakeText(main, "Clean lines. Quiet menace.", 16, MenuColors.Cyan, TextAnchor.MiddleCenter,
                new Vector2(.27f, .205f), new Vector2(.74f, .245f));
            _counter = MakeText(main, "01 / 04", 13, MenuColors.Orange, TextAnchor.MiddleCenter,
                new Vector2(.39f, .16f), new Vector2(.62f, .195f));
            BuildDots(main);
            BuildStats(main);

            MakeText(main, "[‹][›] CHANGE CAR     [CLICK START RACE]     [ESC] SETTINGS", 12, MenuColors.White,
                TextAnchor.MiddleLeft, new Vector2(.05f, .04f), new Vector2(.48f, .095f));
            AddButton(main, "START RACE", new Vector2(.50f, .105f), new Vector2(320f, 82f), BeginRace, true, 24);
            AddButton(main, "SETTINGS", new Vector2(.75f, .105f), new Vector2(210f, 62f), ToggleSettings, false, 17);
            AddButton(main, "QUIT", new Vector2(.91f, .105f), new Vector2(150f, 62f), Quit, false, 17);

            BuildSettings(_contentRoot);
            GameObject fadeObject = new GameObject("Menu screen fade", typeof(RectTransform), typeof(Image));
            fadeObject.transform.SetParent(root, false);
            _fadeImage = fadeObject.GetComponent<Image>();
            _fadeImage.color = Color.black;
            _fadeImage.raycastTarget = false;
            Stretch(_fadeImage.rectTransform);
            StartCoroutine(FadeInitial());

            // Keep the scan texture moving without relying on scaled frame time.
            MenuScanlineDriver driver = _canvas.gameObject.AddComponent<MenuScanlineDriver>();
            driver.SetTexture(scanlines);
        }

        private void BuildSettings(RectTransform root)
        {
            _settingsPanel = new GameObject("Settings panel", typeof(RectTransform), typeof(Image));
            RectTransform panel = _settingsPanel.GetComponent<RectTransform>();
            panel.SetParent(root, false);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(570f, 350f);
            Image image = _settingsPanel.GetComponent<Image>();
            image.color = MenuColors.Panel;
            image.raycastTarget = true;
            Outline outline = _settingsPanel.AddComponent<Outline>();
            outline.effectColor = MenuColors.Cyan;
            outline.effectDistance = new Vector2(2f, 2f);
            MakeText(panel, "SYSTEM SETTINGS", 28, MenuColors.Cyan, TextAnchor.MiddleCenter,
                new Vector2(.08f, .72f), new Vector2(.92f, .91f));
            _muteLabel = MakeText(panel, "TOGGLE AUDIO MUTE", 18, MenuColors.White, TextAnchor.MiddleCenter,
                new Vector2(.19f, .42f), new Vector2(.81f, .57f));
            AddButton(panel, "TOGGLE AUDIO MUTE", new Vector2(.5f, .48f), new Vector2(300f, 58f), ToggleMute, false, 16);
            AddButton(panel, "BACK", new Vector2(.5f, .20f), new Vector2(220f, 54f), ToggleSettings, false, 16);
            _settingsPanel.SetActive(false);
        }

        private void BuildStats(RectTransform parent)
        {
            Image panel = UIBuilder.CreateImage(parent, "Vehicle stats panel", null, MenuColors.Panel);
            SetAnchors(panel.rectTransform, new Vector2(.755f, .37f), new Vector2(.965f, .70f));
            UIBuilder.Outline(panel.transform, "Stats cyan frame", Vector2.zero, Vector2.one, new Color32(0, 240, 255, 150));
            string[] names = { "SPEED", "ACCELERATION", "HANDLING", "BRAKING" };
            for (int i = 0; i < names.Length; i++)
            {
                float y = .79f - i * .22f;
                MakeText(panel.transform, names[i], 12, MenuColors.White, TextAnchor.MiddleLeft,
                    new Vector2(.08f, y), new Vector2(.92f, y + .16f));
                Image track = UIBuilder.CreateImage(panel.transform, names[i] + " stat track", null, new Color(.02f, .07f, .11f, 1f));
                SetAnchors(track.rectTransform, new Vector2(.08f, y - .09f), new Vector2(.92f, y - .025f));
                Image fill = UIBuilder.CreateImage(panel.transform, names[i] + " stat fill", null, i % 2 == 0 ? MenuColors.Cyan : MenuColors.Magenta);
                SetAnchors(fill.rectTransform, new Vector2(.08f, y - .09f), new Vector2(.08f, y - .025f));
                _statFills.Add(fill);
            }
        }

        private void BuildDots(RectTransform parent)
        {
            _dots.Clear();
            for (int i = 0; i < VehicleRegistry.All.Length; i++)
            {
                Image dot = UIBuilder.CreateImage(parent, "Car selection dot " + (i + 1), UIBuilder.CircleSprite(), MenuColors.Cyan);
                RectTransform rect = dot.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .145f);
                rect.sizeDelta = new Vector2(10f, 10f);
                rect.anchoredPosition = new Vector2((i - (VehicleRegistry.All.Length - 1) * .5f) * 22f, 0f);
                _dots.Add(dot);
            }
        }

        private void BuildStarfield(RectTransform parent)
        {
            Random.State oldState = Random.state;
            Random.InitState(2087);
            for (int i = 0; i < 80; i++)
            {
                Image star = UIBuilder.CreateImage(parent, "Night star " + i, UIBuilder.CircleSprite(),
                    new Color(MenuColors.White.r, MenuColors.White.g, MenuColors.White.b, Random.Range(.15f, .65f)));
                RectTransform rect = star.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(Random.value, Random.value);
                rect.sizeDelta = Vector2.one * Random.Range(1.5f, 4f);
            }
            Random.state = oldState;
        }

        private void BuildRain(RectTransform parent)
        {
            Random.State oldState = Random.state;
            Random.InitState(2088);
            for (int i = 0; i < 26; i++)
            {
                Image streak = UIBuilder.CreateImage(parent, "Neon rain streak " + i, null,
                    i % 2 == 0 ? new Color(MenuColors.Cyan.r, MenuColors.Cyan.g, MenuColors.Cyan.b, .3f)
                    : new Color(MenuColors.Purple.r, MenuColors.Purple.g, MenuColors.Purple.b, .3f));
                RectTransform rect = streak.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(Random.value, Random.value);
                rect.sizeDelta = new Vector2(1.5f, Random.Range(18f, 48f));
                rect.localRotation = Quaternion.Euler(0f, 0f, -8f);
                _rainStreaks.Add(rect);
            }
            Random.state = oldState;
        }

        private void Update()
        {
            if (!_ready || !_menuOpen || _starting) return;
            if (MenuInput.Left()) _selector?.Move(-1);
            else if (MenuInput.Right()) _selector?.Move(1);
            if (MenuInput.Back()) ToggleSettings();
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _statFills.Count; i++)
            {
                RectTransform bar = _statFills[i].rectTransform;
                float current = Mathf.InverseLerp(.08f, .92f, bar.anchorMax.x);
                float next = Mathf.MoveTowards(current, _statTargets[i], dt / .4f);
                bar.anchorMax = new Vector2(.08f + .84f * next, bar.anchorMax.y);
            }
            for (int i = 0; i < _rainStreaks.Count; i++)
            {
                RectTransform streak = _rainStreaks[i];
                Vector2 position = streak.anchorMin;
                position.y -= dt * (.18f + (i % 6) * .035f);
                if (position.y < -.08f) position.y = 1.08f;
                streak.anchorMin = streak.anchorMax = position;
            }
            if (_subtitle != null)
            {
                float alpha = Random.value < .005f ? Random.Range(.4f, .85f) : 1f;
                Color color = _subtitle.color;
                color.a = Mathf.Lerp(color.a, alpha, 8f * dt);
                _subtitle.color = color;
            }
            if (Time.unscaledTime >= _nextGlitch)
            {
                _glitchRemaining = .2f;
                _nextGlitch = Time.unscaledTime + Random.Range(3f, 6f);
            }
            if (_glitchRemaining > 0f)
            {
                _glitchRemaining -= dt;
                _glitchTitle.rectTransform.anchoredPosition = _titlePosition + new Vector2(Random.Range(-9f, 9f), 0f);
                _glitchTitle.color = MenuColors.Magenta;
            }
            else if (_glitchTitle != null)
            {
                _glitchTitle.rectTransform.anchoredPosition = _titlePosition;
                _glitchTitle.color = MenuColors.Cyan;
            }
        }

        private void RefreshSelection(int index)
        {
            if (_selector == null || _selector.Selected == null) return;
            CarMenuInfo.Entry info = CarMenuInfo.Get(_selector.Selected);
            _carTitle.text = info.displayName;
            _carDescription.text = info.description;
            _counter.text = (index + 1).ToString("00") + " / " + _selector.Count.ToString("00");
            float[] values = { info.stats.x, info.stats.y, info.stats.z, info.stats.w };
            for (int i = 0; i < _dots.Count; i++)
            {
                _dots[i].color = i == index ? MenuColors.Cyan : new Color(MenuColors.Cyan.r, MenuColors.Cyan.g, MenuColors.Cyan.b, .32f);
                _dots[i].rectTransform.sizeDelta = Vector2.one * (i == index ? 13f : 8f);
            }
            for (int i = 0; i < _statFills.Count; i++)
            {
                _statTargets[i] = Mathf.Clamp01(values[i]);
            }
        }

        private void ToggleSettings()
        {
            if (_settingsPanel == null || _starting) return;
            _settingsOpen = !_settingsOpen;
            _settingsPanel.SetActive(_settingsOpen);
            if (_mainPanel != null) _mainPanel.SetActive(!_settingsOpen);
            if (_muteLabel != null)
                _muteLabel.text = PlayerPrefs.GetInt("theracer.hud.muted", 0) != 0 ? "AUDIO IS MUTED" : "AUDIO IS ON";
        }

        private void ToggleMute()
        {
            if (_hud != null) _hud.ToggleMute();
            if (_muteLabel != null)
                _muteLabel.text = PlayerPrefs.GetInt("theracer.hud.muted", 0) != 0 ? "AUDIO IS MUTED" : "AUDIO IS ON";
        }

        private void BeginRace()
        {
            if (_starting || !_ready || _selector == null || _bootstrap == null) return;
            _starting = true;
            StartCoroutine(StartRaceHandoff());
        }

        private IEnumerator StartRaceHandoff()
        {
            yield return Fade(1f, .4f);
            _menuOpen = false;
            HasShownMenu = true;
            Cursor.visible = _previousCursorVisible;
            Cursor.lockState = _previousCursorLock;
            VehicleId selected = _selector != null && _selector.Selected != null ? _selector.Selected.Id : VehicleId.SilverCoupe;
            if (_selector != null)
            {
                _selector.SelectionChanged -= RefreshSelection;
                Destroy(_selector);
            }
            if (_contentRoot != null) _contentRoot.gameObject.SetActive(false);
            _bootstrap.StartRaceFromMenu(selected);
            yield return Fade(0f, .4f);
            if (_canvas != null) Destroy(_canvas.gameObject);
            Destroy(this);
        }

        private IEnumerator Fade(float target, float duration)
        {
            if (_fadeImage == null) yield break;
            float start = _fadeImage.color.a;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                Color color = _fadeImage.color;
                color.a = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                _fadeImage.color = color;
                yield return null;
            }
            Color final = _fadeImage.color;
            final.a = target;
            _fadeImage.color = final;
        }

        private IEnumerator FadeInitial()
        {
            yield return Fade(0f, .6f);
        }

        private void AddButton(Transform parent, string label, Vector2 anchor, Vector2 size,
            UnityEngine.Events.UnityAction action, bool primary, int fontSize)
        {
            GameObject obj = new GameObject(label + " button", typeof(RectTransform), typeof(Image), typeof(CyberButton));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            Image image = obj.GetComponent<Image>();
            image.color = primary ? MenuColors.Magenta : new Color(.035f, .055f, .10f, .94f);
            image.raycastTarget = true;
            if (primary)
            {
                Image glow = UIBuilder.SoftGlow(obj.transform, label + " orange glow", Vector2.zero, Vector2.one,
                    new Color(MenuColors.Orange.r, MenuColors.Orange.g, MenuColors.Orange.b, .18f));
                glow.rectTransform.sizeDelta = new Vector2(18f, 18f);
                glow.raycastTarget = false;
            }
            Image frame = UIBuilder.Outline(obj.transform, label + " neon hover frame", Vector2.zero, Vector2.one,
                primary ? MenuColors.Magenta : MenuColors.Cyan);
            frame.raycastTarget = false;
            CyberButton hover = obj.GetComponent<CyberButton>();
            hover.Configure(frame, primary, action);
            Text text = MakeText(obj.transform, label, fontSize, MenuColors.White, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            Stretch(text.rectTransform);
        }

        private static Text MakeText(Transform parent, string value, int size, Color color, TextAnchor anchor,
            Vector2 min, Vector2 max)
        {
            Text text = UIBuilder.Text(parent, value, value, size, color, anchor);
            SetAnchors(text.rectTransform, min, max);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(9, size - 5);
            text.resizeTextMaxSize = size;
            return text;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }

        private RawImage CreateTextureOverlay(Transform parent, string name, Texture2D texture, float alpha)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            obj.transform.SetParent(parent, false);
            RawImage raw = obj.GetComponent<RawImage>();
            raw.texture = texture;
            raw.color = new Color(1f, 1f, 1f, alpha);
            Stretch(raw.rectTransform);
            return raw;
        }

        private static Texture2D BuildScanlineTexture()
        {
            Texture2D texture = new Texture2D(2, 64, TextureFormat.RGBA32, false) { name = "PATHLESS Scanlines", wrapMode = TextureWrapMode.Repeat };
            Color[] pixels = new Color[128];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 2; x++) pixels[y * 2 + x] = new Color(0f, 0f, 0f, y % 4 == 0 ? 1f : .15f);
            texture.SetPixels(pixels); texture.Apply();
            return texture;
        }

        private static Texture2D BuildVignetteTexture()
        {
            const int side = 128;
            Texture2D texture = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "PATHLESS Vignette" };
            Color[] pixels = new Color[side * side];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    float dx = (x - side * .5f) / (side * .5f);
                    float dy = (y - side * .5f) / (side * .5f);
                    float edge = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - .35f) / .65f);
                    pixels[y * side + x] = new Color(0f, 0f, 0f, edge * .78f);
                }
            texture.SetPixels(pixels); texture.Apply();
            return texture;
        }

        private static void Quit()
        {
            Time.timeScale = 1f;
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void OnDestroy()
        {
            if (!_starting)
            {
                Cursor.visible = _previousCursorVisible;
                Cursor.lockState = _previousCursorLock;
                _menuOpen = false;
            }
            if (_selector != null) _selector.SelectionChanged -= RefreshSelection;
            if (_scanlineTexture != null) Destroy(_scanlineTexture);
            if (_vignetteTexture != null) Destroy(_vignetteTexture);
        }
    }
}
