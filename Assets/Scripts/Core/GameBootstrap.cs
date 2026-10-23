using System;
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
        [SerializeField] private LightingConfig lightingConfig = new LightingConfig();
        [SerializeField] private RaceConfig raceConfig = new RaceConfig();
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
        private Color _previousAmbientLight;
        private float _previousAmbientIntensity;
        private AmbientMode _previousAmbientMode;
        private Material _previousSkybox;
        private Light _previousSun;
        private Color _previousFogColor;
        private bool _previousFog;

        private void Awake()
        {
            name = ThemeText.BootstrapName;
            if (GetComponent<TestModeController>() == null)
                gameObject.AddComponent<TestModeController>();
            CaptureRenderSettings();
            ConfigureEnvironment();
            BuildLights();
            BuildWorld();
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
            _carController.Configure(carConfig, carVisualProfile);
            carObject.SetActive(true);
            BuildCamera(_carController, _playerInput);
        }

        private void BuildTrack()
        {
            if (_trackRoot != null)
                DestroyRuntimeObject(_trackRoot);
            ReleaseRuntimeMeshes();
            _trackRoot = new GameObject("Track Runtime");
            _trackRoot.transform.SetParent(transform, false);
            _rollingTrack = TestRoad.Build(this, _trackRoot.transform, roadConfig, trackData, _roadMaterial, _gridMaterial,
                _obstacleMaterial, _coneMaterial, _zoneMaterials, _carController.transform);
            _rollingTrack.RegisterWorldShiftCar(_carController);
            if (_rivalCars != null)
                for (int i = 0; i < _rivalCars.Length; i++) _rollingTrack.RegisterWorldShiftCar(_rivalCars[i]);
            if (_cameraRig != null) _cameraRig.BindTrack(_rollingTrack);
            TrackSpawner spawner = _trackRoot.AddComponent<TrackSpawner>();
            spawner.Configure(_trackRoot.transform, _rollingTrack, _carController, _carController.GetComponent<DriftBoostSystem>(), this, pickupSeed);
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
            hud.Configure(_carController, _rollingTrack, trackData, raceConfig, spawner);
            EnsureScreenOverlay();

            if (_racePositions != null)
            {
                _racePositions.RebindTrack(_rollingTrack);
                _raceStarted = false;
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
            for (int i = 0; i < _rivals.Length; i++)
            {
                RivalDefinition identity = RivalRoster.All[i];
                GameObject rivalObject = new GameObject(identity.Handle + ThemeText.RivalCarNameSuffix);
                rivalObject.transform.SetParent(rivalRoot.transform, false);
                float side = i == 0 ? -1f : 1f;
                // Stagger the grid so the wide imported transport cannot overlap the player
                // or the other rival at the start line.
                float longitudinalOffset = i == 0 ? -4.5f : -9f;
                Vector3 gridPosition = start + _carController.transform.right * (side * 5f)
                    + _carController.transform.forward * longitudinalOffset;
                rivalObject.transform.SetPositionAndRotation(gridPosition, _carController.transform.rotation);
                rivalObject.SetActive(false);
                RivalAI ai = rivalObject.AddComponent<RivalAI>();
                CarController car = rivalObject.AddComponent<CarController>();
                car.Configure(carConfig, identity.CreateVisualProfile());
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
                ReleaseRace();
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
            _playerInput.SetRaceReleased(true);
            _racePositions.SetRaceStarted(true);
            for (int i = 0; i < _rivals.Length; i++) _rivals[i].SetRaceStarted(true);
            SynthwaveHUD hud = GetComponentInChildren<SynthwaveHUD>();
            if (hud != null) hud.SetRaceStarted(true);
            Toast.Show(ThemeText.RaceLiveToast, ThemePalette.ToxicGreen);
        }

        private void BuildCamera(CarController target, PlayerCarInput input)
        {
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
}
