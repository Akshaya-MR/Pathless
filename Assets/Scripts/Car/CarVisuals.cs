using System.Collections.Generic;
using UnityEngine;

namespace TheRacer
{
    /// <summary>Procedurally builds the visible cyberpunk vehicle independently of its collider.</summary>
    public sealed class CarVisuals : MonoBehaviour
    {
        private readonly Transform[] _wheels = new Transform[4];
        private Renderer[] _bodyRenderers = new Renderer[2];
        private readonly List<Mesh> _generatedMeshes = new List<Mesh>();
        private MaterialPropertyBlock _bodyBlock;
        private MaterialPropertyBlock _neonBlock;
        private GameObject _modelRoot;
        private Renderer[] _modelRenderers;
        private Texture2D _bodyTexture;
        private Texture2D _bodyMetallic;
        private Texture2D _bodyNormal;
        private Texture2D _tireTexture;
        private Texture2D _tireNormal;
        private Texture2D _wheelTexture;
        private Texture2D _wheelNormal;
        private Texture2D _rivalBodyTexture;
        private Texture2D _rivalBodyMetallic;
        private Texture2D _rivalBodyNormal;
        private Texture2D _rivalPartTexture;
        private Texture2D _rivalPartNormal;
        private CarConfig _config;
        private CarVisualProfile _profile;
        private Light _underglow;
        private Light _leftHeadlight;
        private Light _rightHeadlight;
        private Light _tailLight;
        private Renderer _headlightBars;
        private Renderer _tailStrip;
        private Renderer _underglowStrip;
        private TextMesh _plate;
        private Transform _bodyRoot;
        private Transform _parentOverride;
        private float _wheelSpin;
        private float _damage = 1f;
        private float _brake;

        /// <summary>Wheel transforms in front-left, front-right, rear-left, rear-right order.</summary>
        public Transform[] WheelTransforms => _wheels;

        public void Build(CarConfig config, CarVisualProfile profile)
        {
            _bodyBlock = new MaterialPropertyBlock();
            _neonBlock = new MaterialPropertyBlock();
            _config = config;
            _profile = profile != null ? profile.Copy() : new CarVisualProfile();
            LoadSilverTextures();
            bool hasImportedModel = TryBuildImportedModel();
            if (!hasImportedModel)
            {
                BuildBody();
                BuildWheels();
            }
            BuildLighting();
            BuildPlate();
            ApplyNeonColor();
        }

        private void LoadSilverTextures()
        {
            _bodyTexture = Resources.Load<Texture2D>(ThemeText.SilverBodyTexturePath);
            _bodyMetallic = Resources.Load<Texture2D>(ThemeText.SilverBodyMetallicPath);
            _bodyNormal = Resources.Load<Texture2D>(ThemeText.SilverBodyNormalPath);
            _tireTexture = Resources.Load<Texture2D>(ThemeText.SilverTireTexturePath);
            _tireNormal = Resources.Load<Texture2D>(ThemeText.SilverTireNormalPath);
            _wheelTexture = Resources.Load<Texture2D>(ThemeText.SilverWheelTexturePath);
            _wheelNormal = Resources.Load<Texture2D>(ThemeText.SilverWheelNormalPath);
        }

        private bool TryBuildImportedModel()
        {
            string modelPath = string.IsNullOrWhiteSpace(_profile.modelResourcePath)
                ? ThemeText.SilverCarResourcePath
                : _profile.modelResourcePath;
            GameObject prefab = Resources.Load<GameObject>(modelPath);
            if (prefab == null && _profile.modelResourcePath == ThemeText.FlyingTransportResourcePath)
                prefab = Resources.Load<GameObject>(ThemeText.FlyingTransportAlternateResourcePath);
            if (prefab == null)
                return false;

            _modelRoot = Instantiate(prefab, transform, false);
            _modelRoot.name = _profile.modelResourcePath == ThemeText.SilverCarResourcePath
                || string.IsNullOrWhiteSpace(_profile.modelResourcePath)
                ? ThemeText.SilverCarModelName
                : _profile.handle + " Imported Rival Model";
            Transform modelTransform = _modelRoot.transform;
            modelTransform.localPosition = Vector3.zero;
            modelTransform.localRotation = Quaternion.identity;
            modelTransform.localScale = Vector3.one;
            _modelRenderers = _modelRoot.GetComponentsInChildren<Renderer>(true);
            if (_modelRenderers.Length == 0)
            {
                Destroy(_modelRoot);
                _modelRoot = null;
                return false;
            }

            Bounds bounds = _modelRenderers[0].bounds;
            for (int i = 1; i < _modelRenderers.Length; i++)
                bounds.Encapsulate(_modelRenderers[i].bounds);
            // Imported vehicle packs do not share a forward axis. Normalize the longest
            // horizontal axis to local +Z before sizing the model for the car collider.
            if (bounds.size.x > bounds.size.z)
                modelTransform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            else if (bounds.size.y > bounds.size.z * 1.35f)
                modelTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            modelTransform.localRotation *= Quaternion.Euler(0f, _profile.modelYawCorrection, 0f);
            bounds = _modelRenderers[0].bounds;
            for (int i = 1; i < _modelRenderers.Length; i++)
                bounds.Encapsulate(_modelRenderers[i].bounds);

            float scale = Mathf.Min(3.9f / Mathf.Max(0.1f, bounds.size.z),
                2.35f / Mathf.Max(0.1f, bounds.size.x), 1.9f / Mathf.Max(0.1f, bounds.size.y));
            Vector3 relativeCenter = bounds.center - transform.position;
            float bottomOffset = bounds.min.y - transform.position.y;
            modelTransform.localScale = Vector3.one * scale;
            modelTransform.localPosition = new Vector3(-relativeCenter.x * scale,
                _config.wheelVisualHeight - _config.wheelRadius - bottomOffset * scale,
                -relativeCenter.z * scale);
            if (string.IsNullOrWhiteSpace(_profile.modelResourcePath)
                || _profile.modelResourcePath == ThemeText.SilverCarResourcePath)
                ApplyImportedTextures();
            else
                ApplyRivalImportedTextures();
            _bodyRenderers = _modelRenderers;
            CreateModelNeonAccents(modelTransform, bounds);
            return true;
        }

        private void CreateModelNeonAccents(Transform modelTransform, Bounds modelBounds)
        {
            float halfWidth = Mathf.Clamp(modelBounds.size.x * 0.5f, 0.7f, 1.5f);
            float frontZ = modelBounds.extents.z * 0.9f;
            Transform oldParent = _parentOverride;
            _parentOverride = modelTransform;
            Renderer headlight = CreateBlock(ThemeText.HeadlightName, NeonMaterialFactory.NeonBase,
                new Vector3(0f, 0.2f, frontZ), new Vector3(halfWidth * 1.5f, 0.07f, 0.06f));
            SetTint(headlight, _profile.neonColor, 2.4f);
            Renderer underglow = CreateBlock(ThemeText.UnderglowName, NeonMaterialFactory.NeonBase,
                new Vector3(0f, -0.3f, 0f), new Vector3(halfWidth * 1.4f, 0.035f, modelBounds.size.z * 0.63f));
            SetTint(underglow, _profile.neonColor, 2.2f);
            _parentOverride = oldParent;
        }

        private void ApplyRivalImportedTextures()
        {
            bool isBlackRace = _profile.modelResourcePath == ThemeText.BlackRaceTouringResourcePath;
            string albedoPath = isBlackRace ? ThemeText.BlackRaceTouringAlbedoPath : ThemeText.FlyingTransportAlbedoPath;
            string metallicPath = isBlackRace ? ThemeText.BlackRaceTouringMetallicPath : ThemeText.FlyingTransportMetallicPath;
            string normalPath = isBlackRace ? ThemeText.BlackRaceTouringNormalPath : ThemeText.FlyingTransportNormalPath;
            _rivalBodyTexture = Resources.Load<Texture2D>(albedoPath);
            _rivalBodyMetallic = Resources.Load<Texture2D>(metallicPath);
            _rivalBodyNormal = Resources.Load<Texture2D>(normalPath);
            _rivalPartTexture = isBlackRace
                ? Resources.Load<Texture2D>("CarModels/BlackRaceTouring/Unity Textures/Wheels/BRT_Wheel_AlbedoTransparency")
                : Resources.Load<Texture2D>("CarModels/FlyingTransportVehicle/Unity Textures/Wheels/FTV_Wheel_AlbedoTransparency");
            _rivalPartNormal = isBlackRace
                ? Resources.Load<Texture2D>("CarModels/BlackRaceTouring/Unity Textures/Wheels/BRT_Wheel_Normal")
                : Resources.Load<Texture2D>("CarModels/FlyingTransportVehicle/Unity Textures/Wheels/FTV_Wheel_Normal");

            Material body = null;
            Material parts = null;
            for (int rendererIndex = 0; rendererIndex < _modelRenderers.Length; rendererIndex++)
            {
                Renderer renderer = _modelRenderers[rendererIndex];
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material source = materials[materialIndex];
                    if (source == null) continue;
                    string materialName = source.name;
                    bool part = materialName.IndexOf(ThemeText.TireMaterialKey, System.StringComparison.OrdinalIgnoreCase) >= 0
                        || materialName.IndexOf(ThemeText.WheelMaterialKey, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (part)
                    {
                        if (parts == null)
                            parts = isBlackRace ? NeonMaterialFactory.BlackRaceTouringPart(_rivalPartTexture, _rivalPartNormal)
                                : NeonMaterialFactory.FlyingTransportPart(_rivalPartTexture, _rivalPartNormal);
                        materials[materialIndex] = parts;
                    }
                    else
                    {
                        if (body == null)
                            body = isBlackRace ? NeonMaterialFactory.BlackRaceTouringBody(_rivalBodyTexture, _rivalBodyMetallic, _rivalBodyNormal)
                                : NeonMaterialFactory.FlyingTransportBody(_rivalBodyTexture, _rivalBodyMetallic, _rivalBodyNormal);
                        materials[materialIndex] = body;
                    }
                }
                renderer.sharedMaterials = materials;
            }
        }

        private void ApplyImportedTextures()
        {
            Material bodyMaterial = null;
            Material tireMaterial = null;
            Material wheelMaterial = null;
            for (int rendererIndex = 0; rendererIndex < _modelRenderers.Length; rendererIndex++)
            {
                Renderer renderer = _modelRenderers[rendererIndex];
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                        continue;
                    string materialName = material.name;
                    bool tire = materialName.IndexOf(ThemeText.TireMaterialKey, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool wheel = materialName.IndexOf(ThemeText.WheelMaterialKey, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    Material replacement;
                    if (tire)
                    {
                        if (tireMaterial == null)
                            tireMaterial = NeonMaterialFactory.SilverTire(_tireTexture, _tireNormal);
                        replacement = tireMaterial;
                    }
                    else if (wheel)
                    {
                        if (wheelMaterial == null)
                            wheelMaterial = NeonMaterialFactory.SilverWheel(_wheelTexture, _wheelNormal);
                        replacement = wheelMaterial;
                    }
                    else
                    {
                        if (bodyMaterial == null)
                            bodyMaterial = NeonMaterialFactory.SilverBody(_bodyTexture, _bodyMetallic, _bodyNormal);
                        replacement = bodyMaterial;
                    }
                    materials[materialIndex] = replacement;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private void BuildBody()
        {
            GameObject bodyRoot = new GameObject(ThemeText.BodyGroupName);
            _bodyRoot = bodyRoot.transform;
            _bodyRoot.SetParent(transform, false);
            _parentOverride = _bodyRoot;
            float length = _profile.bodyType == CarBodyType.BruteMK2 ? 4.25f : 3.9f;
            float width = _profile.bodyType == CarBodyType.BruteMK2 || _profile.widebody ? 1.08f : 0.92f;
            if (_profile.bodyType == CarBodyType.GhostCoupe)
                length = 3.65f;
            if (_profile.bodyType == CarBodyType.TurbineGT)
                length = 4.1f;

            Mesh bodyMesh = BuildWedge(width, length, -0.22f, 0.34f, 0.08f);
            _generatedMeshes.Add(bodyMesh);
            _bodyRenderers[0] = CreateMeshPart(ThemeText.CarBodyName,
                bodyMesh, NeonMaterialFactory.MatteBody,
                new Vector3(0f, _config.bodyColliderCenter.y, 0f));
            Mesh glassMesh = BuildWedge(width * 0.66f, length * 0.48f, 0.35f, 0.47f, 0.8f);
            _generatedMeshes.Add(glassMesh);
            _bodyRenderers[1] = CreateMeshPart(ThemeText.CockpitName,
                glassMesh,
                NeonMaterialFactory.DarkGlass, new Vector3(0f, 0f, -0.05f));

            float skirtY = _config.wheelVisualHeight - 0.02f;
            CreateBlock(ThemeText.SideSkirtName, NeonMaterialFactory.WetMetal,
                new Vector3(-width * 0.84f, skirtY, 0f), new Vector3(0.16f, 0.16f, length * 0.68f));
            CreateBlock(ThemeText.SideSkirtName, NeonMaterialFactory.WetMetal,
                new Vector3(width * 0.84f, skirtY, 0f), new Vector3(0.16f, 0.16f, length * 0.68f));
            CreateBlock(ThemeText.DiffuserName, NeonMaterialFactory.WetMetal,
                new Vector3(0f, _config.wheelVisualHeight + 0.02f, -length * 0.47f), new Vector3(width * 1.55f, 0.16f, 0.4f));

            if (_profile.spoiler || _profile.bodyType == CarBodyType.BruteMK2)
            {
                float wingWidth = _profile.bodyType == CarBodyType.BruteMK2 ? width * 2.1f : width * 1.8f;
                CreateBlock(ThemeText.WingSupportName, NeonMaterialFactory.WetMetal,
                    new Vector3(-0.46f, 0.54f, -length * 0.36f), new Vector3(0.09f, 0.48f, 0.11f));
                CreateBlock(ThemeText.WingSupportName, NeonMaterialFactory.WetMetal,
                    new Vector3(0.46f, 0.54f, -length * 0.36f), new Vector3(0.09f, 0.48f, 0.11f));
                CreateBlock(ThemeText.RearWingName, NeonMaterialFactory.MatteBody,
                    new Vector3(0f, 0.8f, -length * 0.39f), new Vector3(wingWidth, 0.12f, 0.42f));
            }

            if (_profile.bodyType == CarBodyType.TurbineGT)
                CreateBlock(ThemeText.IntakeName, NeonMaterialFactory.WetMetal,
                    new Vector3(0f, 0.47f, -0.35f), new Vector3(0.42f, 0.22f, 0.48f));
            if (_profile.bodyType == CarBodyType.BruteMK2)
            {
                CreateBlock(ThemeText.IntakeName, NeonMaterialFactory.WetMetal,
                    new Vector3(-0.69f, 0.16f, length * 0.34f), new Vector3(0.24f, 0.22f, 0.42f));
                CreateBlock(ThemeText.IntakeName, NeonMaterialFactory.WetMetal,
                    new Vector3(0.69f, 0.16f, length * 0.34f), new Vector3(0.24f, 0.22f, 0.42f));
            }

            _parentOverride = null;
            ApplyBodyDamage(1f);
        }

        private void BuildWheels()
        {
            _wheels[0] = null;
            _wheels[1] = null;
            _wheels[2] = null;
            _wheels[3] = null;
            float side = _profile.widebody || _profile.bodyType == CarBodyType.BruteMK2
                ? Mathf.Max(_config.wheelSideOffset, 1.1f) : _config.wheelSideOffset;
            float axle = _config.wheelAxleOffset;
            for (int i = 0; i < _wheels.Length; i++)
            {
                bool left = i == 0 || i == 2;
                bool front = i < 2;
                Vector3 position = new Vector3(left ? -side : side, _config.wheelVisualHeight, front ? axle : -axle);
                Transform wheel = CreatePrimitive(ThemeText.WheelName, PrimitiveType.Cylinder,
                    NeonMaterialFactory.WheelRubber, position,
                    new Vector3(_config.wheelWidth * 0.5f, _config.wheelRadius, _config.wheelRadius),
                    Quaternion.Euler(0f, 0f, 90f));
                _wheels[i] = wheel;
                CreatePrimitive(ThemeText.WheelRimName, PrimitiveType.Cylinder, NeonMaterialFactory.NeonBase,
                    position, new Vector3(_config.wheelWidth * 0.56f, _config.wheelRadius * 0.62f,
                        _config.wheelRadius * 0.62f), Quaternion.Euler(0f, 0f, 90f), true);
            }
        }

        private void BuildLighting()
        {
            _underglowStrip = CreateBlock(ThemeText.UnderglowName, NeonMaterialFactory.NeonBase,
                new Vector3(0f, _config.wheelVisualHeight - 0.12f, 0f), new Vector3(1.42f, 0.045f, 2.65f));
            _underglow = CreatePointLight(ThemeText.UnderglowLightName,
                new Vector3(0f, _config.wheelVisualHeight - 0.05f, 0f), 4.8f, 3.7f);

            _headlightBars = CreateBlock(ThemeText.HeadlightName, NeonMaterialFactory.NeonBase,
                new Vector3(0f, 0.11f, 1.79f), new Vector3(1.22f, 0.075f, 0.08f));
            _leftHeadlight = CreatePointLight(ThemeText.HeadlightLightName, new Vector3(-0.48f, 0.16f, 1.76f), 1.7f, 5.5f);
            _rightHeadlight = CreatePointLight(ThemeText.HeadlightLightName, new Vector3(0.48f, 0.16f, 1.76f), 1.7f, 5.5f);
            _tailStrip = CreateBlock(ThemeText.TailLightName, NeonMaterialFactory.NeonBase,
                new Vector3(0f, 0.17f, -1.86f), new Vector3(1.3f, 0.09f, 0.08f));
            _tailLight = CreatePointLight(ThemeText.TailLightLightName, new Vector3(0f, 0.18f, -1.84f), 0.65f, 2.1f);
        }

        private void BuildPlate()
        {
            GameObject plateObject = new GameObject(ThemeText.LicensePlateName);
            plateObject.transform.SetParent(transform, false);
            plateObject.transform.localPosition = new Vector3(0f, 0.19f, -1.94f);
            plateObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _plate = plateObject.AddComponent<TextMesh>();
            _plate.text = string.IsNullOrWhiteSpace(_profile.handle) ? ThemeText.DefaultDriverHandle : _profile.handle;
            _plate.anchor = TextAnchor.MiddleCenter;
            _plate.alignment = TextAlignment.Center;
            _plate.fontSize = 42;
            _plate.characterSize = 0.045f;
            _plate.color = _profile.neonColor;
            _plate.GetComponent<MeshRenderer>().sharedMaterial = NeonMaterialFactory.PlateMaterial;
            _plate.GetComponent<MeshRenderer>().sharedMaterial.color = Color.white;
            _plate.GetComponent<MeshRenderer>().sharedMaterial.EnableKeyword(ThemeText.EmissionKeyword);
            _plate.GetComponent<MeshRenderer>().sharedMaterial.SetColor(ThemeText.EmissionProperty, Color.white);
        }

        /// <summary>Animates wheel steering and spin from the physics car state.</summary>
        public void UpdateWheels(float forwardSpeed, float steerAngle, float deltaTime)
        {
            if (_config == null)
                return;
            _wheelSpin += forwardSpeed / Mathf.Max(0.1f, _config.wheelRadius) * Mathf.Rad2Deg * deltaTime;
            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i] == null)
                    continue;
                _wheels[i].localRotation = Quaternion.Euler(0f, i < 2 ? steerAngle : 0f, 90f)
                    * Quaternion.AngleAxis(_wheelSpin, Vector3.up);
            }
        }

        /// <summary>Changes shared neon renderers using per-car property blocks.</summary>
        public void ApplyNeonColor()
        {
            if (_profile == null)
                return;
            SetNeon(_underglowStrip);
            SetNeon(_headlightBars);
            SetTint(_tailStrip, ThemePalette.AlertRed, 0.9f);
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name == ThemeText.WheelRimName)
                    SetNeon(child.GetComponent<Renderer>());
            }
            if (_plate != null)
                _plate.color = _profile.neonColor;
            if (_underglow != null)
                _underglow.color = _profile.neonColor;
            if (_leftHeadlight != null)
                _leftHeadlight.color = _profile.neonColor;
            if (_rightHeadlight != null)
                _rightHeadlight.color = _profile.neonColor;
            if (_tailLight != null)
                _tailLight.color = ThemePalette.AlertRed;
        }

        /// <summary>Darkens body panels and dims cosmetic lights as health falls.</summary>
        public void ApplyDamage(float healthRatio)
        {
            _damage = Mathf.Clamp01(healthRatio);
            ApplyBodyDamage(_damage);
        }

        /// <summary>Dev-only F1 inspection cycle across four body silhouettes and three neon hues.</summary>
        public void CycleDevProfile()
        {
            _profile.bodyType = (CarBodyType)(((int)_profile.bodyType + 1) % 4);
            int colorIndex = ((int)_profile.bodyType + 1) % 3;
            _profile.neonColor = colorIndex == 0 ? ThemePalette.NeonCyan
                : colorIndex == 1 ? ThemePalette.NeonMagenta : ThemePalette.ToxicGreen;
            _profile.widebody = _profile.bodyType == CarBodyType.BruteMK2;
            _profile.spoiler = _profile.bodyType != CarBodyType.GhostCoupe;
            if (_modelRoot != null)
            {
                ApplyBodyDamage(_damage);
                ApplyNeonColor();
                return;
            }
            if (_bodyRoot != null)
                Destroy(_bodyRoot.gameObject);
            BuildBody();
            ApplyNeonColor();
        }

        /// <summary>Updates brake light brightness and damage flicker.</summary>
        public void SetBrake(float brake)
        {
            _brake = Mathf.Clamp01(brake);
        }

        /// <summary>Updates brake light brightness and damage flicker.</summary>
        public void SetDamageFlicker(float flicker)
        {
            if (_tailLight != null)
                _tailLight.intensity = Mathf.Lerp(0.65f, 5.2f, _brake) * flicker;
            if (_tailStrip != null)
                SetTint(_tailStrip, ThemePalette.AlertRed, Mathf.Lerp(0.65f, 2.4f, _brake) * flicker);
            if (_leftHeadlight != null)
                _leftHeadlight.intensity = 1.7f * flicker;
            if (_rightHeadlight != null)
                _rightHeadlight.intensity = 1.7f * flicker;
            if (_underglow != null)
                _underglow.intensity = 3.7f * Mathf.Lerp(0.35f, 1f, _damage) * flicker;
        }

        private void ApplyBodyDamage(float healthRatio)
        {
            Color silverPaint = new Color(0.68f, 0.72f, 0.78f, 1f);
            Color damagedColor = _modelRoot != null
                ? Color.Lerp(ThemePalette.Void, silverPaint, 0.35f + healthRatio * 0.65f)
                : Color.Lerp(ThemePalette.Void, _profile != null ? _profile.bodyColor : ThemePalette.PanelDark,
                    0.45f + healthRatio * 0.55f);
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                Renderer renderer = _bodyRenderers[i];
                if (renderer == null)
                    continue;
                if (_modelRoot != null)
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    {
                        renderer.GetPropertyBlock(_bodyBlock, materialIndex);
                        _bodyBlock.SetColor(ThemeText.BaseColorProperty, damagedColor);
                        _bodyBlock.SetColor(ThemeText.ColorProperty, damagedColor);
                        renderer.SetPropertyBlock(_bodyBlock, materialIndex);
                    }
                }
                else
                {
                    renderer.GetPropertyBlock(_bodyBlock);
                    _bodyBlock.SetColor(ThemeText.BaseColorProperty, damagedColor);
                    _bodyBlock.SetColor(ThemeText.ColorProperty, damagedColor);
                    renderer.SetPropertyBlock(_bodyBlock);
                }
            }
        }

        private void SetNeon(Renderer renderer, float intensity = 2.2f)
        {
            if (renderer == null)
                return;
            SetTint(renderer, _profile.neonColor, intensity);
        }

        private void SetTint(Renderer renderer, Color tint, float intensity)
        {
            if (renderer == null)
                return;
            renderer.GetPropertyBlock(_neonBlock);
            _neonBlock.SetColor(ThemeText.BaseColorProperty, tint);
            _neonBlock.SetColor(ThemeText.ColorProperty, tint);
            _neonBlock.SetColor(ThemeText.EmissionProperty, ThemePalette.Neon(tint, intensity));
            renderer.SetPropertyBlock(_neonBlock);
        }

        private Renderer CreateMeshPart(string objectName, Mesh mesh, Material material, Vector3 position)
        {
            GameObject part = new GameObject(objectName);
            part.transform.SetParent(_parentOverride != null ? _parentOverride : transform, false);
            part.transform.localPosition = position;
            MeshFilter filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private Renderer CreateBlock(string objectName, Material material, Vector3 position, Vector3 scale)
        {
            return CreatePrimitive(objectName, PrimitiveType.Cube, material, position, scale, Quaternion.identity, true)
                .GetComponent<Renderer>();
        }

        private Transform CreatePrimitive(string objectName, PrimitiveType type, Material material, Vector3 position,
            Vector3 scale, Quaternion rotation, bool emissive = false)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(_parentOverride != null ? _parentOverride : transform, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.transform.localRotation = rotation;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            Renderer renderer = primitive.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            if (emissive)
                SetNeon(renderer);
            return primitive.transform;
        }

        private Light CreatePointLight(string objectName, Vector3 position, float range, float intensity)
        {
            GameObject lightObject = new GameObject(objectName);
            lightObject.transform.SetParent(_parentOverride != null ? _parentOverride : transform, false);
            lightObject.transform.localPosition = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = _profile.neonColor;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Mesh BuildWedge(float halfWidth, float halfLength, float lowerY, float upperY, float crownY)
        {
            Vector3[] vertices =
            {
                new Vector3(-halfWidth * 0.78f, lowerY, -halfLength), new Vector3(halfWidth * 0.78f, lowerY, -halfLength),
                new Vector3(-halfWidth * 0.78f, lowerY, halfLength), new Vector3(halfWidth * 0.78f, lowerY, halfLength),
                new Vector3(-halfWidth, upperY * 0.62f, -halfLength * 0.62f), new Vector3(halfWidth, upperY * 0.62f, -halfLength * 0.62f),
                new Vector3(-halfWidth * 0.88f, crownY, -halfLength * 0.08f), new Vector3(halfWidth * 0.88f, crownY, -halfLength * 0.08f),
                new Vector3(-halfWidth * 0.7f, upperY, halfLength), new Vector3(halfWidth * 0.7f, upperY, halfLength)
            };
            int[] triangles =
            {
                0, 2, 3, 0, 3, 1,
                0, 1, 5, 0, 5, 4,
                2, 8, 9, 2, 9, 3,
                0, 4, 6, 0, 6, 8, 0, 8, 2,
                1, 3, 9, 1, 9, 7, 1, 7, 5,
                4, 5, 7, 4, 7, 6,
                6, 7, 9, 6, 9, 8
            };
            Mesh mesh = new Mesh { name = ThemeText.WedgeMeshName, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _generatedMeshes.Count; i++)
            {
                if (_generatedMeshes[i] == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(_generatedMeshes[i]);
                else
                    DestroyImmediate(_generatedMeshes[i]);
            }
            _generatedMeshes.Clear();
        }
    }
}
