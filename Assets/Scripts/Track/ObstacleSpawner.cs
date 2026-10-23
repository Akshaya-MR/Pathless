using System.Collections.Generic;
using UnityEngine;

namespace TheRacer
{
    /// <summary>Pools lateral obstacles for generated segments and obeys test mode.</summary>
    public sealed class ObstacleSpawner : MonoBehaviour
    {
        [SerializeField, Tooltip("Obstacle variants to pool and place.")] private GameObject[] obstaclePrefabs;
        [SerializeField, Min(1f), Tooltip("Distance between obstacle slots in metres.")] private float obstacleInterval = 10f;
        [SerializeField, Range(0.1f, 0.45f), Tooltip("Sideways offset as a fraction of the segment's half-width (TrackSegment.HalfWidth).")] private float lateralOffsetFraction = 0.3f;
        [SerializeField, Tooltip("Seed for repeatable obstacle placement.")] private int seed = 2087;

        private readonly List<GameObject> _validPrefabs = new List<GameObject>();
        private readonly Queue<GameObject> _pool = new Queue<GameObject>();
        private readonly Dictionary<TrackSegment, List<GameObject>> _bySegment = new Dictionary<TrackSegment, List<GameObject>>();
        private System.Random _placeRandom;   // side selection only
        private System.Random _variantRandom; // prefab choice only, so pool creation never changes placement

        private void Awake()
        {
            _placeRandom = new System.Random(seed);
            _variantRandom = new System.Random(seed + 1);
            if (obstaclePrefabs != null)
            {
                for (int i = 0; i < obstaclePrefabs.Length; i++)
                    if (obstaclePrefabs[i] != null) _validPrefabs.Add(obstaclePrefabs[i]);
            }
            if (_validPrefabs.Count == 0)
            {
                Debug.LogWarning("ObstacleSpawner: no valid obstacle prefabs assigned, no obstacles will spawn.", this);
                return;
            }
            for (int i = 0; i < _validPrefabs.Count * 8; i++) _pool.Enqueue(CreatePooled());
        }

        private void OnEnable()
        {
            TrackGenerator.OnSegmentSpawned += SpawnFor;
            TrackGenerator.OnSegmentDespawned += ReleaseFor;
            TestModeController.Changed += OnTestModeChanged;
        }

        private void OnDisable()
        {
            TrackGenerator.OnSegmentSpawned -= SpawnFor;
            TrackGenerator.OnSegmentDespawned -= ReleaseFor;
            TestModeController.Changed -= OnTestModeChanged;
        }

        private void Start()
        {
            // Safe to overlap with TrackGenerator's own Start event: SpawnFor ignores segments that already have obstacles.
            SpawnForExistingSegments();
        }

        private void SpawnForExistingSegments()
        {
            foreach (TrackSegment segment in Object.FindObjectsByType<TrackSegment>())
                SpawnFor(segment);
        }

        private void SpawnFor(TrackSegment segment)
        {
            if (_validPrefabs.Count == 0 || TestModeController.IsActive) return;
            if (segment == null || segment.IsRamp || segment.StartAnchor == null || segment.EndAnchor == null) return;
            if (_bySegment.ContainsKey(segment)) return;

            Vector3 start = segment.StartAnchor.position;
            Vector3 end = segment.EndAnchor.position;
            float length = Vector3.Distance(start, end);
            if (length < 0.01f) return;

            Vector3 direction = (end - start) / length;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            float sideOffset = lateralOffsetFraction * segment.HalfWidth;

            List<GameObject> placed = null;
            for (float distance = obstacleInterval; distance < length; distance += obstacleInterval)
            {
                // Skips the landing zone after a ramp (distance carried over by TrackGenerator).
                if (distance < segment.NoObstacleDistance) continue;

                float side = _placeRandom.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 point = start + direction * distance + right * side * sideOffset;

                GameObject obstacle = Take();
                obstacle.transform.SetPositionAndRotation(point, rotation);
                obstacle.transform.SetParent(segment.transform, true);
                obstacle.SetActive(true);

                if (placed == null) placed = new List<GameObject>();
                placed.Add(obstacle);
            }
            if (placed != null) _bySegment.Add(segment, placed);
        }

        private void ReleaseFor(TrackSegment segment)
        {
            if (segment == null || !_bySegment.TryGetValue(segment, out List<GameObject> list)) return;
            ReleaseList(list);
            _bySegment.Remove(segment);
        }

        private void ReleaseList(List<GameObject> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                GameObject obstacle = list[i];
                if (obstacle == null) continue;
                obstacle.SetActive(false);
                obstacle.transform.SetParent(transform, false);
                _pool.Enqueue(obstacle);
            }
            list.Clear();
        }

        private void OnTestModeChanged(bool active)
        {
            if (active)
            {
                foreach (List<GameObject> list in _bySegment.Values) ReleaseList(list);
                _bySegment.Clear();
            }
            else
            {
                SpawnForExistingSegments();
            }
        }

        private GameObject Take()
        {
            while (_pool.Count > 0)
            {
                GameObject item = _pool.Dequeue();
                if (item != null) return item;
            }
            return CreatePooled();
        }

        private GameObject CreatePooled()
        {
            GameObject prefab = _validPrefabs[_variantRandom.Next(_validPrefabs.Count)];
            GameObject item = Instantiate(prefab, transform);
            item.SetActive(false);
            return item;
        }
    }
}
