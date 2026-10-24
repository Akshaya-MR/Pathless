using UnityEngine;

namespace TheRacer
{
    /// <summary>Preallocated, distance-driven pickup pools for the rolling test road.</summary>
    public sealed class TrackSpawner : MonoBehaviour
    {
        [SerializeField, Min(20f)] private float spawnDistanceAhead = 260f;
        [SerializeField, Range(0f, 1f)] private float obstacleChance = 0.65f;
        [SerializeField, Range(0f, 1f), Tooltip("Boost tiles spawn at this fraction of the obstacle rate.")] private float boosterChance = 0.26f;
        [SerializeField, Min(2f)] private float minSpacing = 14f;
        [SerializeField] private int seed = 2087;
        [SerializeField, Min(0f)] private float spawnSafeDistance = 35f;

        private sealed class Item
        {
            public GameObject Root;
            public bool Booster;
            public float Distance;
            public TrackPickup Pickup;
        }

        private Item[] _items;
        private System.Random _random;
        private RollingTrack _track;
        private CarController _car;
        private Material _obstacleMaterial;
        private Material _obstacleStripeMaterial;
        private Texture2D _boosterTexture;
        private Sprite _boosterSprite;
        private Material _boosterFallbackMaterial;
        private Mesh _boosterFallbackMesh;
        private float _nextSpawnDistance;
        private float _lastItemDistance = -100000f;
        public int ObstaclesDodged { get; private set; }
        private float SafeSpacing => Mathf.Max(2f, minSpacing);

        public void Configure(Transform parent, RollingTrack track, CarController car, DriftBoostSystem drift,
            GameBootstrap owner, int randomSeed, Texture2D customBoosterTexture = null)
        {
            _track = track; _car = car; seed = randomSeed; _random = new System.Random(seed);
            _obstacleMaterial = owner.CreateMaterial(ThemePalette.Void, 0.1f, 0.72f,
                ThemePalette.NoEmission, ThemeText.UrpLitShader);
            _obstacleStripeMaterial = owner.CreateMaterial(ThemePalette.AlertRed, 0f, 0.45f,
                ThemePalette.Neon(ThemePalette.AlertRed, 2.2f), ThemeText.UrpLitShader);
            _boosterTexture = customBoosterTexture != null
                ? customBoosterTexture : Resources.Load<Texture2D>("BoostTile/BoostArrows");
            if (_boosterTexture == null)
                Debug.LogWarning("Boost arrow texture not found. Using the visible cyan fallback pad.", this);
            else
                _boosterSprite = Sprite.Create(_boosterTexture,
                    new Rect(0f, 0f, _boosterTexture.width, _boosterTexture.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _items = new Item[30];
            for (int i = 0; i < _items.Length; i++)
            {
                bool booster = i >= 20;
                _items[i] = CreateItem(parent, booster);
            }
            _nextSpawnDistance = Mathf.Max(spawnSafeDistance, _track.GetDistanceAtPosition(_car.transform.position) + SafeSpacing);
            FxEvents.OnPlayerReset += ResetItems;
        }

        private Item CreateItem(Transform parent, bool booster)
        {
            GameObject root = booster ? new GameObject() : GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = booster ? ThemeText.BoosterRampName : ThemeText.ObstacleWallName;
            root.transform.SetParent(parent, false);
            if (booster)
            {
                BoxCollider trigger = root.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(0f, 0.08f, 0f);
                trigger.size = new Vector3(4.8f, 0.35f, 4.2f);

                GameObject graphic = new GameObject("Editable Neon Arrow Texture");
                graphic.transform.SetParent(root.transform, false);
                graphic.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.right);
                graphic.transform.localPosition = new Vector3(0f, 0.045f, 0f);
                SpriteRenderer spriteRenderer = graphic.AddComponent<SpriteRenderer>();
                if (_boosterSprite != null)
                {
                    spriteRenderer.sprite = _boosterSprite;
                    Vector2 size = _boosterSprite.bounds.size;
                    graphic.transform.localScale = new Vector3(4.8f / Mathf.Max(0.01f, size.x),
                        3.2f / Mathf.Max(0.01f, size.y), 1f);
                    spriteRenderer.color = Color.white;
                }
                else
                {
                    // Keep a visible pickup even if a replacement texture is missing.
                    MeshFilter filter = graphic.AddComponent<MeshFilter>();
                    MeshRenderer renderer = graphic.AddComponent<MeshRenderer>();
                    if (_boosterFallbackMesh == null) _boosterFallbackMesh = CreateFlatQuad();
                    if (_boosterFallbackMaterial == null)
                    {
                        Shader shader = Shader.Find(ThemeText.UrpUnlitShader);
                        if (shader == null) shader = Shader.Find("Unlit/Color");
                        if (shader != null)
                            _boosterFallbackMaterial = new Material(shader) { color = ThemePalette.NeonCyan };
                    }
                    filter.sharedMesh = _boosterFallbackMesh;
                    if (_boosterFallbackMaterial != null) renderer.sharedMaterial = _boosterFallbackMaterial;
                    graphic.transform.localScale = new Vector3(4.8f, 3.2f, 1f);
                }
            }
            else
            {
                root.transform.localScale = new Vector3(4.8f, 1.55f, 0.7f);
                Collider collider = root.GetComponent<Collider>();
                collider.isTrigger = true;
                root.GetComponent<Renderer>().sharedMaterial = _obstacleMaterial;
                CreateObstacleNeonBand(root.transform, 0.48f, 0.75f);
                CreateObstacleNeonBand(root.transform, -0.48f, 0.75f);
                CreateObstacleNeonBand(root.transform, 0f, 0.76f, true);
            }
            Rigidbody body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            TrackPickup pickup = root.AddComponent<TrackPickup>(); pickup.Configure(this, booster, root);
            _track.RegisterWorldShiftTransform(root.transform);
            root.SetActive(false);
            return new Item { Root = root, Booster = booster, Pickup = pickup };
        }

        private void CreateObstacleNeonBand(Transform parent, float y, float zDepth, bool vertical = false)
        {
            GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = vertical ? "Neon Blocker Center Stripe" : "Neon Blocker Edge Stripe";
            band.transform.SetParent(parent, false);
            band.transform.localPosition = new Vector3(0f, y * 0.64f, zDepth * 0.68f);
            // The parent obstacle is scaled to road-blocker proportions, so these child
            // dimensions are normalized to its unit-cube mesh.
            band.transform.localScale = vertical ? new Vector3(0.035f, 0.8f, 0.04f)
                : new Vector3(0.95f, 0.07f, 0.04f);
            Collider bandCollider = band.GetComponent<Collider>();
            if (bandCollider != null) Destroy(bandCollider);
            band.GetComponent<Renderer>().sharedMaterial = _obstacleStripeMaterial;
        }

        private static Mesh CreateFlatQuad()
        {
            Mesh mesh = new Mesh { name = "Boost Pad Fallback Quad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private void Update()
        {
            if (_track == null || _car == null || _items == null) return;
            float playerDistance = Mathf.Max(_track.GetDistanceAtPosition(_car.transform.position), _track.DistanceTravelled);
            float endDistance = playerDistance + Mathf.Max(20f, spawnDistanceAhead);
            for (int i = 0; i < _items.Length; i++)
            {
                Item item = _items[i];
                if (item.Root.activeSelf && item.Distance < playerDistance - 8f)
                {
                    if (!item.Booster && !item.Pickup.WasHit)
                        ObstaclesDodged++;
                    item.Root.SetActive(false);
                }
            }
            while (_nextSpawnDistance <= endDistance)
            {
                SpawnOne(_nextSpawnDistance);
                _nextSpawnDistance += SafeSpacing + (float)_random.NextDouble() * SafeSpacing;
            }
        }

        private void SpawnOne(float distance)
        {
            if (distance < spawnSafeDistance || _track == null) return;
            double roll = _random.NextDouble();
            bool booster = roll < boosterChance;
            if (!booster && roll >= boosterChance + obstacleChance) return;
            Item item = FindAvailable(booster, distance);
            if (item == null) return;
            float halfWidth = _track.RoadWidth * 0.5f;
            // Keep boost pads in the middle lanes so they're easy to spot and reach,
            // while obstacles may still use the full usable road width.
            float lateralLimit = booster ? Mathf.Min(6f, Mathf.Max(0.5f, halfWidth - 2f))
                : Mathf.Max(0.5f, halfWidth - 3.1f);
            float lateral = Mathf.Lerp(-lateralLimit, lateralLimit, (float)_random.NextDouble());
            Vector3 point = _track.GetRoadPoint(distance, lateral);
            point.y = booster ? 0.06f : 0.675f;
            item.Distance = distance;
            item.Root.transform.SetPositionAndRotation(point, Quaternion.identity);
            item.Root.SetActive(true);
            _lastItemDistance = distance;
        }

        private Item FindAvailable(bool booster, float distance)
        {
            if (distance - _lastItemDistance < SafeSpacing) return null;
            for (int i = 0; i < _items.Length; i++)
                if (_items[i].Booster == booster && !_items[i].Root.activeSelf) return _items[i];
            return null;
        }

        internal void Hit(CarController car, bool booster)
        {
            if (car == null) return;
            if (booster)
            {
                car.ApplyPickupBoost(5f, 1.3f);
                DriftBoostSystem carBoost = car.GetComponent<DriftBoostSystem>();
                if (carBoost != null) carBoost.AddCore(12f);
                if (car.IsPlayerControlled) Toast.Show(ThemeText.ToastBooster, ThemePalette.NeonCyan);
            }
            else
            {
                RaceAudioController.NotifyObstacleHit(car);
                car.ApplyObstacleImpact();
                if (car.IsPlayerControlled) Toast.Show(ThemeText.ToastImpact, ThemePalette.AlertRed);
            }
        }

        private void ResetItems()
        {
            if (_items == null || _track == null) return;
            for (int i = 0; i < _items.Length; i++) _items[i].Root.SetActive(false);
            ObstaclesDodged = 0;
            _nextSpawnDistance = Mathf.Max(spawnSafeDistance, _track.GetDistanceAtPosition(_car.transform.position) + SafeSpacing);
            _lastItemDistance = -100000f;
        }

        private void OnDestroy()
        {
            FxEvents.OnPlayerReset -= ResetItems;
            if (_boosterSprite != null) Destroy(_boosterSprite);
            if (_boosterFallbackMaterial != null) Destroy(_boosterFallbackMaterial);
            if (_boosterFallbackMesh != null) Destroy(_boosterFallbackMesh);
        }
    }

    /// <summary>Trigger bridge used by pooled track items.</summary>
    public sealed class TrackPickup : MonoBehaviour
    {
        private TrackSpawner _owner;
        private GameObject _root;
        private bool _booster;
        private bool _used;
        public bool WasHit => _used;
        public bool IsBooster => _booster;
        public void Configure(TrackSpawner owner, bool booster, GameObject root) { _owner = owner; _booster = booster; _root = root; }
        private void OnEnable() { _used = false; }
        private void OnTriggerEnter(Collider other)
        {
            CarController car = other.GetComponentInParent<CarController>();
            if (car == null || _used) return;
            _used = true;
            _owner.Hit(car, _booster);
            // A boost tile is consumed by the first car that touches it.
            if (_root != null) _root.SetActive(false);
        }

        private void OnTriggerExit(Collider other)
        {
            // Pickups deactivate on first trigger entry, keeping each pad single-use.
        }
    }
}
