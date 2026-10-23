using UnityEngine;

namespace TheRacer
{
    /// <summary>Maintains live race order using a fixed array and in-place insertion sort.</summary>
    public sealed class RacePositionTracker : MonoBehaviour
    {
        private const int MaxRacers = 6;
        private readonly CarController[] _cars = new CarController[MaxRacers];
        private readonly RacerState[] _states = new RacerState[MaxRacers];
        private readonly float[] _maxDistance = new float[MaxRacers];
        private readonly int[] _positions = new int[MaxRacers];
        private readonly int[] _sortOrder = new int[MaxRacers];
        private RollingTrack _track;
        private RaceConfig _config;
        private int _count;
        private bool _released;

        public int RacerCount => _count;
        public float RaceDistance => _config != null ? Mathf.Max(1f, _config.raceDistance) : 1500f;
        public CarController PlayerCar { get; private set; }

        public void Configure(RollingTrack track, RaceConfig config, CarController playerCar)
        {
            _track = track; _config = config; PlayerCar = playerCar;
            _count = 0; _released = false;
            for (int i = 0; i < MaxRacers; i++) { _cars[i] = null; _states[i] = null; _maxDistance[i] = 0f; _positions[i] = 0; }
            FxEvents.OnPlayerReset += OnPlayerReset;
        }

        /// <summary>Registers one car and creates its independent distance-race state.</summary>
        public RacerState RegisterRacer(CarController car)
        {
            if (car == null || _count >= MaxRacers) return null;
            for (int i = 0; i < _count; i++) if (_cars[i] == car) return _states[i];
            int slot = _count++;
            _cars[slot] = car;
            _states[slot] = new RacerState(1, null);
            _maxDistance[slot] = Mathf.Max(0f, _track.GetDistanceAtPosition(car.transform.position));
            _states[slot].TickDistance(0f, _maxDistance[slot]);
            _positions[slot] = slot + 1;
            return _states[slot];
        }

        public void SetRaceStarted(bool started)
        {
            _released = started;
            if (!started) return;
        }

        public void RebindTrack(RollingTrack track)
        {
            _track = track;
            _released = false;
            for (int i = 0; i < _count; i++)
            {
                _states[i].ResetRace();
                _maxDistance[i] = Mathf.Max(0f, _track.GetDistanceAtPosition(_cars[i].transform.position));
                _states[i].TickDistance(0f, _maxDistance[i]);
                _positions[i] = i + 1;
            }
        }

        public RacerState GetState(CarController car)
        {
            int index = Find(car);
            return index >= 0 ? _states[index] : null;
        }

        public int GetPosition(CarController car)
        {
            int index = Find(car);
            return index >= 0 ? _positions[index] : 0;
        }

        public float GetProgress(CarController car)
        {
            int index = Find(car);
            return index >= 0 ? _maxDistance[index] : 0f;
        }

        /// <summary>Resolves car colliders against registered racers without component lookups.</summary>
        public CarController GetRacerFromCollider(Collider collider)
        {
            if (collider == null) return null;
            Transform hitTransform = collider.transform;
            for (int i = 0; i < _count; i++)
                if (hitTransform == _cars[i].transform || hitTransform.IsChildOf(_cars[i].transform)) return _cars[i];
            return null;
        }

        public void ResetRacer(CarController car)
        {
            int index = Find(car);
            if (index < 0) return;
            _states[index].ResetRace();
            _maxDistance[index] = Mathf.Max(0f, _track.GetDistanceAtPosition(car.transform.position));
            _states[index].TickDistance(0f, _maxDistance[index]);
        }

        private void Update()
        {
            if (!_released || _track == null) return;
            float deltaTime = Time.deltaTime;
            for (int i = 0; i < _count; i++)
            {
                CarController car = _cars[i];
                if (car == null || _states[i].Finished) continue;
                float totalDistance = _track.GetDistanceAtPosition(car.transform.position);
                if (totalDistance > _maxDistance[i]) _maxDistance[i] = totalDistance;
                _states[i].TickDistance(deltaTime, _maxDistance[i]);
                if (_maxDistance[i] >= RaceDistance) _states[i].FinishRace();
            }
            SortPositions();
        }

        private void SortPositions()
        {
            for (int i = 0; i < _count; i++) _sortOrder[i] = i;
            for (int i = 1; i < _count; i++)
            {
                int key = _sortOrder[i];
                int j = i - 1;
                while (j >= 0 && _maxDistance[_sortOrder[j]] < _maxDistance[key])
                { _sortOrder[j + 1] = _sortOrder[j]; j--; }
                _sortOrder[j + 1] = key;
            }
            for (int i = 0; i < _count; i++) _positions[_sortOrder[i]] = i + 1;
        }

        private int Find(CarController car)
        {
            for (int i = 0; i < _count; i++) if (_cars[i] == car) return i;
            return -1;
        }

        private void OnPlayerReset()
        {
            if (PlayerCar != null) ResetRacer(PlayerCar);
        }

        private void OnDestroy() { FxEvents.OnPlayerReset -= OnPlayerReset; }
    }
}
