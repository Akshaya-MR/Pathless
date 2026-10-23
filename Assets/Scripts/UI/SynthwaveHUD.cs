using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace TheRacer
{
    /// <summary>Compact synthwave corner HUD; presentation only, with race data supplied by GameBootstrap.</summary>
    public sealed class SynthwaveHUD : MonoBehaviour
    {
        private const string MuteKey = "theracer.hud.muted";
        private const string VolumeKey = "theracer.hud.volume";
        private static readonly Color SkyBlue = new Color32(154, 216, 232, 255);
        private static readonly Color Cyan = new Color32(42, 225, 255, 255);
        private static readonly Color Navy = new Color32(12, 18, 55, 255);
        private static readonly Color Maroon = new Color32(47, 5, 24, 245);

        private CarController _car;
        private TrackSpawner _spawner;
        private GameBootstrap _owner;
        private RacePositionTracker _positions;
        private RacerState _playerState;
        private Image _needle;
        private Text _speedText;
        private Text _scoreText;
        private Text _audioGlyph;
        private Text _raceText;
        private Text _toast;
        private RectTransform _scoreBadge;
        private Vector3 _scoreBadgeBaseScale = Vector3.one;
        private GameObject _menuOverlay;
        private Slider _volumeSlider;
        private int _score;
        private int _lastDodgeCount;
        private float _displaySpeed;
        private float _speedVelocity;
        private float _scorePulse;
        private float _toastTimer;
        private int _shownTimeTenth = -1;
        private int _shownPosition = -1;
        private float _volume = 1f;
        private bool _muted;
        private bool _raceStarted;
        private bool _menuOpen;
        private bool _hasSpeedOverride;
        private float _speedOverride;
        public bool IsMenuOpen => _menuOpen;

        public void Configure(CarController car, RollingTrack track, TrackData data, RaceConfig config, TrackSpawner spawner)
        {
            _car = car;
            _spawner = spawner;
            _owner = GetComponentInParent<GameBootstrap>();
            _lastDodgeCount = spawner != null ? spawner.ObstaclesDodged : 0;
            ApplyAudioState();
        }

        public void SetRaceState(RacePositionTracker positions, RacerState playerState)
        {
            _positions = positions;
            _playerState = playerState;
            RefreshRaceText();
        }

        public void SetRaceStarted(bool started)
        {
            _raceStarted = started;
            RefreshRaceText();
        }

        private void Awake()
        {
            RectTransform root = (RectTransform)transform;
            BuildCornerButton(root, "Menu", new Vector2(0f, 1f), new Vector2(32f, -32f), 70f, BuildHamburger, OpenMenu);
            BuildCornerButton(root, "Audio", new Vector2(1f, 1f), new Vector2(-32f, -32f), 70f, BuildAudioGlyph, ToggleMute);
            BuildSpeedBadge(root);
            BuildScoreBadge(root);
            BuildTopStatus(root);
            BuildToast(root);
            BuildMenu(root);
            _muted = PlayerPrefs.GetInt(MuteKey, 0) != 0;
            _volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            ApplyAudioState();
        }

        private void OnEnable() { FxEvents.OnDriftBonus += OnDriftBonus; Toast.Attach(this); }
        private void OnDisable() { FxEvents.OnDriftBonus -= OnDriftBonus; Toast.Detach(this); }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            RefreshRaceText();
            float targetSpeed = _hasSpeedOverride ? _speedOverride
                : _car != null ? Mathf.Clamp(_car.SpeedKMH, 0f, 320f) : Mathf.PingPong(Time.unscaledTime * 32f, 220f);
            _displaySpeed = Mathf.SmoothDamp(_displaySpeed, targetSpeed, ref _speedVelocity, 0.18f, 1000f, dt);
            if (_speedText != null) _speedText.text = Mathf.RoundToInt(_displaySpeed).ToString("000", CultureInfo.InvariantCulture);
            if (_needle != null) _needle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -130f + Mathf.Clamp01(_displaySpeed / 320f) * 260f);

            int dodges = _spawner != null ? _spawner.ObstaclesDodged : 0;
            if (dodges > _lastDodgeCount) SetScore(_score + (dodges - _lastDodgeCount) * 100);
            _lastDodgeCount = dodges;
            if (_scoreBadge != null)
            {
                _scorePulse = Mathf.Max(0f, _scorePulse - dt);
                float pulse = _scorePulse > 0f ? 1f + 0.14f * Mathf.Sin((0.32f - _scorePulse) / 0.32f * Mathf.PI) : 1f;
                _scoreBadge.localScale = _scoreBadgeBaseScale * pulse;
            }
            if (_toastTimer > 0f)
            {
                _toastTimer -= dt;
                if (_toastTimer <= 0f && _toast != null) _toast.gameObject.SetActive(false);
            }
        }

        private void BuildCornerButton(RectTransform root, string name, Vector2 anchor, Vector2 offset, float size, Action<RectTransform> content, UnityEngine.Events.UnityAction action)
        {
            RectTransform badge = NewRect(root, name + " Button", anchor, offset, new Vector2(size, size));
            badge.localScale = TouchScale(size, 48f);
            Image glow = UIBuilder.CreateImage(badge, name + " glow", UIBuilder.CircleSprite(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.18f));
            Stretch(glow.rectTransform, 0f);
            Image face = UIBuilder.CreateImage(badge, name + " face", UIBuilder.CircleSprite(), SkyBlue);
            face.raycastTarget = true;
            Stretch(face.rectTransform, 5f);
            Image ring = UIBuilder.CreateImage(badge, name + " neon ring", UIBuilder.RingSprite(), Cyan);
            Stretch(ring.rectTransform, 0f);
            Button button = badge.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(action);
            content(badge);
        }

        private void BuildHamburger(RectTransform parent)
        {
            for (int i = 0; i < 3; i++)
            {
                Image line = UIBuilder.CreateImage(parent, "Menu line " + i, null, Navy);
                RectTransform r = line.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(27f, 4f);
                r.anchoredPosition = new Vector2(0f, (1 - i) * 8f);
            }
        }

        private void BuildAudioGlyph(RectTransform parent)
        {
            _audioGlyph = UIBuilder.Text(parent, "Audio icon", "◀)))", 17, Navy, TextAnchor.MiddleCenter);
            Stretch(_audioGlyph.rectTransform, 0f);
            _audioGlyph.resizeTextForBestFit = true;
            _audioGlyph.resizeTextMinSize = 13;
            _audioGlyph.resizeTextMaxSize = 20;
        }

        private void BuildSpeedBadge(RectTransform root)
        {
            RectTransform badge = MakeBadge(root, "Speedometer", new Vector2(0f, 0f), new Vector2(91f, 91f), new Vector2(144f, 144f));
            Text label = UIBuilder.Text(badge, "SPD label", "SPD", 13, Navy, TextAnchor.MiddleCenter);
            AnchorRect(label.rectTransform, new Vector2(0.18f, 0.68f), new Vector2(0.82f, 0.85f));
            _speedText = UIBuilder.Text(badge, "Speed readout", "000", 25, Navy, TextAnchor.MiddleCenter);
            _speedText.fontStyle = FontStyle.Bold;
            AnchorRect(_speedText.rectTransform, new Vector2(0.12f, 0.40f), new Vector2(0.88f, 0.68f));
            Text unit = UIBuilder.Text(badge, "Speed unit", "KM/H", 9, Navy, TextAnchor.MiddleCenter);
            AnchorRect(unit.rectTransform, new Vector2(0.2f, 0.22f), new Vector2(0.8f, 0.38f));
            Image hub = UIBuilder.CreateImage(badge, "Needle hub", UIBuilder.CircleSprite(), Navy);
            RectTransform hr = hub.rectTransform;
            hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f); hr.sizeDelta = new Vector2(9f, 9f); hr.anchoredPosition = new Vector2(0f, -10f);
            _needle = UIBuilder.CreateImage(badge, "Speed needle", null, Navy);
            RectTransform nr = _needle.rectTransform;
            nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 0.5f); nr.pivot = new Vector2(0.5f, 0f); nr.sizeDelta = new Vector2(3f, 39f); nr.anchoredPosition = new Vector2(0f, -10f);
        }

        private void BuildScoreBadge(RectTransform root)
        {
            _scoreBadge = MakeBadge(root, "Score", new Vector2(1f, 0f), new Vector2(-91f, 91f), new Vector2(144f, 144f));
            _scoreBadgeBaseScale = _scoreBadge.localScale;
            Text title = UIBuilder.Text(_scoreBadge, "Score label", "SCORE", 11, Navy, TextAnchor.MiddleCenter);
            AnchorRect(title.rectTransform, new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.82f));
            _scoreText = UIBuilder.Text(_scoreBadge, "Score value", "000000", 19, Navy, TextAnchor.MiddleCenter);
            _scoreText.fontStyle = FontStyle.Bold;
            AnchorRect(_scoreText.rectTransform, new Vector2(0.08f, 0.39f), new Vector2(0.92f, 0.62f));
        }

        private RectTransform MakeBadge(RectTransform root, string name, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            RectTransform badge = NewRect(root, name + " Badge", anchor, offset, size);
            badge.localScale = TouchScale(size.x, 84f);
            Image glow = UIBuilder.CreateImage(badge, name + " glow", UIBuilder.CircleSprite(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.15f)); Stretch(glow.rectTransform, 0f);
            Image fill = UIBuilder.CreateImage(badge, name + " face", UIBuilder.CircleSprite(), new Color(SkyBlue.r, SkyBlue.g, SkyBlue.b, 0.91f)); Stretch(fill.rectTransform, 6f);
            Image ring = UIBuilder.CreateImage(badge, name + " ring", UIBuilder.RingSprite(), Cyan); Stretch(ring.rectTransform, 0f);
            return badge;
        }

        private void BuildTopStatus(RectTransform root)
        {
            RectTransform strip = NewRect(root, "Race status", new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(300f, 40f));
            Image back = UIBuilder.CreateImage(strip, "Status background", null, new Color(Maroon.r, Maroon.g, Maroon.b, 0.56f)); Stretch(back.rectTransform, 0f);
            _raceText = UIBuilder.Text(strip, "Race status text", "PRESS ENTER TO START", 13, SkyBlue, TextAnchor.MiddleCenter); Stretch(_raceText.rectTransform, 0f);
        }

        private void BuildToast(RectTransform root)
        {
            RectTransform r = NewRect(root, "HUD toast", new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(420f, 55f));
            _toast = UIBuilder.Text(r, "Toast text", "", 20, SkyBlue, TextAnchor.MiddleCenter); Stretch(_toast.rectTransform, 0f); _toast.gameObject.SetActive(false);
        }

        private void BuildMenu(RectTransform root)
        {
            _menuOverlay = new GameObject("Pause menu overlay", typeof(RectTransform), typeof(Image));
            _menuOverlay.transform.SetParent(root, false);
            RectTransform overlay = _menuOverlay.GetComponent<RectTransform>(); Stretch(overlay, 0f);
            Image shade = _menuOverlay.GetComponent<Image>(); shade.color = new Color(5f / 255f, 3f / 255f, 22f / 255f, 0.78f); shade.raycastTarget = true;
            RectTransform panel = NewRect(overlay, "Pause menu panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(390f, 430f));
            panel.localScale = TouchScale(430f, 400f);
            Image panelImage = UIBuilder.CreateImage(panel, "Pause panel background", null, Maroon); Stretch(panelImage.rectTransform, 0f);
            Image outline = UIBuilder.CreateImage(panel, "Pause panel outline", UIBuilder.RingSprite(), Cyan); Stretch(outline.rectTransform, 0f);
            Text heading = UIBuilder.Text(panel, "Pause heading", "PAUSED", 23, SkyBlue, TextAnchor.MiddleCenter); AnchorRect(heading.rectTransform, new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.96f));
            MakeMenuButton(panel, "Resume", 0.70f, CloseMenu);
            MakeMenuButton(panel, "Restart", 0.56f, RestartRace);
            MakeMenuButton(panel, "Settings", 0.42f, ToggleSettings);
            MakeMenuButton(panel, "Quit", 0.28f, Quit);
            Text volumeLabel = UIBuilder.Text(panel, "Volume label", "MASTER VOLUME", 11, SkyBlue, TextAnchor.MiddleCenter); AnchorRect(volumeLabel.rectTransform, new Vector2(0.1f, 0.09f), new Vector2(0.48f, 0.21f));
            _volumeSlider = BuildSlider(panel);
            _volumeSlider.onValueChanged.AddListener(SetVolume);
            _volumeSlider.gameObject.SetActive(false); volumeLabel.gameObject.SetActive(false);
            _settingsLabel = volumeLabel;
            _menuOverlay.SetActive(false);
        }

        private Text _settingsLabel;
        private Slider BuildSlider(RectTransform panel)
        {
            RectTransform holder = NewRect(panel, "Volume slider", new Vector2(0.72f, 0.15f), Vector2.zero, new Vector2(190f, 28f));
            Image track = UIBuilder.CreateImage(holder, "Volume track", null, new Color(0.18f, 0.32f, 0.43f)); Stretch(track.rectTransform, 0f);
            track.raycastTarget = true;
            Slider slider = holder.gameObject.AddComponent<Slider>(); slider.minValue = 0f; slider.maxValue = 1f; slider.value = _volume;
            slider.targetGraphic = track;
            RectTransform fillArea = NewRect(holder, "Fill area", new Vector2(0f, 0f), Vector2.zero, Vector2.zero); fillArea.anchorMax = new Vector2(1f, 1f); fillArea.offsetMin = new Vector2(0f, 0f); fillArea.offsetMax = Vector2.zero;
            Image fill = UIBuilder.CreateImage(fillArea, "Volume fill", null, Cyan); Stretch(fill.rectTransform, 0f); slider.fillRect = fill.rectTransform;
            RectTransform handle = NewRect(holder, "Volume handle", Vector2.zero, Vector2.zero, new Vector2(24f, 28f));
            Image handleImage = UIBuilder.CreateImage(handle, "Volume handle image", UIBuilder.CircleSprite(), SkyBlue); handleImage.raycastTarget = true; Stretch(handleImage.rectTransform, 0f); slider.handleRect = handle;
            return slider;
        }

        private void MakeMenuButton(RectTransform panel, string label, float y, UnityEngine.Events.UnityAction action)
        {
            RectTransform row = NewRect(panel, label + " button", new Vector2(0.5f, y), Vector2.zero, new Vector2(260f, 48f));
            Image image = UIBuilder.CreateImage(row, label + " button background", UIBuilder.RingSprite(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f)); Stretch(image.rectTransform, 0f);
            image.raycastTarget = true;
            Button button = row.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            Text text = UIBuilder.Text(row, label + " button label", label, 15, SkyBlue, TextAnchor.MiddleCenter); Stretch(text.rectTransform, 0f);
        }

        private void ToggleSettings()
        {
            bool show = !_volumeSlider.gameObject.activeSelf;
            _volumeSlider.gameObject.SetActive(show); _settingsLabel.gameObject.SetActive(show);
        }

        public void SetSpeed(float value) { _speedOverride = Mathf.Clamp(value, 0f, 320f); _hasSpeedOverride = true; }
        public void UseVehicleSpeed() { _hasSpeedOverride = false; }
        public void SetScore(int value)
        {
            value = Mathf.Max(0, value);
            if (_score == value) return;
            _score = value;
            if (_scoreText != null) _scoreText.text = _score.ToString("N0", CultureInfo.InvariantCulture);
            _scorePulse = 0.32f;
        }

        public void ToggleMute() => SetMuted(!_muted);
        public void SetMuted(bool muted)
        {
            _muted = muted; PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0); PlayerPrefs.Save(); ApplyAudioState();
        }

        private void SetVolume(float value)
        {
            _volume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(VolumeKey, _volume); PlayerPrefs.Save();
            if (_volume > 0f && _muted) _muted = false;
            ApplyAudioState();
        }

        private void ApplyAudioState()
        {
            AudioListener.volume = _muted ? 0f : _volume;
            if (_audioGlyph != null) _audioGlyph.text = _muted ? "◀ ×" : "◀ )))";
            if (_volumeSlider != null && Mathf.Abs(_volumeSlider.value - _volume) > 0.01f) _volumeSlider.SetValueWithoutNotify(_volume);
        }

        public void OpenMenu()
        {
            if (_menuOpen) return;
            _menuOpen = true; if (_menuOverlay != null) _menuOverlay.SetActive(true);
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetState(GameState.Paused); else Time.timeScale = 0f;
        }

        public void CloseMenu()
        {
            if (!_menuOpen) return;
            _menuOpen = false; if (_menuOverlay != null) _menuOverlay.SetActive(false);
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetState(_raceStarted ? GameState.Racing : GameState.MainMenu); else Time.timeScale = 1f;
        }

        public void ToggleMenu() { if (_menuOpen) CloseMenu(); else OpenMenu(); }

        private void RestartRace()
        {
            CloseMenu();
            if (_owner != null) _owner.RestartRace();
            SetScore(0);
        }

        private static void Quit()
        {
            Time.timeScale = 1f;
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        public void ShowToast(string value, Color color)
        {
            if (_toast == null) return;
            _toast.text = value.ToUpperInvariant(); _toast.color = color; _toast.gameObject.SetActive(true); _toastTimer = 2.1f;
        }

        private void RefreshRaceText()
        {
            if (_raceText == null) return;
            if (!_raceStarted) { if (_raceText.text != "PRESS ENTER TO START") _raceText.text = "PRESS ENTER TO START"; return; }
            int position = _positions != null && _car != null ? _positions.GetPosition(_car) : 1;
            int count = _positions != null ? _positions.RacerCount : 1;
            float elapsed = _playerState != null ? _playerState.TotalRaceTime : 0f;
            int tenth = Mathf.FloorToInt(elapsed * 10f);
            if (tenth == _shownTimeTenth && position == _shownPosition && _raceText.text.Length > 0) return;
            _shownTimeTenth = tenth;
            _shownPosition = position;
            _raceText.text = string.Format(CultureInfo.InvariantCulture, "{0}/{1}   {2:00}:{3:00.0}", position, count, Mathf.FloorToInt(elapsed / 60f), elapsed % 60f);
        }

        private void OnDriftBonus(int points) { if (points > 0) SetScore(_score + points); }

        private static RectTransform NewRect(Transform parent, string name, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = anchor; r.pivot = anchor; r.anchoredPosition = offset; r.sizeDelta = size; return r;
        }

        private static void AnchorRect(RectTransform r, Vector2 min, Vector2 max)
        { r.anchorMin = min; r.anchorMax = max; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }
        private static void Stretch(RectTransform r, float inset)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.one * inset; r.offsetMax = Vector2.one * -inset; }

        private static Vector3 TouchScale(float referenceSize, float minimumPixels)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            float canvasScale = canvas != null ? canvas.scaleFactor : 1f;
            float scale = Mathf.Max(1f, minimumPixels / Mathf.Max(1f, referenceSize * canvasScale));
            return Vector3.one * scale;
        }
    }
}
