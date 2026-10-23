using System.Collections.Generic;
using UnityEngine;

namespace TheRacer
{
    /// <summary>Owns ordered checkpoint gates and temporary race feedback for the player.</summary>
    public sealed class CheckpointSystem : MonoBehaviour
    {
        private readonly List<CheckpointGate> _checkpoints = new List<CheckpointGate>();
        private TrackData _trackData;
        private RollingTrack _rollingTrack;
        private RacerState _state;
        private CarController _car;
        private float _roadStartZ;
        private float _spawnHeight;
        private CheckpointGate _startGate;

        public RacerState State => _state;
        public TrackData Data => _trackData;

        public void ConfigureRollingTrack(RollingTrack rollingTrack)
        {
            _rollingTrack = rollingTrack;
        }

        public void Configure(TrackData data, TestRoadConfig roadConfig, CarConfig carConfig,
            CarController car, Material gateMaterial, Material finishMaterial, int totalLaps)
        {
            _trackData = data;
            _car = car;
            _roadStartZ = roadConfig.centerZ - roadConfig.length * 0.5f;
            _spawnHeight = carConfig.spawnHeight;
            if (_rollingTrack != null)
            {
                _state = new RacerState(int.MaxValue, null);
                return;
            }

            _startGate = CreateGate("Start Line", 4f, -1, CheckpointGateKind.Start,
                data.GetWidth(4f), gateMaterial);
            _state = new RacerState(totalLaps, _startGate.SafeTransform);

            float spacing = 60f;
            for (float distance = spacing; distance < data.lapLength - 10f; distance += spacing)
            {
                CheckpointGate checkpoint = CreateGate("Checkpoint " + (_checkpoints.Count + 1), distance,
                    _checkpoints.Count, CheckpointGateKind.Checkpoint, data.GetWidth(distance), gateMaterial);
                _checkpoints.Add(checkpoint);
            }

            float finishDistance = Mathf.Max(8f, data.lapLength - 4f);
            CreateGate("Finish Line", finishDistance, -1, CheckpointGateKind.Finish,
                data.GetWidth(finishDistance), finishMaterial);

            Vector3 spawnPosition = new Vector3(0f, _spawnHeight, _roadStartZ + 7f);
            _car.ConfigureCheckpointRespawn(spawnPosition, Quaternion.identity);
        }

        private CheckpointGate CreateGate(string gateName, float distance, int index,
            CheckpointGateKind kind, float width, Material material)
        {
            GameObject gateObject = new GameObject(gateName);
            gateObject.transform.SetParent(transform, false);
            gateObject.transform.position = new Vector3(0f, 0f, _roadStartZ + distance);
            CheckpointGate gate = gateObject.AddComponent<CheckpointGate>();
            gate.Initialize(this, index, kind, width, material, _spawnHeight);
            return gate;
        }

        internal void Visit(CheckpointGate gate, CarController car)
        {
            if (gate == null || car != _car || _state == null || _state.Finished)
                return;
            if (car.ForwardSpeed <= 0f)
                return;

            if (gate.Kind == CheckpointGateKind.Checkpoint)
            {
                if (gate.Index != _state.NextCheckpointIndex)
                    return;
                _state.RecordCheckpoint(gate.Index, gate.SafeTransform);
                _car.SetSafeRespawnPose(gate.SafeTransform.position, gate.SafeTransform.rotation);
                return;
            }

            if (gate.Kind != CheckpointGateKind.Finish || _state.NextCheckpointIndex != _checkpoints.Count)
                return;

            if (!_state.CompleteLap() || _state.Finished)
                return;

            Vector3 spawnPosition = new Vector3(0f, _spawnHeight, _roadStartZ + 7f);
            _state.SetSafeCheckpoint(_startGate.SafeTransform);
            _car.ConfigureCheckpointRespawn(spawnPosition, Quaternion.identity);
        }

        private void Update()
        {
            if (_state == null || _car == null || _trackData == null)
                return;
            float distance = _rollingTrack != null ? _rollingTrack.DistanceTravelled : _trackData.GetDistance(_car.transform.position, _roadStartZ);
            _state.Tick(Time.deltaTime, distance, _trackData.lapLength);
        }

        private void OnDestroy()
        {
            if (_state != null)
                _state.ClearEvents();
            _checkpoints.Clear();
            _state = null;
            _car = null;
        }
    }
}
