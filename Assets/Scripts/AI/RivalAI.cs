using UnityEngine;

namespace TheRacer
{
    /// <summary>Non-allocating line-following controller that drives a normal CarController.</summary>
    [DefaultExecutionOrder(-20)]
    public sealed class RivalAI : MonoBehaviour, ICarInput
    {
        private readonly RaycastHit[] _avoidHits = new RaycastHit[16];
        private CarController _car;
        private RollingTrack _track;
        private CarController _player;
        private RacePositionTracker _positions;
        private RacerState _state;
        private AIPersonality _personality;
        private RivalDefinition _identity;
        private RaceConfig _raceConfig;
        private Rigidbody _body;
        private DriftBoostSystem _driftBoost;
        private System.Random _random;
        private float _laneOffset;
        private float _skillMultiplier = 1f;
        private float _throttle, _brake, _steer;
        private bool _handbrake, _boost, _released;
        private float _stuckTimer, _mistakeTimer, _mistakeRemaining, _mistakeSteer;
        private float _avoidLaneOffset;
        private float _avoidanceRemaining;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        public string Handle => _identity != null ? _identity.Handle : string.Empty;
        public string Crew => _identity != null ? _identity.Crew : string.Empty;
        public string Taunt => _identity != null ? _identity.Taunt : string.Empty;
        public AIPersonality Personality => _personality;
        public RacerState State => _state;
        public float Throttle => _throttle;
        public float Brake => _brake;
        public float Steer => _steer;
        public bool Handbrake => _handbrake;
        public bool Boost => _boost;
        public bool ResetPressed => false;
        public bool PausePressed => false;
        public bool CameraTogglePressed => false;

        public void Configure(CarController car, RollingTrack track, CarController player, RivalDefinition identity,
            RaceConfig raceConfig, float skillMultiplier, int randomSeed, float laneOffset)
        {
            _car = car; _player = player; _identity = identity;
            _personality = AIPersonality.CreatePreset(identity.Personality);
            _raceConfig = raceConfig; _skillMultiplier = Mathf.Max(0.1f, skillMultiplier);
            _random = new System.Random(randomSeed); _laneOffset = laneOffset;
            _body = car.GetComponent<Rigidbody>();
            _spawnPosition = car.transform.position;
            _spawnRotation = car.transform.rotation;
            BindTrack(track);
        }

        public void SetRaceState(RacePositionTracker positions, RacerState state)
        {
            _positions = positions; _state = state;
            DriftBoostSystem boost = _car.GetComponent<DriftBoostSystem>();
            if (boost != null) boost.AddCore(_car.OverclockCapacity);
        }

        public void BindTrack(RollingTrack track)
        {
            if (_track != null) _track.WorldShifted -= OnTrackWorldShifted;
            _track = track;
            if (_track != null) _track.WorldShifted += OnTrackWorldShifted;
            _released = false;
            _throttle = _brake = _steer = 0f;
            _handbrake = _boost = false;
            _stuckTimer = 0f;
            _avoidanceRemaining = 0f;
            _avoidLaneOffset = 0f;
        }

        public void SetRaceStarted(bool started)
        {
            _released = started;
            if (!started) _throttle = _brake = _steer = 0f;
        }

        private void Update()
        {
            if (!_released || _car == null || _track == null || _personality == null)
            {
                _throttle = _brake = _steer = 0f; _handbrake = _boost = false; return;
            }
            if (_car.transform.position.y < -3f
                || Mathf.Abs(_car.transform.position.x) > _track.RoadWidth * 0.5f + 8f)
            {
                RecoverBehindPlayer();
                return;
            }
            if (_car.IsGrounded && Mathf.Abs(_car.transform.position.x) < _track.RoadWidth * 0.5f - 1f)
            {
                Vector3 safe = _car.transform.position;
                safe.y = Mathf.Max(0.8f, safe.y);
                _car.SetSafeRespawnPose(safe, Quaternion.LookRotation(Vector3.forward, Vector3.up));
            }
            UpdateMistake();
            _avoidanceRemaining = Mathf.Max(0f, _avoidanceRemaining - Time.deltaTime);
            if (_avoidanceRemaining <= 0f) _avoidLaneOffset = Mathf.MoveTowards(_avoidLaneOffset, 0f, Time.deltaTime * 3f);
            float distance = _track.GetDistanceAtPosition(_car.transform.position);
            float speed = _car.SpeedMS;
            float lookAhead = _personality.lookAheadBase + speed * 0.45f;
            Vector3 target = _track.GetRoadPoint(distance + lookAhead, _laneOffset + _avoidLaneOffset);
            Vector3 localTarget = _car.transform.InverseTransformPoint(target);
            float headingError = Mathf.Atan2(localTarget.x, Mathf.Max(1f, localTarget.z));
            float yawRate = Vector3.Dot(_body.angularVelocity, Vector3.up);
            _steer = Mathf.Clamp(headingError * 3.1f - yawRate * 0.18f + _mistakeSteer, -1f, 1f);

            float curvature = GetUpcomingCurvature(distance, lookAhead);
            ApplyAvoidance(ref curvature);
            float cornerFactor = Mathf.Lerp(1f, _personality.cornerSpeedFactor, Mathf.Clamp01(curvature * 11f));
            float rubberFactor = GetRubberBandFactor();
            float targetSpeed = _car.MaxSpeed * _personality.topSpeedFactor * _skillMultiplier * rubberFactor * cornerFactor;
            targetSpeed = Mathf.Clamp(targetSpeed, _car.MaxSpeed * 0.72f, _car.MaxSpeed * 1.12f);
            _car.SpeedLimitMultiplier = targetSpeed / Mathf.Max(1f, _car.MaxSpeed);

            if (speed < targetSpeed - 1.5f) { _throttle = 1f; _brake = 0f; }
            else if (speed > targetSpeed + 3f) { _throttle = 0f; _brake = 0.65f; }
            else { _throttle = 0.32f; _brake = 0f; }
            _handbrake = _personality.driftiness > 0.65f && curvature > 0.025f && speed > targetSpeed + 3f;
            bool straight = curvature < 0.012f;
            _boost = straight && _driftBoost != null && _driftBoost.Core >= _car.OverclockCapacity * 0.95f;

            _stuckTimer = speed < 1f && _throttle > 0.5f ? _stuckTimer + Time.deltaTime : 0f;
            if (_stuckTimer > 4f || Vector3.Dot(_car.transform.up, Vector3.up) < 0.1f)
                RecoverBehindPlayer();
        }

        private float GetUpcomingCurvature(float distance, float lookAhead)
        {
            Vector3 near = _track.GetRoadForward(distance + lookAhead);
            Vector3 far = _track.GetRoadForward(distance + lookAhead + 9f);
            return Vector3.Angle(near, far) / 9f;
        }

        private void ApplyAvoidance(ref float curvature)
        {
            Vector3 origin = _car.transform.position + Vector3.up * 0.5f;
            int hitCount = Physics.RaycastNonAlloc(origin, _car.transform.forward, _avoidHits, 12f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            float nearest = float.PositiveInfinity;
            RaycastHit selected = default;
            CarController otherCar = null;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _avoidHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(_car.transform) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                selected = hit;
                otherCar = _positions != null ? _positions.GetRacerFromCollider(hit.collider) : null;
            }
            if (nearest >= 12f) return;
            float side = Vector3.Dot(selected.point - _car.transform.position, _car.transform.right);
            if (otherCar != null && _personality.aggression > 0.8f && nearest < 6f)
                _steer = Mathf.Clamp(_steer + Mathf.Sign(side) * 0.2f, -1f, 1f);
            else
            {
                if (_avoidanceRemaining <= 0f)
                    _avoidLaneOffset = _laneOffset >= 0f ? -5f : 5f;
                _avoidanceRemaining = 1.2f;
                _steer = Mathf.Clamp(_steer - Mathf.Sign(side == 0f ? 1f : side) * Mathf.Clamp01((12f - nearest) / 8f), -1f, 1f);
            }
            if (nearest < 7f) curvature = Mathf.Max(curvature, 0.04f);
        }

        private float GetRubberBandFactor()
        {
            if (_positions == null || _player == null || _raceConfig == null) return 1f;
            float delta = _positions.GetProgress(_car) - _positions.GetProgress(_player);
            float threshold = Mathf.Max(5f, _raceConfig.rubberBandDistance);
            float amount = Mathf.Clamp01(Mathf.Abs(delta) / threshold) * _raceConfig.rubberBandStrength;
            return Mathf.Clamp(1f + (delta < 0f ? amount : -amount), 0.94f, 1.06f);
        }

        private void UpdateMistake()
        {
            if (_mistakeRemaining > 0f)
            {
                _mistakeRemaining -= Time.deltaTime;
                return;
            }
            _mistakeTimer += Time.deltaTime;
            if (_mistakeTimer < 6f) return;
            _mistakeTimer = 0f;
            if (_random.NextDouble() < _personality.mistakeChance)
            {
                _mistakeRemaining = 0.45f;
                _mistakeSteer = (float)(_random.NextDouble() * 2.0 - 1.0) * 0.28f;
            }
            else _mistakeSteer = 0f;
        }

    private void RecoverBehindPlayer()
        {
            float playerDistance = _track != null && _player != null
                ? _track.GetDistanceAtPosition(_player.transform.position) : 100f;
            float recoveryDistance = Mathf.Max(0f, playerDistance - 100f);
            Vector3 recoveryPosition = _track != null
                ? _track.GetRoadPoint(recoveryDistance, _laneOffset) + Vector3.up * 0.9f
                : _spawnPosition;
            _car.TeleportTo(recoveryPosition, Quaternion.identity);
            if (_positions != null) _positions.ResetRacer(_car);
            _stuckTimer = 0f;
        }

        private void Start() { if (_car != null) _driftBoost = _car.GetComponent<DriftBoostSystem>(); }

        private void OnTrackWorldShifted(Vector3 shift) { _spawnPosition += shift; }

        private void OnDestroy()
        {
            if (_track != null) _track.WorldShifted -= OnTrackWorldShifted;
            if (_car != null) _car.SpeedLimitMultiplier = 1f;
        }
    }
}
