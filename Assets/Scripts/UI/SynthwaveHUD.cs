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
        private static readonly Color HotPink = new Color32(255, 43, 214, 255);
        private static readonly Color Panel = new Color32(5, 9, 25, 246);
        private static readonly Color MutedCyan = new Color32(47, 137, 165, 255);

        private CarController _car;
        private TrackSpawner _spawner;
        private GameBootstrap _owner;
        private RacePositionTracker _positions;
        private RacerState _playerState;
        private Image _needle;
        private Image _needleGlow;
        private Image _scoreRing;
        private Image _scoreGlow;
        private Text _speedText;
        private Text _scoreText;
        private Text _audioGlyph;
        private Text _raceText;
        private Text _countdownText;
        private Text _toast;
        private RectTransform _speedBadge;
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
        private float _cornerBadgeSize = 220f;
        private float _cornerHorizontalOffset = -68f;
        private float _cornerVerticalOffset = 44f;
        private bool _interfaceBuilt;
        public bool IsMenuOpen => _menuOpen;

        public void Configure(CarController car, RollingTrack track, TrackData data, RaceConfig config, TrackSpawner spawner,
            float cornerBadgeSize, float cornerHorizontalOffset, float cornerVerticalOffset)
        {
            _car = car;
            _spawner = spawner;
            _owner = GetComponentInParent<GameBootstrap>();
            _lastDodgeCount = spawner != null ? spawner.ObstaclesDodged : 0;
            _cornerBadgeSize = Mathf.Clamp(cornerBadgeSize, 120f, 360f);
            _cornerHorizontalOffset = cornerHorizontalOffset;
            _cornerVerticalOffset = cornerVerticalOffset;
            if (!_interfaceBuilt) BuildInterface();
            ApplyAudioState();
        }

        /// <summary>Applies live Inspector edits to the runtime corner HUD.</summary>
        public void SetCornerBadgeLayout(float size, float horizontalOffset, float verticalOffset)
        {
            size = Mathf.Clamp(size, 120f, 360f);
            if (_interfaceBuilt && Mathf.Approximately(size, _cornerBadgeSize)
                && Mathf.Approximately(horizontalOffset, _cornerHorizontalOffset)
                && Mathf.Approximately(verticalOffset, _cornerVerticalOffset)) return;

            _cornerBadgeSize = size;
            _cornerHorizontalOffset = horizontalOffset;
            _cornerVerticalOffset = verticalOffset;
            if (!_interfaceBuilt) return;

            RemoveBadge(_speedBadge);
            RemoveBadge(_scoreBadge);
            _speedBadge = null;
            _scoreBadge = null;
            _needle = null;
            _needleGlow = null;
            _scoreRing = null;
            _scoreGlow = null;
            _speedText = null;
            _scoreText = null;
            RectTransform root = (RectTransform)transform;
            BuildSpeedBadge(root);
            BuildScoreBadge(root);
            if (_scoreText != null) _scoreText.text = _score.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static void RemoveBadge(RectTransform badge)
        {
            if (badge == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(badge.gameObject);
            else UnityEngine.Object.DestroyImmediate(badge.gameObject);
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
            _muted = PlayerPrefs.GetInt(MuteKey, 0) != 0;
            _volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        }

        private void BuildInterface()
        {
            RectTransform root = (RectTransform)transform;
            BuildCornerButton(root, "Menu", new Vector2(0f, 1f), new Vector2(32f, -32f), 70f, BuildHamburger, OpenMenu);
            BuildCornerButton(root, "Audio", new Vector2(1f, 1f), new Vector2(-32f, -32f), 70f, BuildAudioGlyph, ToggleMute);
            BuildSpeedBadge(root);
            BuildScoreBadge(root);
            BuildTopStatus(root);
            BuildToast(root);
            BuildCountdown(root);
            BuildMenu(root);
            _interfaceBuilt = true;
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
            if (_needle != null)
            {
                Quaternion needleRotation = Quaternion.Euler(0f, 0f, -130f + Mathf.Clamp01(_displaySpeed / 320f) * 260f);
                _needle.rectTransform.localRotation = needleRotation;
                if (_needleGlow != null) _needleGlow.rectTransform.localRotation = needleRotation;
            }

            int dodges = _spawner != null ? _spawner.ObstaclesDodged : 0;
            if (dodges > _lastDodgeCount) SetScore(_score + (dodges - _lastDodgeCount) * 100);
            _lastDodgeCount = dodges;
            if (_scoreBadge != null)
            {
                _scorePulse = Mathf.Max(0f, _scorePulse - dt);
                float pulse = _scorePulse > 0f ? 1f + 0.14f * Mathf.Sin((0.32f - _scorePulse) / 0.32f * Mathf.PI) : 1f;
                _scoreBadge.localScale = _scoreBadgeBaseScale * pulse;
                if (_scoreRing != null)
                    _scoreRing.color = _scorePulse > 0f ? Color.Lerp(Cyan, HotPink, pulse - 1f) : Cyan;
                if (_scoreGlow != null)
                    _scoreGlow.color = new Color(HotPink.r, HotPink.g, HotPink.b, _scorePulse > 0f ? 0.24f : 0.12f);
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
            Image glow = UIBuilder.CreateImage(badge, name + " glow", UIBuilder.CircleSprite(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.20f));
            Stretch(glow.rectTransform, 0f);
            Image pinkRim = UIBuilder.CreateImage(badge, name + " magenta rim", UIBuilder.RingSprite(), new Color(HotPink.r, HotPink.g, HotPink.b, 0.72f));
            Stretch(pinkRim.rectTransform, -4f);
            Image face = UIBuilder.CreateImage(badge, name + " face", UIBuilder.CircleSprite(), Panel);
            face.raycastTarget = true;
            Stretch(face.rectTransform, 8f);
            Image ring = UIBuilder.CreateImage(badge, name + " neon ring", UIBuilder.RingSprite(), Cyan);
            Stretch(ring.rectTransform, 0f);
            Image innerBezel = UIBuilder.CreateImage(badge, name + " inner bezel", UIBuilder.RingSprite(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.34f));
            Stretch(innerBezel.rectTransform, 14f);
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
                Image line = UIBuilder.CreateImage(parent, "Menu line " + i, null, i == 1 ? HotPink : Cyan);
                RectTransform r = line.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(27f, 4f);
                r.anchoredPosition = new Vector2(0f, (1 - i) * 8f);
            }
        }

        private void BuildAudioGlyph(RectTransform parent)
        {
            _audioGlyph = UIBuilder.Text(parent, "Audio icon", "◀)))", 17, Cyan, TextAnchor.MiddleCenter);
            Stretch(_audioGlyph.rectTransform, 0f);
            _audioGlyph.resizeTextForBestFit = true;
            _audioGlyph.resizeTextMinSize = 13;
            _audioGlyph.resizeTextMaxSize = 20;
            AddNeonText(_audioGlyph, Cyan);
        }

        private void BuildSpeedBadge(RectTransform root)
        {
            // Push the circle beyond the left edge; the negative offset is intentional
            // so only the inner portion remains visible, matching the reference HUD.
            _speedBadge = MakeBadge(root, "Speedometer", new Vector2(0f, 0f),
                new Vector2(_cornerHorizontalOffset, _cornerVerticalOffset), new Vector2(_cornerBadgeSize, _cornerBadgeSize));
            RectTransform badge = _speedBadge;
            float sizeFactor = _cornerBadgeSize / 220f;
            BuildSpeedTicks(badge);
            Text label = UIBuilder.Text(badge, "SPD label", "SPD // ENGINE", Mathf.RoundToInt(11f * sizeFactor), MutedCyan, TextAnchor.MiddleCenter);
            AnchorRect(label.rectTransform, new Vector2(0.18f, 0.68f), new Vector2(0.82f, 0.85f));
            AddNeonText(label, Cyan);
            _speedText = UIBuilder.Text(badge, "Speed readout", "000", Mathf.RoundToInt(38f * sizeFactor), SkyBlue, TextAnchor.MiddleCenter);
            _speedText.fontStyle = FontStyle.Bold;
            AnchorRect(_speedText.rectTransform, new Vector2(0.12f, 0.40f), new Vector2(0.88f, 0.68f));
            AddNeonText(_speedText, Cyan);
            Text unit = UIBuilder.Text(badge, "Speed unit", "KM/H", Mathf.RoundToInt(11f * sizeFactor), Cyan, TextAnchor.MiddleCenter);
            AnchorRect(unit.rectTransform, new Vector2(0.2f, 0.22f), new Vector2(0.8f, 0.38f));
            AddNeonText(unit, Cyan);
            _needleGlow = UIBuilder.CreateImage(badge, "Speed needle glow", null, new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f));
            RectTransform glowRect = _needleGlow.rectTransform;
            glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.5f); glowRect.pivot = new Vector2(0.5f, 0f); glowRect.sizeDelta = new Vector2(11f * sizeFactor, 60f * sizeFactor); glowRect.anchoredPosition = new Vector2(0f, -15f * sizeFactor);
            _needle = UIBuilder.CreateImage(badge, "Speed needle", null, SkyBlue);
            RectTransform nr = _needle.rectTransform;
            nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 0.5f); nr.pivot = new Vector2(0.5f, 0f); nr.sizeDelta = new Vector2(4f * sizeFactor, 59f * sizeFactor); nr.anchoredPosition = new Vector2(0f, -15f * sizeFactor);
            Image hub = UIBuilder.CreateImage(badge, "Needle hub", UIBuilder.CircleSprite(), HotPink);
            RectTransform hr = hub.rectTransform;
            hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f); hr.sizeDelta = new Vector2(15f * sizeFactor, 15f * sizeFactor); hr.anchoredPosition = new Vector2(0f, -15f * sizeFactor);
        }

        private void BuildSpeedTicks(RectTransform badge)
        {
            const int tickCount = 25;
            float radius = _cornerBadgeSize * 0.39f;
            float sizeFactor = _cornerBadgeSize / 220f;
            for (int i = 0; i < tickCount; i++)
            {
                float t = i / (float)(tickCount - 1);
                float angle = (90f - 130f + 260f * t) * Mathf.Deg2Rad;
                Color color = t > 0.82f ? HotPink : Color.Lerp(Cyan, SkyBlue, t * 0.35f);
                Image tick = UIBuilder.CreateImage(badge, "Speed dial tick " + i, null,
                    new Color(color.r, color.g, color.b, i % 3 == 0 ? 0.95f : 0.58f));
                RectTransform rect = tick.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2((i % 3 == 0 ? 10f : 6f) * sizeFactor, 3f * sizeFactor);
                rect.anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                rect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            }
        }

        private void BuildScoreBadge(RectTransform root)
        {
            _scoreBadge = MakeBadge(root, "Score", new Vector2(1f, 0f),
                new Vector2(-_cornerHorizontalOffset, _cornerVerticalOffset), new Vector2(_cornerBadgeSize, _cornerBadgeSize));
            _scoreBadgeBaseScale = _scoreBadge.localScale;
            float sizeFactor = _cornerBadgeSize / 220f;
            Text title = UIBuilder.Text(_scoreBadge, "Score label", "// SCORE //", Mathf.RoundToInt(14f * sizeFactor), HotPink, TextAnchor.MiddleCenter);
            AnchorRect(title.rectTransform, new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.82f));
            AddNeonText(title, HotPink);
            Image divider = UIBuilder.CreateImage(_scoreBadge, "Score divider", null, new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f));
            AnchorRect(divider.rectTransform, new Vector2(0.3f, 0.57f), new Vector2(0.7f, 0.585f));
            _scoreText = UIBuilder.Text(_scoreBadge, "Score value", "000000", Mathf.RoundToInt(32f * sizeFactor), SkyBlue, TextAnchor.MiddleCenter);
            _scoreText.fontStyle = FontStyle.Bold;
            AnchorRect(_scoreText.rectTransform, new Vector2(0.08f, 0.39f), new Vector2(0.92f, 0.62f));
            AddNeonText(_scoreText, Cyan);
        }

        private RectTransform MakeBadge(RectTransform root, string name, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            RectTransform badge = NewRect(root, name + " Badge", anchor, offset, size);
            badge.localScale = TouchScale(size.x, 84f);
            Image glow = UIBuilder.CreateImage(badge, name + " glow", UIBuilder.CircleSprite(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.18f)); Stretch(glow.rectTransform, 0f);
            if (name == "Score") _scoreGlow = glow;
            Image pinkRim = UIBuilder.CreateImage(badge, name + " magenta rim", UIBuilder.RingSprite(), new Color(HotPink.r, HotPink.g, HotPink.b, 0.72f)); Stretch(pinkRim.rectTransform, -4f);
            Image fill = UIBuilder.CreateImage(badge, name + " face", UIBuilder.CircleSprite(), Panel); Stretch(fill.rectTransform, 8f);
            Image ring = UIBuilder.CreateImage(badge, name + " ring", UIBuilder.RingSprite(), Cyan); Stretch(ring.rectTransform, 0f);
            if (name == "Score") _scoreRing = ring;
            Image innerBezel = UIBuilder.CreateImage(badge, name + " inner bezel", UIBuilder.RingSprite(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.34f)); Stretch(innerBezel.rectTransform, 14f);
            return badge;
        }

        private void BuildTopStatus(RectTransform root)
        {
            RectTransform strip = NewRect(root, "Race status", new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(300f, 40f));
            Image back = UIBuilder.CreateImage(strip, "Status background", null, new Color(Panel.r, Panel.g, Panel.b, 0.82f)); Stretch(back.rectTransform, 0f);
            Image pinkEdge = UIBuilder.CreateImage(strip, "Status magenta edge", null, HotPink);
            AnchorRect(pinkEdge.rectTransform, new Vector2(0f, 0.96f), new Vector2(1f, 1f));
            Image cyanEdge = UIBuilder.CreateImage(strip, "Status cyan edge", null, Cyan);
            AnchorRect(cyanEdge.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.035f));
            _raceText = UIBuilder.Text(strip, "Race status text", "PRESS ENTER TO START", 13, SkyBlue, TextAnchor.MiddleCenter); Stretch(_raceText.rectTransform, 0f);
            AddNeonText(_raceText, Cyan);
        }

        private void BuildToast(RectTransform root)
        {
            RectTransform r = NewRect(root, "HUD toast", new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(420f, 55f));
            _toast = UIBuilder.Text(r, "Toast text", "", 20, SkyBlue, TextAnchor.MiddleCenter); Stretch(_toast.rectTransform, 0f); AddNeonText(_toast, HotPink); _toast.gameObject.SetActive(false);
        }

        private void BuildCountdown(RectTransform root)
        {
            RectTransform holder = NewRect(root, "Race start countdown", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(420f, 160f));
            _countdownText = UIBuilder.Text(holder, "Countdown text", "3", 86, SkyBlue, TextAnchor.MiddleCenter);
            Stretch(_countdownText.rectTransform, 0f);
            _countdownText.fontStyle = FontStyle.Bold | FontStyle.Italic;
            AddNeonText(_countdownText, Cyan);
            holder.gameObject.SetActive(false);
        }

        public void ShowCountdown(string value)
        {
            if (_countdownText == null) return;
            _countdownText.text = value;
            _countdownText.fontSize = value == "GO!" ? 72 : 86;
            _countdownText.color = value == "GO!" ? new Color32(135, 255, 100, 255) : SkyBlue;
            _countdownText.transform.parent.gameObject.SetActive(true);
        }

        public void HideCountdown()
        {
            if (_countdownText != null) _countdownText.transform.parent.gameObject.SetActive(false);
        }

        private void BuildMenu(RectTransform root)
        {
            _menuOverlay = new GameObject("Pause menu overlay", typeof(RectTransform), typeof(Image));
            _menuOverlay.transform.SetParent(root, false);
            RectTransform overlay = _menuOverlay.GetComponent<RectTransform>(); Stretch(overlay, 0f);
            Image shade = _menuOverlay.GetComponent<Image>(); shade.color = new Color(5f / 255f, 3f / 255f, 22f / 255f, 0.82f); shade.raycastTarget = true;
            RectTransform panel = NewRect(overlay, "Pause menu panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(390f, 430f));
            panel.localScale = TouchScale(430f, 400f);
            Image panelImage = UIBuilder.CreateImage(panel, "Pause panel background", null, Panel); Stretch(panelImage.rectTransform, 0f);
            UIBuilder.Outline(panel, "Pause panel magenta frame", Vector2.zero, Vector2.one, HotPink);
            UIBuilder.Outline(panel, "Pause panel cyan frame", new Vector2(0.012f, 0.012f), new Vector2(0.988f, 0.988f), Cyan);
            Text heading = UIBuilder.Text(panel, "Pause heading", "SYSTEM // PAUSED", 20, SkyBlue, TextAnchor.MiddleCenter); AnchorRect(heading.rectTransform, new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.96f));
            AddNeonText(heading, Cyan);
            MakeMenuButton(panel, "Resume", 0.70f, CloseMenu);
            MakeMenuButton(panel, "Restart", 0.56f, RestartRace);
            MakeMenuButton(panel, "Settings", 0.42f, ToggleSettings);
            MakeMenuButton(panel, "Quit", 0.28f, Quit);
            Text volumeLabel = UIBuilder.Text(panel, "Volume label", "MASTER VOLUME", 11, Cyan, TextAnchor.MiddleCenter); AnchorRect(volumeLabel.rectTransform, new Vector2(0.1f, 0.09f), new Vector2(0.48f, 0.21f));
            AddNeonText(volumeLabel, Cyan);
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
            Image track = UIBuilder.CreateImage(holder, "Volume track", null, new Color(0.04f, 0.09f, 0.16f)); Stretch(track.rectTransform, 0f);
            track.raycastTarget = true;
            Slider slider = holder.gameObject.AddComponent<Slider>(); slider.minValue = 0f; slider.maxValue = 1f; slider.value = _volume;
            slider.targetGraphic = track;
            RectTransform fillArea = NewRect(holder, "Fill area", new Vector2(0f, 0f), Vector2.zero, Vector2.zero); fillArea.anchorMax = new Vector2(1f, 1f); fillArea.offsetMin = new Vector2(0f, 0f); fillArea.offsetMax = Vector2.zero;
            Image fill = UIBuilder.CreateImage(fillArea, "Volume fill", null, Cyan); Stretch(fill.rectTransform, 0f); slider.fillRect = fill.rectTransform;
            RectTransform handle = NewRect(holder, "Volume handle", Vector2.zero, Vector2.zero, new Vector2(24f, 28f));
            Image handleImage = UIBuilder.CreateImage(handle, "Volume handle image", UIBuilder.CircleSprite(), HotPink); handleImage.raycastTarget = true; Stretch(handleImage.rectTransform, 0f); slider.handleRect = handle;
            return slider;
        }

        private void MakeMenuButton(RectTransform panel, string label, float y, UnityEngine.Events.UnityAction action)
        {
            RectTransform row = NewRect(panel, label + " button", new Vector2(0.5f, y), Vector2.zero, new Vector2(260f, 48f));
            Image image = UIBuilder.CreateImage(row, label + " button background", null, new Color(0.025f, 0.045f, 0.09f, 0.94f)); Stretch(image.rectTransform, 0f);
            image.raycastTarget = true;
            UIBuilder.Outline(row, label + " cyan frame", Vector2.zero, Vector2.one, Cyan);
            UIBuilder.Outline(row, label + " magenta frame", new Vector2(0.015f, 0.06f), new Vector2(0.985f, 0.94f), new Color(HotPink.r, HotPink.g, HotPink.b, 0.68f));
            Button button = row.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            ColorBlock colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(0.72f, 1f, 1f); colors.pressedColor = new Color(0.55f, 0.75f, 1f); colors.selectedColor = colors.highlightedColor; button.colors = colors;
            Text text = UIBuilder.Text(row, label + " button label", label, 15, SkyBlue, TextAnchor.MiddleCenter); Stretch(text.rectTransform, 0f);
            AddNeonText(text, Cyan);
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
            if (_audioGlyph != null)
            {
                _audioGlyph.text = _muted ? "◀ ×" : "◀ )))";
                _audioGlyph.color = _muted ? HotPink : Cyan;
            }
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

        private static void AddNeonText(Text text, Color color)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(color.r, color.g, color.b, 0.48f);
            outline.effectDistance = new Vector2(1f, -1f);
            Shadow glow = text.gameObject.AddComponent<Shadow>();
            glow.effectColor = new Color(color.r, color.g, color.b, 0.55f);
            glow.effectDistance = new Vector2(0f, 0f);
        }

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
