using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheRacer
{
    /// <summary>Maintains an endless authored-segment window without moving the car.</summary>
    public sealed class TrackGenerator : MonoBehaviour
    {
        [SerializeField, Tooltip("Segment prefab variants with TrackSegment anchors.")] private TrackSegment[] segmentPrefabs;
        [SerializeField, Tooltip("Car used for transition tests.")] private Transform car;
        [SerializeField, Min(1), Tooltip("Number of segments kept in front of the car.")] private int segmentsAhead = 5;
        [SerializeField, Min(0), Tooltip("Number of completed segments kept behind the car.")] private int segmentsBehind = 1;
        [SerializeField, Tooltip("Seed for repeatable segment selection.")] private int seed = 2087;
        [SerializeField, Min(10f), Tooltip("Horizontal world distance before floating-origin correction.")] private float floatingOriginThreshold = 5000f;
        [SerializeField, Tooltip("World Y where the first segment's start anchor is placed.")] private float firstSegmentGroundY = 0f;

        public static event Action<TrackSegment> OnSegmentSpawned;
        public static event Action<TrackSegment> OnSegmentDespawned;
        /// <summary>Raised after the origin shift with the shift that was subtracted. Other systems (gates, trails, particles) can subscribe.</summary>
        public static event Action<Vector3> OriginShifted;

        private readonly LinkedList<TrackSegment> _active = new LinkedList<TrackSegment>();
        private readonly Dictionary<TrackSegment, Queue<TrackSegment>> _pools = new Dictionary<TrackSegment, Queue<TrackSegment>>();
        private readonly Dictionary<TrackSegment, TrackSegment> _sourceOf = new Dictionary<TrackSegment, TrackSegment>();
        private readonly List<TrackSegment> _allPrefabs = new List<TrackSegment>();
        private readonly List<TrackSegment> _nonRampPrefabs = new List<TrackSegment>();
        private System.Random _random;
        private Rigidbody _carBody;
        private LinkedListNode<TrackSegment> _currentNode;
        private bool _lastRamp;
        private bool _ready;

        private int WindowSize => segmentsBehind + 1 + segmentsAhead;

        private void Awake()
        {
            if (car == null)
            {
                Debug.LogError("TrackGenerator: car is not assigned.", this);
                return;
            }
            if (segmentPrefabs == null || segmentPrefabs.Length == 0)
            {
                Debug.LogError("TrackGenerator: no segment prefabs assigned.", this);
                return;
            }
            for (int i = 0; i < segmentPrefabs.Length; i++)
            {
                TrackSegment prefab = segmentPrefabs[i];
                if (prefab == null || prefab.StartAnchor == null || prefab.EndAnchor == null)
                {
                    Debug.LogError("TrackGenerator: segment prefab at index " + i + " is missing or has no Start/End anchor.", this);
                    return;
                }
                _allPrefabs.Add(prefab);
                if (!prefab.IsRamp) _nonRampPrefabs.Add(prefab);
            }

            _random = new System.Random(seed);
            _carBody = car.GetComponent<Rigidbody>();

            TrackSegment first = AcquireSegment();
            Quaternion yaw = Quaternion.Euler(0f, car.eulerAngles.y, 0f);
            AlignStartTo(first, new Vector3(car.position.x, firstSegmentGroundY, car.position.z), yaw);
            first.SetNoObstacleDistance(0f);
            first.gameObject.SetActive(true);
            _active.AddLast(first);
            while (_active.Count < WindowSize) SpawnNext(false);
            _currentNode = _active.First;
            _ready = true;
        }

        private void Start()
        {
            // Events are raised here (not in Awake) so every subscriber has run OnEnable.
            foreach (TrackSegment segment in _active) OnSegmentSpawned?.Invoke(segment);
        }

        private void Update()
        {
            if (!_ready || car == null) return;
            int guard = 0;
            while (_currentNode != null && _currentNode.Value.EndAnchor != null
                && PassedEnd(_currentNode.Value) && guard++ < 32)
            {
                LinkedListNode<TrackSegment> next = _currentNode.Next;
                if (next == null) break;
                _currentNode = next;
                SpawnNext(true);
                while (_active.Count > WindowSize)
                {
                    if (!DespawnOldest()) break;
                }
            }

            Vector3 p = car.position;
            if (p.x * p.x + p.z * p.z > floatingOriginThreshold * floatingOriginThreshold) ShiftOrigin();
        }

        private bool PassedEnd(TrackSegment segment)
        {
            Vector3 offset = car.position - segment.EndAnchor.position;
            return Vector3.Dot(offset, segment.EndAnchor.forward) > 0f;
        }

        /// <summary>Rotates and moves a segment so its StartAnchor matches the target pose. Works for any anchor hierarchy.</summary>
        private static void AlignStartTo(TrackSegment segment, Vector3 position, Quaternion rotation)
        {
            Transform root = segment.transform;
            Quaternion delta = rotation * Quaternion.Inverse(segment.StartAnchor.rotation);
            root.rotation = delta * root.rotation;
            root.position += position - segment.StartAnchor.position;
        }

        private void SpawnNext(bool notify = true)
        {
            TrackSegment previous = _active.Last.Value;
            TrackSegment next = AcquireSegment();
            AlignStartTo(next, previous.EndAnchor.position, previous.EndAnchor.rotation);

            float carry = previous.IsRamp
                ? previous.LandingNoSpawnDistance
                : Mathf.Max(0f, previous.NoObstacleDistance - previous.Length);
            next.SetNoObstacleDistance(carry);

            next.gameObject.SetActive(true);
            _active.AddLast(next);
            if (notify) OnSegmentSpawned?.Invoke(next);
        }

        private bool DespawnOldest()
        {
            if (_active.Count <= segmentsBehind + 1) return false;
            TrackSegment segment = _active.First.Value;
            if (_currentNode != null && _currentNode.Value == segment) return false;
            OnSegmentDespawned?.Invoke(segment);
            _active.RemoveFirst();
            segment.gameObject.SetActive(false);
            _pools[_sourceOf[segment]].Enqueue(segment);
            return true;
        }

        /// <summary>Picks a prefab at random now (never ramp after ramp) and returns an inactive instance of it.</summary>
        private TrackSegment AcquireSegment()
        {
            List<TrackSegment> source = _lastRamp && _nonRampPrefabs.Count > 0 ? _nonRampPrefabs : _allPrefabs;
            TrackSegment prefab = source[_random.Next(source.Count)];

            if (!_pools.TryGetValue(prefab, out Queue<TrackSegment> pool))
            {
                pool = new Queue<TrackSegment>();
                _pools.Add(prefab, pool);
            }

            TrackSegment segment;
            if (pool.Count > 0)
            {
                segment = pool.Dequeue();
            }
            else
            {
                segment = Instantiate(prefab, transform);
                segment.gameObject.SetActive(false);
                _sourceOf.Add(segment, prefab);
            }
            _lastRamp = prefab.IsRamp;
            return segment;
        }

        private void ShiftOrigin()
        {
            Vector3 p = car.position;
            Vector3 shift = new Vector3(p.x, 0f, p.z);
            transform.position -= shift;
            if (_carBody != null) _carBody.position -= shift;
            else car.position -= shift;

            Camera cam = Camera.main;
            if (cam != null) cam.transform.position -= shift;

            Physics.SyncTransforms();
            OriginShifted?.Invoke(shift);
        }

        [ContextMenu("Spawn 100 segments")]
        private void Spawn100Segments()
        {
            if (!_ready || _active.Count == 0) return;
            for (int i = 0; i < 100; i++) SpawnNext(true);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = ThemePalette.ToxicGreen;
            foreach (TrackSegment segment in _active)
                if (segment != null) Gizmos.DrawWireCube(segment.transform.position, Vector3.one * 0.5f);
        }
    }
}
