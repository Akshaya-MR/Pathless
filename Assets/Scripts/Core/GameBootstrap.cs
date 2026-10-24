using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheRacer
{
    /// <summary>Single entry point that constructs and owns the Part 01 runtime scene.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Serializable]
        private sealed class LightingConfig
        {
            [Min(0f)] public float ambientIntensity = 0.16f;
            [Min(0f)] public float cyanIntensity = 1.65f;
            [Min(0f)] public float magentaIntensity = 0.9f;
        }

        [SerializeField] private CarConfig carConfig = new CarConfig();
        [SerializeField] private CameraRigConfig cameraConfig = new CameraRigConfig();
        [SerializeField] private TestRoadConfig roadConfig = new TestRoadConfig();
        [SerializeField] private TrackData trackData = new TrackData();
        [SerializeField] private CarVisualProfile carVisualProfile = new CarVisualProfile();
        [SerializeField] private VehicleId playerVehicleId = VehicleId.SilverCoupe;
        [SerializeField] private LightingConfig lightingConfig = new LightingConfig();
        [SerializeField] private RaceConfig raceConfig = new RaceConfig();
        [SerializeField] private RoadsideDecorationLayout roadsideLayout;
        [SerializeField, Tooltip("Optional replacement for the default neon boost-arrow texture in Resources/BoostTile.")]
        private Texture2D boosterTileTexture = null;
        [SerializeField, Range(120f, 360f), Tooltip("Change the size of the lower-corner speed and score circles.")]
        private float hudCornerBadgeSize = 360f;
        [SerializeField, Range(-220f, 120f), Tooltip("Horizontal offset from each screen edge. Negative values move the circles off-screen.")]
        private float hudCornerHorizontalOffset = -73f;
        [SerializeField, Range(-100f, 180f), Tooltip("Vertical offset from the bottom edge of the screen.")]
        private float hudCornerVerticalOffset = -100f;
        [SerializeField] private int pickupSeed = 2087;

        private readonly List<Material> _runtimeMaterials = new List<Material>();
        private readonly List<Mesh> _runtimeMeshes = new List<Mesh>();
        private readonly Material[] _zoneMaterials = new Material[4];
        private Material _roadMaterial;
        private Material _gridMaterial;
        private Material _obstacleMaterial;
        private Material _coneMaterial;
        private Material _finishMaterial;
        private GameObject _trackRoot;
        private RollingTrack _rollingTrack;
        private CameraRig _cameraRig;
        private CarController _carController;
        private PlayerCarInput _playerInput;
        private RivalAI[] _rivals;
        private CarController[] _rivalCars;
        private RacePositionTracker _racePositions;
        private RacerState _playerRaceState;
        private bool _raceStarted;
        private Coroutine _countdownRoutine;
        private RaceAudioController _raceAudio;
        private Color _previousAmbientLight;
        private float _previousAmbientIntensity;
        private AmbientMode _previousAmbientMode;
        private Material _previousSkybox;
        private Light _previousSun;
        private Color _previousFogColor;
        private bool _previousFog;

        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            SynthwaveHUD hud = GetComponentInChildren<SynthwaveHUD>(true);
            if (hud != null)
                hud.SetCornerBadgeLayout(hudCornerBadgeSize, hudCornerHorizontalOffset, hudCornerVerticalOffset);
        }

        private void Awake()
        {
            name = ThemeText.BootstrapName;
            if (GetComponent<TestModeController>() == null)
                gameObject.AddComponent<TestModeController>();
            CaptureRenderSettings();
            ConfigureEnvironment();
            BuildLights();
            BuildWorld();
            _raceAudio = gameObject.AddComponent<RaceAudioController>();
            _raceAudio.Configure(_carController);
        }

        private void CaptureRenderSettings()
        {
            _previousAmbientLight = RenderSettings.ambientLight;
            _previousAmbientIntensity = RenderSettings.ambientIntensity;
            _previousAmbientMode = RenderSettings.ambientMode;
            _previousSkybox = RenderSettings.skybox;
            _previousSun = RenderSettings.sun;
            _previousFogColor = RenderSettings.fogColor;
            _previousFog = RenderSettings.fog;
        }

        private void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ThemePalette.Void;
            RenderSettings.ambientIntensity = lightingConfig.ambientIntensity;
            // The skybox shader samples Unity's fog color for its horizon even when scene fog is off.
            // The default gray fog color caused the gray band outside the road.
            RenderSettings.fogColor = new Color(0.12f, 0.004f, 0.012f, 1f);
            // Keep the project's assigned skybox so imported URP sky packs remain visible in Play mode.
            RenderSettings.fog = false;
        }

        private void BuildLights()
        {
            // The retrowave skybox places its sun from Unity's active scene sun direction.
            // Keep it just above the horizon, like the supplied reference image.
            RenderSettings.sun = CreateDirectionalLight(ThemeText.KeyLightName, ThemePalette.NeonCyan,
                lightingConfig.cyanIntensity, Quaternion.Euler(2f, 180f, 0f));
            // RetrowaveSkybox Lite reads this legacy global directly; URP does not populate it
            // consistently for custom sky shaders. Use the light-to-sun direction so the disc
            // sits ahead of the racers, not behind the world they are driving into.
            Vector3 sunDirection = -RenderSettings.sun.transform.forward;
            Shader.SetGlobalVector("_WorldSpaceLightPos0", new Vector4(sunDirection.x, sunDirection.y,
                sunDirection.z, 0f));
            CreateDirectionalLight(ThemeText.FillLightName, ThemePalette.NeonMagenta, lightingConfig.magentaIntensity,
                Quaternion.Euler(24f, 145f, 0f));
        }

        private Light CreateDirectionalLight(string objectName, Color color, float intensity, Quaternion rotation)
        {
            GameObject lightObject = new GameObject(objectName);
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = rotation;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            light.renderMode = LightRenderMode.Auto;
            return light;
        }

        private void BuildWorld()
        {
            _roadMaterial = CreateMaterial(ThemePalette.PanelDark, 0.55f, 0.82f, ThemePalette.NoEmission, ThemeText.UrpLitShader);
            Color horizonRed = new Color(1f, 0.015f, 0.005f, 1f);
            _gridMaterial = CreateMaterial(horizonRed, 0f, 0.5f,
                ThemePalette.Neon(horizonRed, 1.7f), ThemeText.UrpLitShader);
            _obstacleMaterial = CreateMaterial(ThemePalette.Void, 0.1f, 0.72f, ThemePalette.NoEmission, ThemeText.UrpLitShader);
            _coneMaterial = CreateMaterial(ThemePalette.AcidYellow, 0.05f, 0.45f,
                ThemePalette.Neon(ThemePalette.AcidYellow, 0.45f), ThemeText.UrpLitShader);
            _zoneMaterials[(int)TrackZone.NeonStrip] = CreateZoneMaterial(ThemePalette.NeonCyan);
            _zoneMaterials[(int)TrackZone.Docks] = CreateZoneMaterial(ThemePalette.NeonMagenta);
            _zoneMaterials[(int)TrackZone.Undercity] = CreateZoneMaterial(ThemePalette.ElectricPurple);
            _zoneMaterials[(int)TrackZone.CorpSpire] = CreateZoneMaterial(ThemePalette.ToxicGreen);
            _finishMaterial = CreateMaterial(ThemePalette.AlertRed, 0f, 0.4f,
                ThemePalette.Neon(ThemePalette.AlertRed, 2f), ThemeText.UrpLitShader);
            trackData.Configure(roadConfig.length, roadConfig.width);
            BuildCar();
            BuildTrack();
            BuildRivalsAndRace();
        }

        private Material CreateZoneMaterial(Color color)
        {
            return CreateMaterial(color, 0f, 0.45f, ThemePalette.Neon(color, 1.65f), ThemeText.UrpLitShader);
        }

        private void BuildCar()
        {
            GameObject carObject = new GameObject(ThemeText.CarName);
            carObject.transform.SetParent(transform, false);
            float startZ = roadConfig.centerZ - roadConfig.length * 0.5f;
            carObject.transform.position = new Vector3(0f, carConfig.spawnHeight, startZ + 7f);
            carObject.SetActive(false);
            _playerInput = carObject.AddComponent<PlayerCarInput>();
            _playerInput.SetRaceReleased(false);
            _carController = carObject.AddComponent<CarController>();
            _carController.Configure(carConfig, VehicleRegistry.Get(playerVehicleId).CreateVisualProfile(carVisualProfile));
            carObject.SetActive(true);
            BuildCamera(_carController, _playerInput);
        }

        private void BuildTrack()
        {
            if (_raceAudio != null) _raceAudio.StopRaceMusic();
            if (_countdownRoutine != null)
            {
                StopCoroutine(_countdownRoutine);
                _countdownRoutine = null;
            }
            if (_trackRoot != null)
                DestroyRuntimeObject(_trackRoot);
            ReleaseRuntimeMeshes();
            _trackRoot = new GameObject("Track Runtime");
            _trackRoot.transform.SetParent(transform, false);
            _rollingTrack = TestRoad.Build(this, _trackRoot.transform, roadConfig, trackData, _roadMaterial, _gridMaterial,
                _obstacleMaterial, _coneMaterial, _zoneMaterials, _carController.transform);
            if (roadsideLayout == null)
                roadsideLayout = GetComponentInChildren<RoadsideDecorationLayout>(true);
            if (roadsideLayout != null)
            {
                roadsideLayout.gameObject.SetActive(true);
                _rollingTrack.CopyRoadsideLayout(roadsideLayout.transform,
                    roadsideLayout.repeatSegmentLength, roadsideLayout.repeatCount);
                roadsideLayout.gameObject.SetActive(false);
            }
            _rollingTrack.RegisterWorldShiftCar(_carController);
            if (_rivalCars != null)
                for (int i = 0; i < _rivalCars.Length; i++) _rollingTrack.RegisterWorldShiftCar(_rivalCars[i]);
            if (_cameraRig != null) _cameraRig.BindTrack(_rollingTrack);
            TrackSpawner spawner = _trackRoot.AddComponent<TrackSpawner>();
            spawner.Configure(_trackRoot.transform, _rollingTrack, _carController,
                _carController.GetComponent<DriftBoostSystem>(), this, pickupSeed, boosterTileTexture);
            SynthwaveHUD hud = GetComponentInChildren<SynthwaveHUD>(true);
            if (hud == null)
            {
                Canvas canvas = GetComponentInChildren<Canvas>(true);
                if (canvas == null) canvas = UIBuilder.CreateCanvas(transform);
                hud = canvas.gameObject.AddComponent<SynthwaveHUD>();
            }
            hud.gameObject.SetActive(true);
            hud.enabled = true;
            Canvas hudCanvas = hud.GetComponent<Canvas>();
            if (hudCanvas != null) { hudCanvas.enabled = true; hudCanvas.overrideSorting = true; hudCanvas.sortingOrder = 100; }
            hud.Configure(_carController, _rollingTrack, trackData, raceConfig, spawner,
                hudCornerBadgeSize, hudCornerHorizontalOffset, hudCornerVerticalOffset);
            EnsureScreenOverlay();

            if (_racePositions != null)
            {
                _racePositions.RebindTrack(_rollingTrack);
                _raceStarted = false;
                SynthwaveHUD resetHud = GetComponentInChildren<SynthwaveHUD>(true);
                if (resetHud != null) resetHud.HideCountdown();
                _playerInput.SetRaceReleased(false);
                for (int i = 0; i < _rivals.Length; i++) _rivals[i].BindTrack(_rollingTrack);
                _racePositions.SetRaceStarted(false);
                SynthwaveHUD activeHud = GetComponentInChildren<SynthwaveHUD>();
                activeHud.SetRaceState(_racePositions, _racePositions.GetState(_carController));
                activeHud.SetRaceStarted(false);
            }
        }

        private void EnsureScreenOverlay()
        {
            ScreenOverlay overlay = GetComponentInChildren<ScreenOverlay>(true);
            if (overlay != null)
            {
                overlay.gameObject.SetActive(true);
                return;
            }
            Canvas canvas = UIBuilder.CreateCanvas(transform, 90);
            canvas.gameObject.name = ThemeText.HudOverlayCanvasName;
            ScreenOverlay screenOverlay = canvas.gameObject.AddComponent<ScreenOverlay>();
            screenOverlay.Build();
        }

        private void BuildRivalsAndRace()
        {
            _rivals = new RivalAI[RivalRoster.ActiveRivalCount];
            _rivalCars = new CarController[RivalRoster.ActiveRivalCount];
            GameObject rivalRoot = new GameObject(ThemeText.RivalRootName);
            rivalRoot.transform.SetParent(transform, false);
            Vector3 start = _carController.transform.position;
            System.Random vehicleRandom = new System.Random();
            VehicleDefinition[] aiVehicles = VehicleRegistry.GetAiVehicleLineup(
                playerVehicleId, vehicleRandom, _rivals.Length);
            for (int i = 0; i < _rivals.Length; i++)
            {
                RivalDefinition identity = RivalRoster.All[i];
                VehicleDefinition vehicle = aiVehicles[i];
                GameObject rivalObject = new GameObject(identity.Handle + ThemeText.RivalCarNameSuffix);
                rivalObject.transform.SetParent(rivalRoot.transform, false);
                float side = i % 2 == 0 ? -1f : 1f;
                // Two rows of two cars keep all four AI racers separate at the start.
                int row = i / 2;
                float longitudinalOffset = -4.5f - row * 4.5f;
                Vector3 gridPosition = start + _carController.transform.right * (side * 4.5f)
                    + _carController.transform.forward * longitudinalOffset;
                rivalObject.transform.SetPositionAndRotation(gridPosition, _carController.transform.rotation);
                rivalObject.SetActive(false);
                RivalAI ai = rivalObject.AddComponent<RivalAI>();
                CarController car = rivalObject.AddComponent<CarController>();
                CarVisualProfile rivalProfile = identity.CreateVisualProfile();
                rivalProfile.modelResourcePath = vehicle.ModelResourcePath;
                rivalProfile.modelYawCorrection = vehicle.ModelYawCorrection;
                car.Configure(carConfig, rivalProfile);
                car.ConfigureInput(ai);
                ai.Configure(car, _rollingTrack, _carController, identity, raceConfig,
                    raceConfig.rivalSkillMultiplier, pickupSeed + 101 + i, side * 3.2f);
                rivalObject.SetActive(true);
                _rivals[i] = ai;
                _rivalCars[i] = car;
                _rollingTrack.RegisterWorldShiftCar(car);
            }

            _racePositions = gameObject.AddComponent<RacePositionTracker>();
            _racePositions.Configure(_rollingTrack, raceConfig, _carController);
            _playerRaceState = _racePositions.RegisterRacer(_carController);
            for (int i = 0; i < _rivals.Length; i++)
                _rivals[i].SetRaceState(_racePositions, _racePositions.RegisterRacer(_rivalCars[i]));
            SynthwaveHUD hud = GetComponentInChildren<SynthwaveHUD>();
            hud.SetRaceState(_racePositions, _playerRaceState);
            hud.SetRaceStarted(false);
            _playerInput.SetRaceReleased(false);
        }

        private void Update()
        {
            if (_playerInput != null && _playerInput.DevTrackRebuildPressed)
                BuildTrack();
            SynthwaveHUD hud = GetComponentInChildren<SynthwaveHUD>();
            if (_playerInput != null && _playerInput.PausePressed && hud != null)
                hud.ToggleMenu();
            if (!_raceStarted && (hud == null || !hud.IsMenuOpen) && _playerInput != null && _playerInput.RaceStartPressed)
                if (_countdownRoutine == null) _countdownRoutine = StartCoroutine(PlayStartCountdown());
        }

        /// <summary>Restarts the race and rebuilds the rolling track from the original start grid.</summary>
        public void RestartRace()
        {
            Time.timeScale = 1f;
            if (_carController != null) _carController.ResetToSpawn();
            if (_rivalCars != null)
                for (int i = 0; i < _rivalCars.Length; i++)
                    if (_rivalCars[i] != null) _rivalCars[i].ResetToSpawn();
            BuildTrack();
            SynthwaveHUD hud = GetComponentInChildren<SynthwaveHUD>();
            if (hud != null) hud.SetScore(0);
        }

        private void ReleaseRace()
        {
            _raceStarted = true;
            if (_raceAudio != null) _raceAudio.PlayRaceMusic();
            _playerInput.SetRaceReleased(true);
            _racePositions.SetRaceStarted(true);
            for (int i = 0; i < _rivals.Length; i++) _rivals[i].SetRaceStarted(true);
            SynthwaveHUD hud = GetComponentInChildren<SynthwaveHUD>();
            if (hud != null) hud.SetRaceStarted(true);
            Toast.Show(ThemeText.RaceLiveToast, ThemePalette.ToxicGreen);
        }

        private IEnumerator PlayStartCountdown()
        {
            SynthwaveHUD hud = GetComponentInChildren<SynthwaveHUD>();
            for (int count = 3; count > 0; count--)
            {
                if (hud != null) hud.ShowCountdown(count.ToString());
                if (_raceAudio != null)
                    _raceAudio.PlayCountdownCue(count);
                yield return new WaitForSecondsRealtime(1f);
            }
            if (hud != null) hud.ShowCountdown("GO!");
            if (_raceAudio != null) _raceAudio.PlayGoCue();
            ReleaseRace();
            yield return new WaitForSecondsRealtime(0.55f);
            if (hud != null) hud.HideCountdown();
            _countdownRoutine = null;
        }

        private void BuildCamera(CarController target, PlayerCarInput input)
        {
            // Scene components serialize their own camera values, so apply the requested
            // modest closer chase framing here instead of relying on config defaults.
            cameraConfig.chaseHeight = 3.0f;
            cameraConfig.chaseDistance = 6.4f;
            cameraConfig.speedDistanceBonus = 3.0f;
            cameraConfig.baseFieldOfView = 62f;
            cameraConfig.maxFieldOfView = 74f;
            GameObject cameraObject = new GameObject(ThemeText.CameraName);
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = ThemeText.MainCameraTag;
            cameraObject.transform.position = new Vector3(0f, 4f, -8f);
            _cameraRig = cameraObject.AddComponent<CameraRig>();
            _cameraRig.Configure(target, input, cameraConfig);
            Camera[] existingCameras = FindObjectsByType<Camera>();
            for (int i = 0; i < existingCameras.Length; i++)
                if (existingCameras[i] != _cameraRig.GetComponent<Camera>())
                    existingCameras[i].enabled = false;
            AudioListener listener = cameraObject.AddComponent<AudioListener>();
            AudioListener[] existingListeners = FindObjectsByType<AudioListener>();
            for (int i = 0; i < existingListeners.Length; i++)
                if (existingListeners[i] != listener) existingListeners[i].enabled = false;
        }

        /// <summary>Creates a runtime material and tracks it for cleanup.</summary>
        public Material CreateMaterial(Color baseColor, float metallic, float smoothness, Color emission, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                shader = Shader.Find(ThemeText.UrpLitShader);
            if (shader == null)
                shader = Shader.Find(ThemeText.StandardShader);
            if (shader == null)
                shader = Shader.Find(ThemeText.UnlitColorShader);

            Material material = new Material(shader);
            material.color = baseColor;
            SetColor(material, ThemeText.BaseColorProperty, baseColor);
            SetColor(material, ThemeText.ColorProperty, baseColor);
            SetFloat(material, ThemeText.MetallicProperty, metallic);
            SetFloat(material, ThemeText.SmoothnessProperty, smoothness);
            if (emission.maxColorComponent > 0.001f)
            {
                material.EnableKeyword(ThemeText.EmissionKeyword);
                SetColor(material, ThemeText.EmissionProperty, emission);
            }
            _runtimeMaterials.Add(material);
            return material;
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
                material.SetColor(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        /// <summary>Registers a generated mesh for destruction with this runtime scene.</summary>
        public void RegisterRuntimeMesh(Mesh mesh)
        {
            if (mesh != null)
                _runtimeMeshes.Add(mesh);
        }

        private void OnDestroy()
        {
            RenderSettings.ambientLight = _previousAmbientLight;
            RenderSettings.ambientIntensity = _previousAmbientIntensity;
            RenderSettings.ambientMode = _previousAmbientMode;
            RenderSettings.skybox = _previousSkybox;
            RenderSettings.sun = _previousSun;
            RenderSettings.fogColor = _previousFogColor;
            RenderSettings.fog = _previousFog;
            for (int i = 0; i < _runtimeMaterials.Count; i++)
                DestroyRuntimeObject(_runtimeMaterials[i]);
            for (int i = 0; i < _runtimeMeshes.Count; i++)
                DestroyRuntimeObject(_runtimeMeshes[i]);
            _runtimeMaterials.Clear();
            _runtimeMeshes.Clear();
            UIBuilder.Release();
            NeonMaterialFactory.ReleaseAll();
        }

        private void ReleaseRuntimeMeshes()
        {
            for (int i = 0; i < _runtimeMeshes.Count; i++)
                DestroyRuntimeObject(_runtimeMeshes[i]);
            _runtimeMeshes.Clear();
        }

        private static void DestroyRuntimeObject(UnityEngine.Object runtimeObject)
        {
            if (runtimeObject == null)
                return;
            if (Application.isPlaying)
                Destroy(runtimeObject);
            else
                DestroyImmediate(runtimeObject);
        }
    }

    /// <summary>Editor-refreshed list of music clips discovered in Assets/music.</summary>
    public sealed class MusicPlaylistCatalog : ScriptableObject
    {
        [SerializeField] private AudioClip[] clips = new AudioClip[0];
        [SerializeField] private AudioClip engineStartClip;
        public AudioClip[] Clips => clips;
        public AudioClip EngineStartClip => engineStartClip;
        public void SetClips(AudioClip[] value) => clips = value ?? new AudioClip[0];
        public void SetEngineStartClip(AudioClip value) => engineStartClip = value;
    }

    /// <summary>Race music and countdown/collision sound effects for the runtime race.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class RaceAudioController : MonoBehaviour
    {
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.38f;
        [Range(0f, 1f)] [SerializeField] private float effectsVolume = 0.8f;
        [SerializeField, Min(1f)] private float collisionPlayerRadius = 30f;
        private AudioSource _musicSource;
        private AudioSource _effectsSource;
        private CarController _playerCar;
        private AudioClip[] _clips;
        private int _lastClip = -1;
        private AudioClip _engineStartClip;
        private AudioClip _impactClip;
        private AudioClip _countdownClip;
        private AudioClip _goClip;
        private AudioClip _fallbackMusicClip;
        private float _lastImpactTime = -10f;
        private bool _raceMusicStarted;

        private void Awake()
        {
            _musicSource = GetComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = false;
            _musicSource.volume = musicVolume;
            _effectsSource = gameObject.AddComponent<AudioSource>();
            _effectsSource.playOnAwake = false;
            _effectsSource.loop = false;
            _effectsSource.spatialBlend = 0f;
            _effectsSource.volume = effectsVolume;
            MusicPlaylistCatalog catalog = Resources.Load<MusicPlaylistCatalog>("MusicPlaylist");
            _clips = catalog != null ? catalog.Clips : null;
            _engineStartClip = catalog != null ? catalog.EngineStartClip : null;
            _impactClip = CreateImpactClip();
            _countdownClip = CreateToneClip("Countdown Beep", 660f, 0.13f);
            _goClip = CreateToneClip("Race Go Beep", 990f, 0.34f);
            _fallbackMusicClip = CreateFallbackMusicClip();
        }

        public void Configure(CarController playerCar) => _playerCar = playerCar;

        private void Update()
        {
            if (_raceMusicStarted && !_musicSource.isPlaying) PlayRaceMusic();
        }

        public void PlayCountdownCue(int count)
        {
            if (count <= 0) return;
            // Keep each number clearly audible over the engine, with the level rising into GO.
            float countdownLevel = Mathf.Clamp(effectsVolume * 1.45f, 0f, 1f);
            float engineLevel = Mathf.Clamp(effectsVolume * (1.05f + (3 - count) * 0.12f), 0f, 1f);
            _effectsSource.PlayOneShot(_countdownClip, countdownLevel);
            if (_engineStartClip != null)
                _effectsSource.PlayOneShot(_engineStartClip, engineLevel);
        }

        public void PlayGoCue() => _effectsSource.PlayOneShot(_goClip, Mathf.Clamp(effectsVolume * 1.5f, 0f, 1f));

        public void PlayRaceMusic()
        {
            if (_clips == null || _clips.Length == 0)
            {
                _musicSource.clip = _fallbackMusicClip;
                _musicSource.loop = true;
                _musicSource.Play();
                _raceMusicStarted = true;
                return;
            }
            _musicSource.loop = false;
            int next = _clips.Length == 1 ? 0 : UnityEngine.Random.Range(0, _clips.Length - 1);
            if (_clips.Length > 1 && next >= _lastClip) next++;
            _lastClip = next;
            _musicSource.clip = _clips[next];
            _musicSource.Play();
            _raceMusicStarted = true;
        }

        public void StopRaceMusic() { _raceMusicStarted = false; _musicSource.Stop(); }

        public static void NotifyCarCollision(CarController firstCar, Collision collision)
        {
            RaceAudioController audio = FindAnyObjectByType<RaceAudioController>();
            if (audio == null || audio._playerCar == null || firstCar == null || collision == null) return;
            CarController otherCar = collision.collider.GetComponentInParent<CarController>();
            if (otherCar == null || otherCar == firstCar || collision.relativeVelocity.magnitude < 2f) return;
            Vector3 contact = collision.contactCount > 0 ? collision.GetContact(0).point : firstCar.transform.position;
            if ((contact - audio._playerCar.transform.position).sqrMagnitude > audio.collisionPlayerRadius * audio.collisionPlayerRadius) return;
            if (Time.unscaledTime - audio._lastImpactTime < 0.16f) return;
            audio._lastImpactTime = Time.unscaledTime;
            audio._effectsSource.PlayOneShot(audio._impactClip, audio.effectsVolume);
        }

        private static AudioClip CreateToneClip(string clipName, float frequency, float duration)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Min(1f, i / (sampleRate * 0.008f)) * Mathf.Clamp01((duration - t) / 0.025f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.24f;
            }
            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateImpactClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.24f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            uint noise = 0x6D2B79F5;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                float random = ((noise & 0xffff) / 32767.5f) - 1f;
                float envelope = Mathf.Exp(-t * 19f);
                float thump = Mathf.Sin(2f * Mathf.PI * (105f - 55f * t) * t);
                samples[i] = (thump * 0.62f + random * 0.38f) * envelope * 0.48f;
            }
            AudioClip clip = AudioClip.Create("Neon Car Impact", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateFallbackMusicClip()
        {
            const int sampleRate = 22050;
            const float bpm = 108f;
            const int bars = 8;
            float beatDuration = 60f / bpm;
            int sampleCount = Mathf.CeilToInt(sampleRate * beatDuration * 4f * bars);
            float[] samples = new float[sampleCount];
            float[] roots = { 110f, 87.31f, 130.81f, 98f }; // A, F, C, G
            float[] scale = { 1f, 1.1892f, 1.3348f, 1.4983f, 1.7818f, 2f, 2.3784f, 2.6696f };
            uint noise = 0x1B873593;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float beat = t / beatDuration;
                int beatIndex = Mathf.FloorToInt(beat);
                int bar = (beatIndex / 4) % 4;
                float beatPhase = beat - beatIndex;
                int step = Mathf.FloorToInt(beat * 2f) % 8;
                float stepPhase = beat * 2f - Mathf.Floor(beat * 2f);
                float root = roots[bar];
                float bassEnvelope = Mathf.Exp(-beatPhase * 5f);
                float bass = Mathf.Sin(2f * Mathf.PI * root * t) * bassEnvelope * 0.11f;
                float leadFrequency = root * 2f * scale[(step + bar * 2) % scale.Length];
                float leadEnvelope = Mathf.Min(1f, stepPhase * 18f) * Mathf.Exp(-stepPhase * 4.2f);
                float lead = Mathf.Sin(2f * Mathf.PI * leadFrequency * t) * leadEnvelope * 0.055f;
                float pad = (Mathf.Sin(2f * Mathf.PI * root * 2f * t)
                    + Mathf.Sin(2f * Mathf.PI * root * 2.5f * t)
                    + Mathf.Sin(2f * Mathf.PI * root * 3f * t)) * 0.012f;
                float kick = 0f;
                if (beatIndex % 4 == 0 || beatIndex % 4 == 2)
                    kick = Mathf.Sin(2f * Mathf.PI * (48f + 55f * Mathf.Exp(-beatPhase * 18f)) * t)
                        * Mathf.Exp(-beatPhase * 12f) * 0.17f;
                float snare = 0f;
                if (beatIndex % 4 == 1 || beatIndex % 4 == 3)
                {
                    noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                    float random = ((noise & 0xffff) / 32767.5f) - 1f;
                    snare = random * Mathf.Exp(-beatPhase * 20f) * 0.045f;
                }
                samples[i] = Mathf.Clamp((bass + lead + pad + kick + snare) * 1.8f, -0.75f, 0.75f);
            }
            AudioClip clip = AudioClip.Create("Default Synthwave Race Loop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
