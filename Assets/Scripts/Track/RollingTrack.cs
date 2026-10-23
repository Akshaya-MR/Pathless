using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheRacer
{
    /// <summary>Recycles a simple, continuous straight test road ahead of the car.</summary>
    public sealed class RollingTrack : MonoBehaviour
    {
        private sealed class Tile
        {
            public Transform root;
        }

        private sealed class RoadArea
        {
            public Transform root;
            public int index;
        }

        private readonly List<Tile> _tiles = new List<Tile>();
        private readonly RoadArea[] _areas = new RoadArea[3];
        private readonly List<CarController> _worldShiftCars = new List<CarController>(4);
        private readonly List<Transform> _worldShiftTransforms = new List<Transform>(32);
        private Transform _car;
        private TestRoadConfig _config;
        private GameBootstrap _owner;
        private Material _roadMaterial;
        private Material _gridMaterial;
        private Material _barrierMaterial;
        private Mesh _roadMesh;
        private Mesh _gridMesh;
        private float _startCarZ;
        private int _tilesPerArea;
        private float _areaLength;
        private float _lastSafeDistance = -1f;

        /// <summary>Metres driven since the car was configured (starts at 0).</summary>
        public float DistanceTravelled { get; private set; }
        /// <summary>Raised after the local world origin shifts to keep the player near zero.</summary>
        public event Action<Vector3> WorldShifted;
        /// <summary>World Z where tile index 0 begins.</summary>
        public float RoadStartZ { get; private set; }
        public float RoadWidth => _config != null ? _config.width : 34f;

        /// <summary>Returns a world position on the currently built straight rolling road.</summary>
        public Vector3 GetRoadPoint(float distance, float lateral)
        {
            return new Vector3(lateral, 0f, _startCarZ + distance);
        }

        /// <summary>Offsets a road point laterally, suitable for predictable AI hazard avoidance lanes.</summary>
        public Vector3 GetRoadPoint(float distance, float lateral, float longitudinalOffset)
        {
            return GetRoadPoint(distance + longitudinalOffset, lateral);
        }

        /// <summary>Returns the forward tangent of the active road path.</summary>
        public Vector3 GetRoadForward(float distance) { return Vector3.forward; }

        /// <summary>Maps a road world position to the current run's non-negative distance.</summary>
        public float GetDistanceAtPosition(Vector3 position)
        {
            return Mathf.Max(0f, position.z - _startCarZ);
        }

        public void Configure(GameBootstrap owner, Transform parent, TestRoadConfig config, TrackData data,
            Material road, Material grid, Material obstacle, Material cone, Material[] zones, Transform car)
        {
            _owner = owner;
            _car = car;
            _config = config;
            _roadMaterial = road;
            _gridMaterial = grid;
            _barrierMaterial = obstacle;
            _tilesPerArea = Mathf.Max(1, Mathf.CeilToInt(config.length / 3f / config.tileLength));
            _areaLength = _tilesPerArea * config.tileLength;
            _startCarZ = car.position.z;
            RoadStartZ = car.position.z - config.tileLength * 2f;
            CarController playerCar = car.GetComponent<CarController>();
            if (playerCar != null) _worldShiftCars.Add(playerCar);
            _lastSafeDistance = 0f;
            _roadMesh = CreateRoadMesh();
            _gridMesh = CreateGridMesh();
            // Three visible road areas stay alive. When every racer reaches the third,
            // the oldest area is moved ahead and its existing scene objects are reused.
            for (int areaIndex = 0; areaIndex < _areas.Length; areaIndex++)
            {
                GameObject areaObject = new GameObject("Road Area " + (areaIndex + 1));
                areaObject.transform.SetParent(transform, false);
                areaObject.transform.position = new Vector3(0f, 0f, RoadStartZ + areaIndex * _areaLength);
                RoadArea area = new RoadArea { root = areaObject.transform, index = areaIndex };
                _areas[areaIndex] = area;
                for (int tileIndex = 0; tileIndex < _tilesPerArea; tileIndex++)
                    _tiles.Add(CreateTile(area.root, tileIndex));
            }
        }

        public void RegisterWorldShiftCar(CarController car)
        {
            if (car != null && !_worldShiftCars.Contains(car)) _worldShiftCars.Add(car);
        }

        public void RegisterWorldShiftTransform(Transform target)
        {
            if (target != null && !_worldShiftTransforms.Contains(target)) _worldShiftTransforms.Add(target);
        }

        private Tile CreateTile(Transform areaRoot, int tileIndex)
        {
            GameObject root = new GameObject("Road Section");
            root.transform.SetParent(areaRoot, false);
            root.transform.localPosition = new Vector3(0f, 0f, tileIndex * _config.tileLength);
            GameObject road = new GameObject("Road Surface");
            road.transform.SetParent(root.transform, false);
            road.AddComponent<MeshFilter>().sharedMesh = _roadMesh;
            road.AddComponent<MeshRenderer>().sharedMaterial = _roadMaterial;
            road.AddComponent<MeshCollider>().sharedMesh = _roadMesh;
            GameObject grid = new GameObject("Neon Grid");
            grid.transform.SetParent(root.transform, false);
            grid.transform.localPosition = Vector3.up * 0.018f;
            grid.AddComponent<MeshFilter>().sharedMesh = _gridMesh;
            grid.AddComponent<MeshRenderer>().sharedMaterial = _gridMaterial;
            CreateBarriers(root.transform);
            return new Tile { root = root.transform };
        }

        private Mesh CreateRoadMesh()
        {
            float half = _config.width * 0.5f;
            Mesh mesh = new Mesh { name = "Endless Road Surface" };
            mesh.vertices = new[] { new Vector3(-half, 0f, 0f), new Vector3(-half, 0f, _config.tileLength),
                new Vector3(half, 0f, _config.tileLength), new Vector3(half, 0f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _owner.RegisterRuntimeMesh(mesh);
            return mesh;
        }

        /// <summary>
        /// Grid lines are clamped to the tile bounds so neighbouring tiles never overlap at the seams
        /// (overlap would double the brightness with additive or transparent materials).
        /// </summary>
        private Mesh CreateGridMesh()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float half = _config.width * 0.5f;
            float length = _config.tileLength;
            float thickness = _config.gridThickness;
            float spacing = Mathf.Max(1f, _config.gridSpacing);

            int zLines = Mathf.FloorToInt(length / spacing);
            for (int i = 0; i <= zLines; i++)
            {
                float z = i * spacing;
                float z0 = Mathf.Max(0f, z - thickness);
                float z1 = Mathf.Min(length, z + thickness);
                AddQuad(vertices, triangles, new Vector3(-half, 0f, z0), new Vector3(-half, 0f, z1),
                    new Vector3(half, 0f, z1), new Vector3(half, 0f, z0));
            }

            int xLines = Mathf.FloorToInt(_config.width / spacing);
            for (int i = 0; i <= xLines; i++)
                AddLongitudinalLine(vertices, triangles, -half + i * spacing, half, length, thickness);
            if (xLines * spacing < _config.width - 0.0001f)
                AddLongitudinalLine(vertices, triangles, half, half, length, thickness);

            Mesh mesh = new Mesh { name = "Endless Road Grid" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _owner.RegisterRuntimeMesh(mesh);
            return mesh;
        }

        private static void AddLongitudinalLine(List<Vector3> vertices, List<int> triangles, float x, float half, float length, float thickness)
        {
            float x0 = Mathf.Max(-half, x - thickness);
            float x1 = Mathf.Min(half, x + thickness);
            AddQuad(vertices, triangles, new Vector3(x0, 0f, 0f), new Vector3(x0, 0f, length),
                new Vector3(x1, 0f, length), new Vector3(x1, 0f, 0f));
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int index = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
        }

        /// <summary>Walls fill the whole tile length with no gaps, including at tile seams.</summary>
        private void CreateBarriers(Transform parent)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(_config.tileLength / Mathf.Max(1f, _config.barrierSpacing)));
            float wallLength = _config.tileLength / count;
            for (int i = 0; i < count; i++)
            {
                float z = (i + 0.5f) * wallLength;
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = ThemeText.WallName;
                    wall.transform.SetParent(parent, false);
                    wall.transform.localPosition = new Vector3(side * (_config.width * 0.5f + 0.45f), 0.65f, z);
                    wall.transform.localScale = new Vector3(0.55f, 1.3f, wallLength);
                    wall.GetComponent<Renderer>().sharedMaterial = _barrierMaterial;
                }
            }
        }

        private void Update()
        {
            if (_car == null || _tiles.Count == 0) return;

            float carZ = _car.position.z;
            float currentDistance = Mathf.Max(0f, carZ - _startCarZ);
            DistanceTravelled = Mathf.Max(DistanceTravelled, currentDistance);

            // Rolling mode has no authored checkpoint gates. Advance the player's
            // recovery pose while the car is upright, grounded, and still over the road.
            // Without this, every fall/flip reset uses the original start pose.
            CarController playerCar = _car.GetComponent<CarController>();
            if (playerCar != null && playerCar.IsGrounded && _car.position.y > -2f
                && Mathf.Abs(_car.position.x) <= _config.width * 0.5f - 1f
                && currentDistance >= _lastSafeDistance + 12f)
            {
                Vector3 safePosition = _car.position;
                safePosition.y = Mathf.Max(0.8f, _car.position.y);
                Vector3 safeForward = Vector3.ProjectOnPlane(_car.forward, Vector3.up);
                if (safeForward.sqrMagnitude < 0.01f) safeForward = Vector3.forward;
                playerCar.SetSafeRespawnPose(safePosition, Quaternion.LookRotation(safeForward.normalized, Vector3.up));
                _lastSafeDistance = currentDistance;
            }
            RecycleOldestAreaWhenRacersReachThird();
        }

        private void RecycleOldestAreaWhenRacersReachThird()
        {
            int oldest = 0;
            int newest = 0;
            for (int i = 1; i < _areas.Length; i++)
            {
                if (_areas[i].index < _areas[oldest].index) oldest = i;
                if (_areas[i].index > _areas[newest].index) newest = i;
            }
            int firstSectionIndex = _areas[oldest].index;
            float thirdAreaStart = RoadStartZ + (firstSectionIndex + 2) * _areaLength;
            for (int i = 0; i < _worldShiftCars.Count; i++)
            {
                CarController racer = _worldShiftCars[i];
                if (racer == null || racer.transform.position.y < -5f
                    || racer.transform.position.z < thirdAreaStart)
                    return;
            }

            RoadArea area = _areas[oldest];
            area.index = _areas[newest].index + 1;
            area.root.position = new Vector3(0f, 0f, RoadStartZ + area.index * _areaLength);
            Physics.SyncTransforms();
        }
    }
}
