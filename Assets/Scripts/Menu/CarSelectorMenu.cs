using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheRacer
{
    /// <summary>Registry-backed selector with an isolated, render-texture 3D preview copy.</summary>
    public sealed class CarSelectorMenu : MonoBehaviour
    {
        private const string SelectionKey = "Pathless_SelectedCar";
        private const int PreviewLayer = 29;
        private VehicleDefinition[] _vehicles;
        private int _index;
        private float _switchLock;
        private Transform _model;
        private Transform _stage;
        private Transform _ring;
        private Camera _camera;
        private RenderTexture _renderTexture;
        private RawImage _preview;
        private float _flashRemaining;
        private readonly List<Material> _previewMaterials = new List<Material>();

        public event Action<int> SelectionChanged;
        public VehicleDefinition Selected => _vehicles != null && _vehicles.Length > 0 ? _vehicles[_index] : null;
        public int Index => _index;
        public int Count => _vehicles != null ? _vehicles.Length : 0;
        public bool IsSwitching => _switchLock > 0f;

        public void Initialize(RawImage preview)
        {
            _preview = preview;
            _vehicles = VehicleRegistry.All;
            if (_vehicles == null || _vehicles.Length == 0) _vehicles = new[] { VehicleRegistry.Get(VehicleId.SilverCoupe) };
            _index = Mathf.Clamp(PlayerPrefs.GetInt(SelectionKey, 0), 0, _vehicles.Length - 1);
            BuildPreviewStage();
            ShowSelectedModel();
            SelectionChanged?.Invoke(_index);
        }

        private void Update()
        {
            if (!MainMenuController.IsMenuOpen) return;
            float dt = Time.unscaledDeltaTime;
            _switchLock = Mathf.Max(0f, _switchLock - dt);
            _flashRemaining = Mathf.Max(0f, _flashRemaining - dt);
            if (_model != null) _model.Rotate(0f, 20f * dt, 0f, Space.World);
            if (_ring != null)
            {
                float glow = _flashRemaining > 0f ? 2.8f : 1.25f + .35f * Mathf.Sin(Time.unscaledTime * 1.4f);
                SetEmission(_ring.GetComponent<Renderer>().sharedMaterial, MenuColors.Cyan * glow);
                float scale = _switchLock > 0f ? .97f : 1f;
                if (_model != null) _model.localScale = Vector3.Lerp(_model.localScale, Vector3.one * scale, 16f * dt);
            }
        }

        public void Move(int direction)
        {
            if (!MainMenuController.IsMenuOpen || _switchLock > 0f || Count < 2 || direction == 0) return;
            _index = (_index + (direction > 0 ? 1 : -1) + Count) % Count;
            _switchLock = .35f;
            _flashRemaining = .15f;
            PlayerPrefs.SetInt(SelectionKey, _index);
            PlayerPrefs.Save();
            ShowSelectedModel();
            SelectionChanged?.Invoke(_index);
        }

        private void BuildPreviewStage()
        {
            GameObject stage = new GameObject("PATHLESS Menu Preview Stage");
            _stage = stage.transform;
            _stage.position = new Vector3(0f, -1000f, 0f);
            GameObject cameraObject = new GameObject("PATHLESS Preview Camera");
            cameraObject.transform.SetParent(_stage, false);
            cameraObject.transform.position = _stage.position + new Vector3(0f, 2.45f, 8.2f);
            cameraObject.transform.LookAt(_stage.position + new Vector3(0f, .7f, 0f));
            _camera = cameraObject.AddComponent<Camera>();
            _camera.fieldOfView = 35f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = MenuColors.Background;
            _camera.nearClipPlane = .1f;
            _camera.farClipPlane = 100f;
            _camera.cullingMask = 1 << PreviewLayer;
            _camera.allowHDR = true;
            _renderTexture = new RenderTexture(900, 640, 16, RenderTextureFormat.ARGB32)
            { name = "PATHLESS Menu Car Preview", antiAliasing = 2 };
            _camera.targetTexture = _renderTexture;
            if (_preview != null) _preview.texture = _renderTexture;

            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "MenuCarStand";
            platform.transform.SetParent(_stage, false);
            platform.transform.localPosition = new Vector3(0f, -.04f, 0f);
            platform.transform.localScale = new Vector3(5.5f, .04f, 5.5f);
            SetLayer(platform, PreviewLayer);
            Destroy(platform.GetComponent<Collider>());
            ApplyMaterial(platform.GetComponent<Renderer>(), new Color(.012f, .022f, .04f), Color.black, .3f);

            GameObject ring = new GameObject("MenuCarStand Cyan Ring", typeof(MeshFilter), typeof(MeshRenderer));
            ring.transform.SetParent(_stage, false);
            ring.transform.localPosition = new Vector3(0f, .012f, 0f);
            ring.GetComponent<MeshFilter>().sharedMesh = CreateRingMesh(3.16f, 3.09f, 96);
            Material ringMaterial = NewMaterial(MenuColors.Cyan, MenuColors.Cyan * 1.25f, .3f);
            ring.GetComponent<MeshRenderer>().sharedMaterial = ringMaterial;
            SetLayer(ring, PreviewLayer);
            _ring = ring.transform;

            CreatePointLight("Menu Cyan Preview Light", new Vector3(-3f, 4f, 2f), MenuColors.Cyan, 3f, 12f);
            CreatePointLight("Menu Magenta Preview Light", new Vector3(3f, 3.5f, -1f), MenuColors.Magenta, 3f, 12f);
            GameObject under = new GameObject("Menu Stand Underglow");
            under.transform.SetParent(_stage, false);
            under.transform.localPosition = new Vector3(0f, -.2f, 0f);
            Light underLight = under.AddComponent<Light>();
            underLight.type = LightType.Point;
            underLight.color = MenuColors.Cyan;
            underLight.intensity = .45f;
            underLight.range = 4f;
            SetLayer(under, PreviewLayer);

            GameObject rain = new GameObject("Menu neon rain particles", typeof(ParticleSystem));
            rain.transform.SetParent(_stage, false);
            rain.transform.localPosition = new Vector3(0f, 4f, 0f);
            SetLayer(rain, PreviewLayer);
            ParticleSystem rainSystem = rain.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = rainSystem.main;
            main.loop = true;
            main.startLifetime = 3f;
            main.startSpeed = 7f;
            main.startSize = .045f;
            main.startColor = MenuColors.Cyan;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.useUnscaledTime = true;
            main.maxParticles = 120;
            ParticleSystem.EmissionModule emission = rainSystem.emission;
            emission.rateOverTime = 30f;
            ParticleSystem.ShapeModule shape = rainSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(12f, 1f, 10f);
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = rainSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient rainGradient = new Gradient();
            rainGradient.SetKeys(new[] { new GradientColorKey(MenuColors.Cyan, 0f), new GradientColorKey(MenuColors.Purple, 1f) },
                new[] { new GradientAlphaKey(.75f, 0f), new GradientAlphaKey(.05f, 1f) });
            colorOverLifetime.color = rainGradient;
            ParticleSystemRenderer rainRenderer = rain.GetComponent<ParticleSystemRenderer>();
            rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            rainRenderer.lengthScale = 3f;
            rainRenderer.velocityScale = .1f;
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            if (particleShader != null) rainRenderer.sharedMaterial = new Material(particleShader);
        }

        private void ShowSelectedModel()
        {
            DestroyPreviewMaterials();
            if (_model != null) Destroy(_model.gameObject);
            GameObject prefab = Selected != null ? Resources.Load<GameObject>(Selected.ModelResourcePath) : null;
            if (prefab == null) return;
            GameObject instance = Instantiate(prefab, _stage, false);
            instance.name = Selected.DisplayName + " Preview Copy";
            _model = instance.transform;
            _model.localPosition = Vector3.zero;
            _model.localRotation = Quaternion.identity;
            _model.localScale = Vector3.one;
            SetLayer(instance, PreviewLayer);
            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.detectCollisions = false; }
            foreach (AudioSource source in instance.GetComponentsInChildren<AudioSource>(true))
            { source.Stop(); source.mute = true; }
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            ApplyPreviewMaterials(renderers);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.x > bounds.size.z) _model.localRotation = Quaternion.Euler(0f, -90f, 0f);
            else if (bounds.size.y > bounds.size.z * 1.35f) _model.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _model.localRotation *= Quaternion.Euler(0f, Selected.ModelYawCorrection, 0f);
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float scale = Mathf.Min(3.9f / Mathf.Max(.1f, bounds.size.z), 2.35f / Mathf.Max(.1f, bounds.size.x), 1.9f / Mathf.Max(.1f, bounds.size.y));
            Vector3 center = bounds.center - _stage.position;
            float bottom = bounds.min.y - _stage.position.y;
            _model.localScale = Vector3.one * scale;
            _model.localPosition = new Vector3(-center.x * scale, .08f - bottom * scale, -center.z * scale);
            _model.localScale = Vector3.zero;
        }

        private void ApplyPreviewMaterials(Renderer[] renderers)
        {
            bool silverCoupe = Selected != null && Selected.Id == VehicleId.SilverCoupe;
            bool cyberpunkCar = Selected != null && Selected.Id == VehicleId.CyberpunkCar;
            Texture2D silverBody = silverCoupe ? Resources.Load<Texture2D>(ThemeText.SilverBodyTexturePath) : null;
            Texture2D silverBodyMetallic = silverCoupe ? Resources.Load<Texture2D>(ThemeText.SilverBodyMetallicPath) : null;
            Texture2D silverBodyNormal = silverCoupe ? Resources.Load<Texture2D>(ThemeText.SilverBodyNormalPath) : null;
            Texture2D silverTire = silverCoupe ? Resources.Load<Texture2D>(ThemeText.SilverTireTexturePath) : null;
            Texture2D silverTireNormal = silverCoupe ? Resources.Load<Texture2D>(ThemeText.SilverTireNormalPath) : null;
            Texture2D silverWheel = silverCoupe ? Resources.Load<Texture2D>(ThemeText.SilverWheelTexturePath) : null;
            Texture2D silverWheelNormal = silverCoupe ? Resources.Load<Texture2D>(ThemeText.SilverWheelNormalPath) : null;
            Texture2D cyberpunkAlbedo = cyberpunkCar ? Resources.Load<Texture2D>("VehicleModels/CyberpunkCar/textures/Vehicle_basecolor") : null;
            Texture2D cyberpunkMetallic = cyberpunkCar ? Resources.Load<Texture2D>("VehicleModels/CyberpunkCar/textures/Vehicle_metallic") : null;
            Texture2D cyberpunkNormal = cyberpunkCar ? Resources.Load<Texture2D>("VehicleModels/CyberpunkCar/textures/Vehicle_normal") : null;
            Texture2D cyberpunkOcclusion = cyberpunkCar ? Resources.Load<Texture2D>("VehicleModels/CyberpunkCar/textures/Vehicle_ao") : null;
            Texture2D cyberpunkEmission = cyberpunkCar ? Resources.Load<Texture2D>("VehicleModels/CyberpunkCar/textures/Vehicle_emissive") : null;
            Shader shader = Shader.Find(ThemeText.UrpLitShader) ?? Shader.Find(ThemeText.StandardShader);

            foreach (Renderer renderer in renderers)
            {
                Material[] sourceMaterials = renderer.sharedMaterials;
                Material[] previewMaterials = new Material[sourceMaterials.Length];
                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    Material source = sourceMaterials[i];
                    if (source == null || shader == null) continue;

                    bool hasUrpMaterial = source.shader != null
                        && source.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal);
                    Material previewMaterial = hasUrpMaterial
                        ? new Material(source)
                        : new Material(shader);
                    previewMaterial.name = source.name + " Menu Preview";
                    _previewMaterials.Add(previewMaterial);
                    if (!hasUrpMaterial) CopyPreviewMaterialProperties(source, previewMaterial);

                    if (silverCoupe)
                    {
                        string materialName = source.name;
                        bool tire = materialName.IndexOf(ThemeText.TireMaterialKey, StringComparison.OrdinalIgnoreCase) >= 0;
                        bool wheel = materialName.IndexOf(ThemeText.WheelMaterialKey, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (tire)
                        {
                            ClearPreviewTexture(previewMaterial, ThemeText.BaseMapProperty, ThemeText.MainTextureProperty);
                            SetPreviewTexture(previewMaterial, source, ThemeText.BaseMapProperty, silverTire, ThemeText.BaseMapProperty, ThemeText.MainTextureProperty);
                            SetPreviewTexture(previewMaterial, source, ThemeText.NormalMapProperty, silverTireNormal, ThemeText.NormalMapProperty);
                            SetPreviewColor(previewMaterial, new Color(.12f, .14f, .17f, 1f));
                        }
                        else if (wheel)
                        {
                            ClearPreviewTexture(previewMaterial, ThemeText.BaseMapProperty, ThemeText.MainTextureProperty);
                            SetPreviewTexture(previewMaterial, source, ThemeText.BaseMapProperty, silverWheel, ThemeText.BaseMapProperty, ThemeText.MainTextureProperty);
                            SetPreviewTexture(previewMaterial, source, ThemeText.NormalMapProperty, silverWheelNormal, ThemeText.NormalMapProperty);
                            SetPreviewColor(previewMaterial, new Color(.62f, .70f, .76f, 1f));
                        }
                        else
                        {
                            ClearPreviewTexture(previewMaterial, ThemeText.BaseMapProperty, ThemeText.MainTextureProperty);
                            SetPreviewTexture(previewMaterial, source, ThemeText.BaseMapProperty, silverBody, ThemeText.BaseMapProperty, ThemeText.MainTextureProperty);
                            SetPreviewTexture(previewMaterial, source, ThemeText.MetallicMapProperty, silverBodyMetallic, ThemeText.MetallicMapProperty);
                            SetPreviewTexture(previewMaterial, source, ThemeText.NormalMapProperty, silverBodyNormal, ThemeText.NormalMapProperty);
                            ClearPreviewTexture(previewMaterial, "_EmissionMap");
                            SetPreviewColor(previewMaterial, new Color(.58f, .68f, .76f, 1f));
                            SetPreviewFloat(previewMaterial, "_Metallic", .48f);
                            SetPreviewFloat(previewMaterial, "_Smoothness", .58f);
                            SetPreviewColorProperty(previewMaterial, "_EmissionColor", Color.black);
                            previewMaterial.DisableKeyword("_EMISSION");
                        }
                    }
                    else if (cyberpunkCar)
                    {
                        ClearPreviewTexture(previewMaterial, ThemeText.BaseMapProperty, ThemeText.MainTextureProperty);
                        SetPreviewTexture(previewMaterial, source, ThemeText.BaseMapProperty, cyberpunkAlbedo, ThemeText.BaseMapProperty, ThemeText.MainTextureProperty);
                        SetPreviewTexture(previewMaterial, source, ThemeText.MetallicMapProperty, cyberpunkMetallic, ThemeText.MetallicMapProperty);
                        SetPreviewTexture(previewMaterial, source, ThemeText.NormalMapProperty, cyberpunkNormal, ThemeText.NormalMapProperty);
                        SetPreviewTexture(previewMaterial, source, "_OcclusionMap", cyberpunkOcclusion, "_OcclusionMap");
                        SetPreviewTexture(previewMaterial, source, "_EmissionMap", cyberpunkEmission, "_EmissionMap");
                        SetPreviewColor(previewMaterial, Color.HSVToRGB(.70f, .72f, .72f));
                        SetPreviewColorProperty(previewMaterial, "_EmissionColor", new Color(.08f, .20f, .38f, 1f));
                        SetPreviewFloat(previewMaterial, "_Metallic", .24f);
                        SetPreviewFloat(previewMaterial, "_Smoothness", .42f);
                        if (cyberpunkEmission != null) previewMaterial.EnableKeyword("_EMISSION");
                    }
                    previewMaterials[i] = previewMaterial;
                }
                renderer.sharedMaterials = previewMaterials;
            }
        }

        private static void CopyPreviewMaterialProperties(Material source, Material destination)
        {
            CopyPreviewColor(source, destination, "_BaseColor", "_Color");
            CopyPreviewTexture(source, destination, "_BaseMap", "_MainTex");
            CopyPreviewTexture(source, destination, "_MetallicGlossMap");
            CopyPreviewTexture(source, destination, "_BumpMap");
            CopyPreviewTexture(source, destination, "_OcclusionMap");
            CopyPreviewTexture(source, destination, "_EmissionMap");
            CopyPreviewFloat(source, destination, "_Metallic");
            CopyPreviewFloat(source, destination, "_Smoothness", "_Glossiness");
            CopyPreviewColor(source, destination, "_EmissionColor");
            if (destination.HasProperty("_MetallicGlossMap") && destination.GetTexture("_MetallicGlossMap") != null)
                destination.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (destination.HasProperty("_BumpMap") && destination.GetTexture("_BumpMap") != null)
                destination.EnableKeyword("_NORMALMAP");
            if (destination.HasProperty("_EmissionMap") && destination.GetTexture("_EmissionMap") != null)
                destination.EnableKeyword("_EMISSION");
        }

        private static void CopyPreviewColor(Material source, Material destination, string property, string fallback = null)
        {
            string sourceProperty = source.HasProperty(property) ? property : fallback;
            if (!string.IsNullOrEmpty(sourceProperty) && source.HasProperty(sourceProperty) && destination.HasProperty(property))
                destination.SetColor(property, source.GetColor(sourceProperty));
        }

        private static void CopyPreviewFloat(Material source, Material destination, string property, string fallback = null)
        {
            string sourceProperty = source.HasProperty(property) ? property : fallback;
            if (!string.IsNullOrEmpty(sourceProperty) && source.HasProperty(sourceProperty) && destination.HasProperty(property))
                destination.SetFloat(property, source.GetFloat(sourceProperty));
        }

        private static void CopyPreviewTexture(Material source, Material destination, string property, string fallback = null)
        {
            string sourceProperty = source.HasProperty(property) ? property : fallback;
            if (string.IsNullOrEmpty(sourceProperty) || !source.HasProperty(sourceProperty) || !destination.HasProperty(property)) return;
            Texture texture = source.GetTexture(sourceProperty);
            if (texture == null) return;
            destination.SetTexture(property, texture);
            destination.SetTextureScale(property, source.GetTextureScale(sourceProperty));
            destination.SetTextureOffset(property, source.GetTextureOffset(sourceProperty));
        }

        private static void SetPreviewTexture(Material material, Material source, string destinationProperty,
            Texture texture, params string[] sourceProperties)
        {
            if (texture == null || !material.HasProperty(destinationProperty)) return;
            material.SetTexture(destinationProperty, texture);
            for (int i = 0; i < sourceProperties.Length; i++)
            {
                string sourceProperty = sourceProperties[i];
                if (source.HasProperty(sourceProperty))
                {
                    material.SetTextureScale(destinationProperty, source.GetTextureScale(sourceProperty));
                    material.SetTextureOffset(destinationProperty, source.GetTextureOffset(sourceProperty));
                    break;
                }
            }
            if (destinationProperty == ThemeText.NormalMapProperty) material.EnableKeyword("_NORMALMAP");
            if (destinationProperty == ThemeText.MetallicMapProperty) material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        private static void SetPreviewColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static void SetPreviewColorProperty(Material material, string property, Color color)
        {
            if (material.HasProperty(property)) material.SetColor(property, color);
        }

        private static void ClearPreviewTexture(Material material, params string[] properties)
        {
            for (int i = 0; i < properties.Length; i++)
                if (material.HasProperty(properties[i])) material.SetTexture(properties[i], null);
        }

        private static void SetPreviewFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private void DestroyPreviewMaterials()
        {
            for (int i = 0; i < _previewMaterials.Count; i++)
                if (_previewMaterials[i] != null) Destroy(_previewMaterials[i]);
            _previewMaterials.Clear();
        }

        private void CreatePointLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(_stage, false);
            obj.transform.localPosition = position;
            Light light = obj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            SetLayer(obj, PreviewLayer);
        }

        private static void ApplyMaterial(Renderer renderer, Color color, Color emission, float metallic)
        {
            renderer.sharedMaterial = NewMaterial(color, emission, metallic);
        }

        private static Material NewMaterial(Color color, Color emission, float metallic)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            material.EnableKeyword("_EMISSION");
            SetEmission(material, emission);
            return material;
        }

        private static void SetEmission(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color);
            else if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        }

        private static void SetLayer(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayer(child.gameObject, layer);
        }

        private static Mesh CreateRingMesh(float outer, float inner, int segments)
        {
            Vector3[] vertices = new Vector3[segments * 2];
            int[] triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * outer;
                vertices[i * 2 + 1] = direction * inner;
                int next = (i + 1) % segments;
                int t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = next * 2; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2 + 1; triangles[t + 4] = next * 2; triangles[t + 5] = next * 2 + 1;
            }
            Mesh mesh = new Mesh { name = "Menu Stand Neon Ring" };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals();
            return mesh;
        }

        private void OnDestroy()
        {
            DestroyPreviewMaterials();
            if (_camera != null) _camera.targetTexture = null;
            if (_renderTexture != null) { _renderTexture.Release(); Destroy(_renderTexture); }
            if (_stage != null) Destroy(_stage.gameObject);
        }
    }
}
