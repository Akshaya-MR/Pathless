using UnityEngine;

namespace TheRacer
{
    /// <summary>Preallocated, distance-driven pickup pools for the rolling test road.</summary>
    public sealed class TrackSpawner : MonoBehaviour
    {
        [SerializeField, Min(20f)] private float spawnDistanceAhead = 180f;
        [SerializeField, Range(0f, 1f)] private float obstacleChance = 0.65f;
        [SerializeField, Range(0f, 1f)] private float boosterChance = 0.28f;
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
        private Material _boosterMaterial;
        private float _nextSpawnDistance;
        private float _lastItemDistance = -100000f;
        public int ObstaclesDodged { get; private set; }
        private float SafeSpacing => Mathf.Max(2f, minSpacing);

        public void Configure(Transform parent, RollingTrack track, CarController car, DriftBoostSystem drift,
            GameBootstrap owner, int randomSeed)
        {
            _track = track; _car = car; seed = randomSeed; _random = new System.Random(seed);
            _obstacleMaterial = owner.CreateMaterial(ThemePalette.AlertRed, 0.05f, 0.5f,
                ThemePalette.Neon(ThemePalette.AlertRed, 1.4f), ThemeText.UrpLitShader);
            _boosterMaterial = owner.CreateMaterial(ThemePalette.NeonCyan, 0f, 0.5f,
                ThemePalette.Neon(ThemePalette.NeonCyan, 2.5f), ThemeText.UrpLitShader);
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
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = booster ? ThemeText.BoosterRampName : ThemeText.ObstacleWallName;
            root.transform.SetParent(parent, false);
            root.GetComponent<Renderer>().sharedMaterial = booster ? _boosterMaterial : _obstacleMaterial;
            Collider collider = root.GetComponent<Collider>();
            collider.isTrigger = true;
            if (booster)
            {
                root.transform.localScale = new Vector3(2.8f, 0.08f, 3.8f);
                root.transform.localRotation = Quaternion.identity;
                root.GetComponent<Renderer>().sharedMaterial = _boosterMaterial;
            }
            else root.transform.localScale = new Vector3(4.4f, 1.35f, 0.55f);
            Rigidbody body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            TrackPickup pickup = root.AddComponent<TrackPickup>(); pickup.Configure(this, booster, root);
            _track.RegisterWorldShiftTransform(root.transform);
            root.SetActive(false);
            return new Item { Root = root, Booster = booster, Pickup = pickup };
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
            float lateralLimit = Mathf.Max(0.5f, halfWidth - (booster ? 2f : 3.1f));
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
                car.ApplyPickupBoost(2f);
                DriftBoostSystem carBoost = car.GetComponent<DriftBoostSystem>();
                if (carBoost != null) carBoost.AddCore(12f);
                if (car.IsPlayerControlled) Toast.Show(ThemeText.ToastBooster, ThemePalette.NeonCyan);
            }
            else
            {
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

        private void OnDestroy() { FxEvents.OnPlayerReset -= ResetItems; }
    }

    /// <summary>Trigger bridge used by pooled track items.</summary>
    public sealed class TrackPickup : MonoBehaviour
    {
        private TrackSpawner _owner;
        private GameObject _root;
        private bool _booster;
        private bool _used;
        private readonly CarController[] _boostedCars = new CarController[6];
        private int _boostedCarCount;
        public bool WasHit => _used;
        public void Configure(TrackSpawner owner, bool booster, GameObject root) { _owner = owner; _booster = booster; _root = root; }
        private void OnEnable() { _used = false; _boostedCarCount = 0; }
        private void OnTriggerEnter(Collider other)
        {
            CarController car = other.GetComponentInParent<CarController>();
            if (car == null) return;
            if (_booster)
            {
                for (int i = 0; i < _boostedCarCount; i++)
                    if (_boostedCars[i] == car) return;
                if (_boostedCarCount >= _boostedCars.Length) return;
                _boostedCars[_boostedCarCount++] = car;
            }
            else if (_used) return;
            _used = true;
            _owner.Hit(car, _booster);
            if (!_booster) _root.SetActive(false);
        }

        private void OnTriggerExit(Collider other)
        {
            // Pickups deactivate on first trigger entry, keeping each pad single-use.
        }
    }
}
