using UnityEngine;
using UnityEngine.UI;

namespace TheRacer
{
    /// <summary>Procedural cyber HUD. Display objects are built once and updated from cached race values.</summary>
    public sealed class HUD : MonoBehaviour
    {
        private const int ArcCount = 30;
        private static readonly byte[] DigitMasks = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };
        private static readonly string[] TwoDigitCache = BuildTwoDigitCache();
        private static readonly string[] ThreeDigitCache = BuildThreeDigitCache();
        private readonly Image[] _arcSegments = new Image[ArcCount];
        private readonly Image[][] _speedSegments = new Image[3][];
        private NeonText _lapText, _positionText;
        private Text _currentTimeText, _bestTimeText, _distanceText, _remainingText;
        private Text _zoneText, _wrongWayText, _toastText, _startPrompt, _dodgesText;
        private Image _coreFill;
        private CarController _car;
        private DriftBoostSystem _boost;
        private RollingTrack _track;
        private TrackData _trackData;
        private RacePositionTracker _positions;
        private RacerState _playerState;
        private TrackSpawner _spawner;
        private string[] _distanceCache;
        private string[] _remainingCache;
        private float _lapLength = 560f;
        private float _raceDistance = 1500f;
        private float _lapStartedAt;
        private float _bestLap = -1f;
        private int _observedLap;
        private int _shownSpeed = -1, _shownDistance = -1, _shownRemaining = -1, _shownZone = -1;
        private int _shownPosition = -1, _shownLap = -1, _shownDodges = -1, _shownCurrentTimeBucket = -1, _shownBestTimeBucket = -1;
        private float _wrongWayTime, _toastRemaining;
        private Color _toastColor;
        private bool _raceStarted;
        private bool _wasCoreReady;

        public void Configure(CarController car, RollingTrack track, TrackData data, RaceConfig config, TrackSpawner spawner)
        {
            _car = car;
            _boost = car != null ? car.GetComponent<DriftBoostSystem>() : null;
            _track = track;
            _trackData = data;
            _spawner = spawner;
            _lapLength = data != null ? Mathf.Max(1f, data.lapLength) : 560f;
            _raceDistance = config != null ? Mathf.Max(1f, config.raceDistance) : 1500f;
            _observedLap = -1;
            _lapStartedAt = 0f;
            _bestLap = -1f;
            BuildDistanceTextCache();
            InvalidateDisplayCache();
        }

        public void SetRaceState(RacePositionTracker positions, RacerState playerState)
        {
            _positions = positions;
            _playerState = playerState;
            InvalidateDisplayCache();
        }

        public void SetRaceStarted(bool started)
        {
            _raceStarted = started;
            if (_startPrompt != null) _startPrompt.gameObject.SetActive(!started);
        }

        private void Awake()
        {
            RectTransform root = (RectTransform)transform;
            Color panel = HudStyle.WithAlpha(ThemePalette.PanelDark, HudStyle.PanelAlpha);
            UIBuilder.Panel(root, ThemeText.HudSpeedPanelName, new Vector2(0.025f, 0.035f), new Vector2(0.265f, 0.37f), Vector2.zero, panel);
            UIBuilder.SoftGlow(root, ThemeText.HudSpeedPanelName + " Glow", new Vector2(0.025f, 0.035f), new Vector2(0.265f, 0.37f), ThemePalette.NeonCyan);
            UIBuilder.Outline(root, ThemeText.HudSpeedPanelName + " Outline", new Vector2(0.025f, 0.035f), new Vector2(0.265f, 0.37f), ThemePalette.NeonCyan);
            UIBuilder.CornerBrackets(root, ThemeText.HudSpeedPanelName + " Brackets", new Vector2(0.025f, 0.035f), new Vector2(0.265f, 0.37f), ThemePalette.NeonCyan);
            UIBuilder.Panel(root, ThemeText.HudRacePanelName, new Vector2(0.29f, 0.84f), new Vector2(0.98f, 0.98f), Vector2.zero, panel);
            UIBuilder.Outline(root, ThemeText.HudRacePanelName + " Outline", new Vector2(0.29f, 0.84f), new Vector2(0.98f, 0.98f), ThemePalette.NeonCyan);
            UIBuilder.CornerBrackets(root, ThemeText.HudRacePanelName + " Brackets", new Vector2(0.29f, 0.84f), new Vector2(0.98f, 0.98f), ThemePalette.NeonMagenta);
            BuildSpeedometer(root);

            Text speedUnit = MakeText(root, ThemeText.HudSpeed, ThemeText.HudSpeed, 19, ThemePalette.HudMuted,
                TextAnchor.MiddleCenter, new Vector2(0.075f, 0.055f), new Vector2(0.19f, 0.095f));
            speedUnit.fontStyle = FontStyle.Bold;
            MakeText(root, ThemeText.HudOverclock, ThemeText.HudOverclock, 20, ThemePalette.NeonMagenta,
                TextAnchor.MiddleLeft, new Vector2(0.32f, 0.905f), new Vector2(0.53f, 0.97f));
            UIBuilder.Panel(root, ThemeText.HudCorePanelName, new Vector2(0.32f, 0.865f), new Vector2(0.53f, 0.90f), Vector2.zero, panel);
            _coreFill = UIBuilder.Bar(root, ThemeText.HudCorePanelName + " Fill", ThemePalette.NeonMagenta);
            SetAnchors(_coreFill.rectTransform, new Vector2(0.32f, 0.865f), new Vector2(0.53f, 0.90f));

            _lapText = new NeonText(root, ThemeText.HudLapCounter, ThemeText.HudInitialLap, 26, ThemePalette.AcidYellow,
                TextAnchor.MiddleCenter, new Vector2(0.55f, 0.89f), new Vector2(0.68f, 0.97f), null);
            MakeText(root, ThemeText.HudCurrentLapTime, ThemeText.HudCurrentLapTime, 16, ThemePalette.HudMuted,
                TextAnchor.MiddleCenter, new Vector2(0.68f, 0.85f), new Vector2(0.82f, 0.89f));
            _currentTimeText = MakeText(root, ThemeText.HudCurrentLapTime, ThemeText.HudCurrentTimeInitial, 22, ThemePalette.HudText,
                TextAnchor.MiddleCenter, new Vector2(0.68f, 0.89f), new Vector2(0.82f, 0.97f));
            MakeText(root, ThemeText.HudBestLapTime, ThemeText.HudBestLapTime, 16, ThemePalette.HudMuted,
                TextAnchor.MiddleCenter, new Vector2(0.82f, 0.85f), new Vector2(0.97f, 0.89f));
            _bestTimeText = MakeText(root, ThemeText.HudBestLapTime, ThemeText.HudBestTimeInitial, 22, ThemePalette.NeonMagenta,
                TextAnchor.MiddleCenter, new Vector2(0.82f, 0.89f), new Vector2(0.97f, 0.97f));
            _positionText = new NeonText(root, ThemeText.HudPosition, ThemeText.HudPositionInitial, 22, ThemePalette.NeonCyan,
                TextAnchor.MiddleCenter, new Vector2(0.85f, 0.80f), new Vector2(0.98f, 0.85f), null);
            _zoneText = MakeText(root, ThemeText.HudZoneNeonStrip, ThemeText.HudZoneNeonStrip, 18, ThemePalette.NeonMagenta,
                TextAnchor.MiddleCenter, new Vector2(0.70f, 0.80f), new Vector2(0.84f, 0.85f));
            UIBuilder.Panel(root, ThemeText.HudRacePanelName + " Distance Panel", new Vector2(0.72f, 0.02f), new Vector2(0.98f, 0.18f), Vector2.zero, panel);
            UIBuilder.Outline(root, ThemeText.HudRacePanelName + " Distance Outline", new Vector2(0.72f, 0.02f), new Vector2(0.98f, 0.18f), ThemePalette.NeonCyan);
            MakeText(root, ThemeText.HudDistanceTitle, ThemeText.HudDistanceTitle, 16, ThemePalette.HudMuted,
                TextAnchor.MiddleCenter, new Vector2(0.74f, 0.16f), new Vector2(0.96f, 0.18f));
            _distanceText = MakeText(root, ThemeText.HudDistanceTitle, ThemeText.HudDistanceInitial, 25, ThemePalette.HudText,
                TextAnchor.MiddleCenter, new Vector2(0.74f, 0.10f), new Vector2(0.96f, 0.16f));
            _remainingText = MakeText(root, ThemeText.HudToGoInitial, ThemeText.HudToGoInitial, 18, ThemePalette.NeonCyan,
                TextAnchor.MiddleCenter, new Vector2(0.74f, 0.065f), new Vector2(0.96f, 0.105f));
            _dodgesText = MakeText(root, ThemeText.HudDodges, ThemeText.HudDodgesInitial, 18, ThemePalette.ToxicGreen,
                TextAnchor.MiddleCenter, new Vector2(0.74f, 0.025f), new Vector2(0.96f, 0.065f));
            _wrongWayText = MakeText(root, ThemeText.HudWrongWay, ThemeText.HudWrongWay, 30, ThemePalette.AlertRed,
                TextAnchor.MiddleCenter, new Vector2(0.35f, 0.70f), new Vector2(0.65f, 0.80f));
            _wrongWayText.gameObject.SetActive(false);
            _toastText = MakeText(root, ThemeText.HudToast, string.Empty, 28, ThemePalette.AcidYellow,
                TextAnchor.MiddleCenter, new Vector2(0.25f, 0.59f), new Vector2(0.75f, 0.68f));
            _startPrompt = MakeText(root, ThemeText.HudStartPrompt, ThemeText.HudStartPrompt, 30, ThemePalette.AcidYellow,
                TextAnchor.MiddleCenter, new Vector2(0.3f, 0.46f), new Vector2(0.7f, 0.55f));
            _startPrompt.gameObject.SetActive(!_raceStarted);
            InvalidateDisplayCache();
        }

        private void BuildSpeedometer(RectTransform root)
        {
            Vector2 center = new Vector2(0.145f, 0.205f);
            const float radius = 103f;
            for (int i = 0; i < ArcCount; i++)
            {
                float angle = (225f + 270f * i / (ArcCount - 1)) * Mathf.Deg2Rad;
                Image segment = UIBuilder.CreateImage(root, ThemeText.HudArcSegment + i, null, new Color(ThemePalette.HudMuted.r, ThemePalette.HudMuted.g, ThemePalette.HudMuted.b, 0.24f));
                RectTransform rect = segment.rectTransform;
                rect.anchorMin = rect.anchorMax = center;
                rect.sizeDelta = new Vector2(19f, 6f);
                rect.anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                rect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
                _arcSegments[i] = segment;
            }

            for (int digit = 0; digit < 3; digit++)
            {
                GameObject digitObject = new GameObject(ThemeText.HudDigit + digit, typeof(RectTransform));
                digitObject.transform.SetParent(root, false);
                RectTransform digitRect = digitObject.GetComponent<RectTransform>();
                digitRect.anchorMin = digitRect.anchorMax = center;
                digitRect.sizeDelta = new Vector2(34f, 62f);
                digitRect.anchoredPosition = new Vector2((digit - 1) * 40f, 0f);
                _speedSegments[digit] = new Image[7];
                for (int part = 0; part < 7; part++)
                {
                    Image segment = UIBuilder.CreateImage(digitObject.transform, ThemeText.HudDigitSegment + part, null, ThemePalette.NeonCyan);
                    RectTransform rect = segment.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = part == 0 || part == 3 || part == 6 ? new Vector2(22f, 5f) : new Vector2(5f, 22f);
                    rect.anchoredPosition = SegmentPosition(part);
                    _speedSegments[digit][part] = segment;
                }
            }
        }

        private static Vector2 SegmentPosition(int segment)
        {
            switch (segment)
            {
                case 0: return new Vector2(0f, 27f);
                case 1: return new Vector2(12f, 14f);
                case 2: return new Vector2(12f, -14f);
                case 3: return new Vector2(0f, -27f);
                case 4: return new Vector2(-12f, -14f);
                case 5: return new Vector2(-12f, 14f);
                default: return Vector2.zero;
            }
        }

        private static Text MakeText(Transform parent, string name, string value, int size, Color color, TextAnchor anchor, Vector2 min, Vector2 max)
        {
            Text text = UIBuilder.Text(parent, name, value, size, color, anchor);
            text.fontStyle = FontStyle.Bold;
            SetAnchors(text.rectTransform, min, max);
            return text;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }

        private void OnEnable()
        {
            Toast.Attach(this);
            FxEvents.OnDriftBonus += OnDriftBonus;
            FxEvents.OnOverclockStart += OnOverclockStart;
            FxEvents.OnPlayerReset += OnPlayerReset;
        }

        private void OnDisable()
        {
            Toast.Detach(this);
            FxEvents.OnDriftBonus -= OnDriftBonus;
            FxEvents.OnOverclockStart -= OnOverclockStart;
            FxEvents.OnPlayerReset -= OnPlayerReset;
        }

        private void Update()
        {
            if (_car == null || _track == null) return;
            UpdateSpeedReadout(Mathf.Clamp(Mathf.RoundToInt(_car.SpeedKMH), 0, 999));
            UpdateSpeedArc(_car.SpeedRatio);
            float coreRatio = _car.OverclockCore / Mathf.Max(1f, _car.OverclockCapacity);
            if (_coreFill != null)
            {
                _coreFill.fillAmount = coreRatio;
                bool ready = coreRatio >= 0.999f || (_boost != null && _boost.IsOverclocking);
                _coreFill.color = ready ? ThemePalette.AcidYellow : ThemePalette.NeonMagenta;
            }

            float distance = _positions != null ? _positions.GetProgress(_car) : _track.DistanceTravelled;
            float totalRaceTime = _playerState != null ? _playerState.TotalRaceTime : 0f;
            int lapIndex = Mathf.FloorToInt(distance / _lapLength);
            if (lapIndex > _observedLap)
            {
                if (_observedLap >= 0 && _raceStarted)
                {
                    float completedTime = totalRaceTime - _lapStartedAt;
                    if (_bestLap < 0f || completedTime < _bestLap) _bestLap = completedTime;
                }
                _observedLap = lapIndex;
                _lapStartedAt = totalRaceTime;
                _shownBestTimeBucket = -1;
                _shownCurrentTimeBucket = -1;
            }

            int totalLaps = Mathf.CeilToInt(_raceDistance / _lapLength);
            int displayedLap = Mathf.Min(totalLaps, lapIndex + 1);
            if (displayedLap != _shownLap)
            {
                _shownLap = displayedLap;
                _lapText.Set(ThemeText.HudLapCounter + " " + displayedLap + ThemeText.HudPositionFormatSlash + totalLaps);
            }
            int currentTimeBucket = Mathf.FloorToInt(Mathf.Max(0f, totalRaceTime - _lapStartedAt) * 10f);
            if (currentTimeBucket != _shownCurrentTimeBucket)
            {
                _shownCurrentTimeBucket = currentTimeBucket;
                _currentTimeText.text = FormatTime(currentTimeBucket / 10f);
            }
            int bestTimeBucket = _bestLap >= 0f ? Mathf.FloorToInt(_bestLap * 10f) : -1;
            if (bestTimeBucket != _shownBestTimeBucket)
            {
                _shownBestTimeBucket = bestTimeBucket;
                _bestTimeText.text = _bestLap >= 0f ? FormatTime(bestTimeBucket / 10f) : ThemeText.HudBestTimeInitial;
            }

            int travelled = Mathf.Clamp(Mathf.FloorToInt(distance), 0, _distanceCache.Length - 1);
            int remaining = Mathf.Clamp(Mathf.CeilToInt(_raceDistance - distance), 0, _remainingCache.Length - 1);
            if (travelled != _shownDistance) { _shownDistance = travelled; _distanceText.text = _distanceCache[travelled]; }
            if (remaining != _shownRemaining) { _shownRemaining = remaining; _remainingText.text = _playerState != null && _playerState.Finished ? ThemeText.HudRaceFinished : _remainingCache[remaining]; }
            int dodges = _spawner != null ? _spawner.ObstaclesDodged : 0;
            if (dodges != _shownDodges) { _shownDodges = dodges; _dodgesText.text = ThemeText.HudDodges + " " + ThreeDigitCache[Mathf.Clamp(dodges, 0, 999)]; }
            int position = _positions != null ? _positions.GetPosition(_car) : 1;
            if (position != _shownPosition)
            {
                _shownPosition = position;
                int racerCount = _positions != null ? _positions.RacerCount : 1;
                _positionText.Set(ThemeText.HudPosition + " " + Ordinal(position) + ThemeText.HudPositionFormatSlash + racerCount);
            }
            int zone = _trackData != null ? (int)_trackData.GetZone(distance % _lapLength) : 0;
            if (zone != _shownZone) { _shownZone = zone; _zoneText.text = ZoneName((TrackZone)zone); }
            bool wrong = Vector3.Dot(_car.transform.forward, Vector3.forward) < -0.3f;
            _wrongWayTime = wrong ? _wrongWayTime + Time.deltaTime : 0f;
            bool showWrong = _wrongWayTime >= 1.5f && (Mathf.FloorToInt(Time.time * 3f) % 2 == 0);
            if (_wrongWayText.gameObject.activeSelf != showWrong) _wrongWayText.gameObject.SetActive(showWrong);
            if (_toastRemaining > 0f)
            {
                _toastRemaining = Mathf.Max(0f, _toastRemaining - Time.deltaTime);
                Color color = _toastColor; color.a = Mathf.Min(1f, _toastRemaining * 1.7f); _toastText.color = color;
                if (_toastRemaining <= 0f && _toastText.gameObject.activeSelf) _toastText.gameObject.SetActive(false);
            }
        }

        private void UpdateSpeedReadout(int speed)
        {
            if (speed == _shownSpeed) return;
            _shownSpeed = speed;
            for (int d = 0; d < _speedSegments.Length; d++)
            {
                int digit = d == 0 ? speed / 100 : d == 1 ? speed / 10 % 10 : speed % 10;
                for (int s = 0; s < 7; s++)
                    _speedSegments[d][s].color = (DigitMasks[digit] & (1 << s)) != 0 ? ThemePalette.NeonCyan : new Color(ThemePalette.HudMuted.r, ThemePalette.HudMuted.g, ThemePalette.HudMuted.b, 0.16f);
            }
        }

        private void UpdateSpeedArc(float ratio)
        {
            int lit = Mathf.Clamp(Mathf.RoundToInt(ratio * ArcCount), 0, ArcCount);
            bool flash = (Mathf.FloorToInt(Time.unscaledTime * 4f) & 1) == 0;
            for (int i = 0; i < ArcCount; i++)
            {
                if (i >= lit) _arcSegments[i].color = new Color(ThemePalette.HudMuted.r, ThemePalette.HudMuted.g, ThemePalette.HudMuted.b, 0.24f);
                else if (i >= ArcCount - 5) _arcSegments[i].color = flash ? ThemePalette.NeonMagenta : ThemePalette.AlertRed;
                else _arcSegments[i].color = ThemePalette.NeonCyan;
            }
        }

        public void ShowToast(string value, Color color)
        { _toastText.text = value.ToUpperInvariant(); _toastColor = color; _toastText.color = color; _toastRemaining = 2.1f; _toastText.gameObject.SetActive(true); }

        private void InvalidateDisplayCache()
        {
            _shownSpeed = _shownDistance = _shownRemaining = _shownZone = _shownPosition = _shownLap = _shownDodges = -1;
            _shownCurrentTimeBucket = _shownBestTimeBucket = -1;
        }

        private void OnPlayerReset()
        {
            _observedLap = 0;
            _lapStartedAt = 0f;
            _bestLap = -1f;
            InvalidateDisplayCache();
            Toast.Show(ThemeText.ToastReset, ThemePalette.AlertRed);
        }

        private static string FormatTime(float seconds)
        {
            int hundredths = Mathf.Max(0, Mathf.FloorToInt(seconds * 100f));
            int minutes = Mathf.Min(99, hundredths / 6000);
            int secondsPart = hundredths / 100 % 60;
            int fraction = hundredths % 100;
            return TwoDigitCache[minutes] + ThemeText.TimeSeparator + TwoDigitCache[secondsPart]
                + ThemeText.TimeDecimal + TwoDigitCache[fraction];
        }

        private static string[] BuildTwoDigitCache()
        {
            string[] cache = new string[100];
            for (int i = 0; i < cache.Length; i++) cache[i] = i.ToString(ThemeText.HudTwoDigitFormat);
            return cache;
        }

        private static string[] BuildThreeDigitCache()
        {
            string[] cache = new string[1000];
            for (int i = 0; i < cache.Length; i++) cache[i] = i.ToString(ThemeText.HudSpeedFormat);
            return cache;
        }

        private void BuildDistanceTextCache()
        {
            int max = Mathf.Clamp(Mathf.CeilToInt(_raceDistance), 1, 20000);
            _distanceCache = new string[max + 1];
            _remainingCache = new string[max + 1];
            for (int i = 0; i <= max; i++)
            {
                string digits = i.ToString(ThemeText.HudDistanceFormat);
                _distanceCache[i] = digits + ThemeText.HudMetersSuffix;
                _remainingCache[i] = digits + ThemeText.HudToGoSuffix;
            }
        }

        private static string Ordinal(int value)
        {
            int lastTwo = value % 100;
            string suffix = lastTwo >= 11 && lastTwo <= 13 ? ThemeText.OrdinalTh : value % 10 == 1 ? ThemeText.OrdinalSt
                : value % 10 == 2 ? ThemeText.OrdinalNd : value % 10 == 3 ? ThemeText.OrdinalRd : ThemeText.OrdinalTh;
            return value + suffix;
        }

        private static string ZoneName(TrackZone zone)
        {
            switch (zone)
            {
                case TrackZone.Docks: return ThemeText.HudZoneDocks;
                case TrackZone.Undercity: return ThemeText.HudZoneUndercity;
                case TrackZone.CorpSpire: return ThemeText.HudZoneCorpSpire;
                default: return ThemeText.HudZoneNeonStrip;
            }
        }

        private static void OnDriftBonus(int score) { if (score > 0) Toast.Show(ThemeText.ToastDrift, ThemePalette.NeonMagenta); }
        private static void OnOverclockStart() { Toast.Show(ThemeText.ToastOverclock, ThemePalette.AcidYellow); }
    }
}
