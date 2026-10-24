using UnityEngine;

namespace TheRacer
{
    /// <summary>Rigidbody arcade car with raycast suspension and temporary primitive visuals.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class CarController : MonoBehaviour
    {
        private const int WheelCount = 4;
        private readonly RaycastHit[] _rayHits = new RaycastHit[12];
        private readonly Vector3[] _suspensionAnchors = new Vector3[WheelCount];
        private Rigidbody _body;
        private BoxCollider _collider;
        private ICarInput _input;
        private PlayerCarInput _playerInput;
        private CarConfig _config;
        private CarVisualProfile _visualProfile;
        private CarVisuals _visuals;
        private CarDamage _damage;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Vector3 _safeRespawnPosition;
        private Quaternion _safeRespawnRotation;
        private Vector3 _groundNormal = Vector3.up;
        private float _stuckTime;
        private float _flippedTime;
        private float _steerAngle;
        private bool _isGrounded;
        private bool _initialized;
        private DriftBoostSystem _driftBoost;
        private float _pickupBoostRemaining;
        private float _pickupSpeedMultiplier = 1f;
        public float SpeedLimitMultiplier { get; set; } = 1f;
        public event System.Action Teleported;

        public float GripMultiplier { get; set; } = 1f;
        public float SpeedMS => _body != null ? ReadVelocity().magnitude : 0f;
        public float SpeedRatio => _config != null ? Mathf.Clamp01(SpeedMS / Mathf.Max(0.1f, _config.maxSpeed)) : 0f;
        public float SpeedKMH => SpeedMS * 3.6f;
        public float ForwardSpeed => _body != null ? Vector3.Dot(ReadVelocity(), ProjectedForward) : 0f;
        public float LateralSpeed => _body != null ? Vector3.Dot(ReadVelocity(), transform.right) : 0f;
        public bool IsGrounded => _isGrounded;
        public float SteerAngle => _steerAngle;
        public Transform[] WheelTransforms => _visuals != null ? _visuals.WheelTransforms : null;
        public float OverclockCore => _driftBoost != null ? _driftBoost.Core : 0f;
        public float OverclockCapacity => _config != null ? _config.overclockCapacity : 100f;
        public float MaxSpeed => _config != null ? _config.maxSpeed : 60f;
        public bool IsPlayerControlled => _playerInput != null;
        private Vector3 ProjectedForward => Vector3.ProjectOnPlane(transform.forward, _groundNormal).normalized;

        public void Configure(CarConfig config, CarVisualProfile visualProfile)
        {
            _config = config;
            _visualProfile = visualProfile != null ? visualProfile.Copy() : new CarVisualProfile();
        }

        /// <summary>Assigns a player or rival input source before the car is activated.</summary>
        public void ConfigureInput(ICarInput input)
        {
            _input = input;
        }

        private void Awake()
        {
            if (_config == null)
                _config = new CarConfig();
            _body = GetComponent<Rigidbody>();
            _collider = GetComponent<BoxCollider>();
            _playerInput = GetComponent<PlayerCarInput>();
            if (_input == null) _input = _playerInput;
            ConfigurePhysics();
            BuildVisuals();
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _safeRespawnPosition = _spawnPosition;
            _safeRespawnRotation = _spawnRotation;
            _initialized = true;
        }

        public void ConfigureCheckpointRespawn(Vector3 position, Quaternion rotation)
        {
            _safeRespawnPosition = position;
            _safeRespawnRotation = rotation;
            TeleportTo(position, rotation);
        }

        public void SetSafeRespawnPose(Vector3 position, Quaternion rotation)
        {
            _safeRespawnPosition = position;
            _safeRespawnRotation = rotation;
        }

        private void ConfigurePhysics()
        {
            _body.mass = _config.mass;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _body.maxAngularVelocity = 8f;
            SetBodyDamping(_body);
            _body.centerOfMass = _config.centerOfMass;
            _collider.center = _config.bodyColliderCenter;
            _collider.size = _config.bodyColliderSize;
            _collider.material = null;
            _suspensionAnchors[0] = new Vector3(-_config.suspensionSideOffset, _config.suspensionAnchorHeight, _config.suspensionAxleOffset);
            _suspensionAnchors[1] = new Vector3(_config.suspensionSideOffset, _config.suspensionAnchorHeight, _config.suspensionAxleOffset);
            _suspensionAnchors[2] = new Vector3(-_config.suspensionSideOffset, _config.suspensionAnchorHeight, -_config.suspensionAxleOffset);
            _suspensionAnchors[3] = new Vector3(_config.suspensionSideOffset, _config.suspensionAnchorHeight, -_config.suspensionAxleOffset);
        }

        private void BuildVisuals()
        {
            _visuals = GetComponent<CarVisuals>();
            if (_visuals == null)
                _visuals = gameObject.AddComponent<CarVisuals>();
            _visuals.Build(_config, _visualProfile);
            _damage = GetComponent<CarDamage>();
            if (_damage == null)
                _damage = gameObject.AddComponent<CarDamage>();
            _damage.Configure(_visuals);
            _driftBoost = gameObject.AddComponent<DriftBoostSystem>();
            _driftBoost.Configure(this, _input, _config);
        }

        private void FixedUpdate()
        {
            if (!_initialized)
                return;
            if (_input != null && _input.ResetPressed)
                ResetToSpawn();
            // RivalAI owns rival recovery so they can rejoin near the player instead of
            // being silently teleported back to their original grid position.
            if (_playerInput != null && transform.position.y < _config.fallResetHeight)
                ResetToSpawn();

            if (_pickupBoostRemaining > 0f)
            {
                _pickupBoostRemaining = Mathf.Max(0f, _pickupBoostRemaining - Time.fixedDeltaTime);
                if (_pickupBoostRemaining <= 0f) _pickupSpeedMultiplier = 1f;
            }

            ApplySuspension();
            ApplyDriving();
            ApplyLateralGrip();
            ApplyDownforceAndDrag();
            ApplyStability();
            if (_playerInput != null)
                UpdateResetTimers();
        }

        private void ApplySuspension()
        {
            int groundedRays = 0;
            Vector3 normalSum = Vector3.zero;
            for (int i = 0; i < WheelCount; i++)
            {
                Vector3 origin = transform.TransformPoint(_suspensionAnchors[i]);
                int hitCount = Physics.RaycastNonAlloc(origin, -transform.up, _rayHits,
                    _config.suspensionRayLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float nearestDistance = float.PositiveInfinity;
                RaycastHit nearestHit = default;
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit hit = _rayHits[hitIndex];
                    if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) && hit.distance < nearestDistance)
                    {
                        nearestDistance = hit.distance;
                        nearestHit = hit;
                    }
                }
                if (nearestDistance >= _config.suspensionRayLength)
                    continue;

                groundedRays++;
                normalSum += nearestHit.normal;
                Vector3 pointVelocity = _body.GetPointVelocity(origin);
                float springOffset = _config.suspensionRestLength - nearestDistance;
                float damping = Vector3.Dot(pointVelocity, nearestHit.normal) * _config.suspensionDamper;
                float force = Mathf.Clamp(springOffset * _config.suspensionSpring - damping, 0f, _body.mass * 12f);
                _body.AddForceAtPosition(nearestHit.normal * force, origin, ForceMode.Force);
            }
            _isGrounded = groundedRays >= 2;
            if (_isGrounded && normalSum.sqrMagnitude > 0.001f)
                _groundNormal = normalSum.normalized;
            else
                _groundNormal = Vector3.up;
        }

        private void ApplyDriving()
        {
            if (_input == null)
                return;
            Vector3 forward = ProjectedForward;
            if (forward.sqrMagnitude < 0.5f)
                forward = transform.forward;
            Vector3 velocity = ReadVelocity();
            float forwardSpeed = Vector3.Dot(velocity, forward);
            float speedRatio = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / _config.maxSpeed);
            float powerCurve = Mathf.Lerp(1f, _config.minimumPowerFraction,
                Mathf.Pow(speedRatio, _config.powerCurveExponent));
            float throttle = _input.Throttle;
            float brake = _input.Brake;

            float speedLimit = _config.maxSpeed * Mathf.Max(SpeedLimitMultiplier,
                Mathf.Max(_driftBoost != null ? _driftBoost.SpeedMultiplier : 1f, _pickupSpeedMultiplier));
            if (brake > 0.01f && forwardSpeed > 0.5f)
                _body.AddForce(-forward * Mathf.Min(brake * _config.brakeDecel, forwardSpeed / Time.fixedDeltaTime) * _body.mass, ForceMode.Force);
            else if (throttle > 0.01f && forwardSpeed < speedLimit)
                _body.AddForce(forward * (throttle * _config.driveAcceleration * powerCurve * _body.mass), ForceMode.Force);
            else if (brake > 0.01f && forwardSpeed <= 0.5f && forwardSpeed > -_config.reverseMaxSpeed)
                _body.AddForce(-forward * (brake * _config.driveAcceleration * 0.55f * _body.mass), ForceMode.Force);

            float inputSteer = Mathf.Clamp(_input.Steer, -1f, 1f);
            float maxYawRate = Mathf.Lerp(_config.maxYawRateLowSpeed, _config.maxYawRateHighSpeed,
                Mathf.Clamp01(Mathf.Abs(forwardSpeed) / _config.maxSpeed));
            float controlScale = _isGrounded ? 1f : _config.airControlMultiplier;
            float targetYawRate = inputSteer * maxYawRate * controlScale;
            float currentYawRate = Vector3.Dot(_body.angularVelocity, _groundNormal);
            float newYawRate = Mathf.Lerp(currentYawRate, targetYawRate, Mathf.Clamp01(_config.steeringResponse * Time.fixedDeltaTime));
            _body.angularVelocity += _groundNormal * (newYawRate - currentYawRate);
            _steerAngle = inputSteer * _config.maxWheelSteerAngle;
        }

        private void ApplyLateralGrip()
        {
            if (!_isGrounded)
                return;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, _groundNormal).normalized;
            Vector3 velocity = ReadVelocity();
            float lateralSpeed = Vector3.Dot(velocity, right);
            float grip = _config.lateralGrip * Mathf.Max(0f, GripMultiplier);
            if (_driftBoost != null)
                grip = Mathf.Lerp(grip, _config.driftGrip, _driftBoost.DriftBlend);
            float removal = Mathf.Clamp01(grip * Time.fixedDeltaTime);
            WriteVelocity(velocity - right * (lateralSpeed * removal));
        }

        private void ApplyDownforceAndDrag()
        {
            Vector3 velocity = ReadVelocity();
            float speed = velocity.magnitude;
            if (_isGrounded && speed > 0.1f)
                _body.AddForce(-_groundNormal * (speed * _config.downforceCoefficient), ForceMode.Force);
            if (speed > 0.1f)
            {
                float dragAcceleration = speed * speed * _config.dragCoefficient + _config.rollingResistance;
                _body.AddForce(-velocity.normalized * (dragAcceleration * _body.mass), ForceMode.Force);
            }
        }

        private void ApplyStability()
        {
            if (_isGrounded)
                return;
            Vector3 tiltTorque = Vector3.Cross(transform.up, Vector3.up) * _config.airUprightStrength;
            _body.AddTorque(tiltTorque - new Vector3(_body.angularVelocity.x, 0f, _body.angularVelocity.z) * _config.airAngularDamping,
                ForceMode.Acceleration);
        }

        private void UpdateResetTimers()
        {
            bool flipped = Vector3.Dot(transform.up, Vector3.up) < Mathf.Cos(_config.flipResetAngle * Mathf.Deg2Rad);
            _flippedTime = flipped ? _flippedTime + Time.fixedDeltaTime : 0f;
            bool tryingButStuck = _isGrounded && _input != null && (_input.Throttle > 0.2f || _input.Brake > 0.2f)
                && ReadVelocity().sqrMagnitude < 0.16f;
            _stuckTime = tryingButStuck ? _stuckTime + Time.fixedDeltaTime : 0f;
            if (_flippedTime >= _config.flipResetDelay || _stuckTime >= _config.stuckResetDelay)
                ResetToSpawn();
        }

        private void Update()
        {
            if (!_initialized)
                return;
            _visuals?.UpdateWheels(ForwardSpeed, _steerAngle, Time.deltaTime);
            _visuals?.SetBrake(_input != null ? _input.Brake : 0f);
            if (_playerInput != null && _playerInput.DevVisualCyclePressed)
                _visuals?.CycleDevProfile();
        }

        private void OnCollisionEnter(Collision collision)
        {
            RaceAudioController.NotifyCarCollision(this, collision);
            float impact = collision.relativeVelocity.magnitude;
            if (impact < _config.impactThreshold)
                return;
            WriteVelocity(ReadVelocity() * _config.impactSpeedRetention);
            FxEvents.RaiseImpact(this, impact);
        }

        public void ResetToSpawn()
        {
            if (_body == null)
                return;
            _pickupBoostRemaining = 0f;
            _pickupSpeedMultiplier = 1f;
            if (_driftBoost != null) _driftBoost.ResetBoost();
            TeleportTo(_safeRespawnPosition, _safeRespawnRotation);
            if (IsPlayerControlled) FxEvents.RaisePlayerReset();
        }

        /// <summary>Starts or refreshes the temporary pickup speed boost.</summary>
        public void ApplyPickupBoost(float duration, float speedMultiplier = 1.5f)
        {
            _pickupBoostRemaining = Mathf.Max(_pickupBoostRemaining, duration);
            _pickupSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, _groundNormal).normalized;
            if (forward.sqrMagnitude < 0.5f)
                forward = transform.forward;
            Vector3 velocity = ReadVelocity();
            float currentForwardSpeed = Vector3.Dot(velocity, forward);
            float boostedForwardSpeed = Mathf.Max(currentForwardSpeed, 0f) * _pickupSpeedMultiplier;
            boostedForwardSpeed = Mathf.Min(boostedForwardSpeed, _config.maxSpeed * _pickupSpeedMultiplier);
            boostedForwardSpeed = Mathf.Max(currentForwardSpeed, boostedForwardSpeed);
            WriteVelocity(velocity + forward * (boostedForwardSpeed - currentForwardSpeed));
        }

        /// <summary>Applies the pooled obstacle's speed penalty.</summary>
        public void ApplyObstacleImpact()
        {
            WriteVelocity(ReadVelocity() * 0.6f);
            FxEvents.RaiseImpact(this, 12f);
        }

        /// <summary>Applies configured overclock acceleration without a hot-path lookup.</summary>
        public void ApplyOverclockForce(float force)
        {
            _body.AddForce(transform.forward * force, ForceMode.Acceleration);
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (_body == null)
                return;
            WriteVelocity(Vector3.zero);
            _body.angularVelocity = Vector3.zero;
            _body.position = position;
            _body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            _flippedTime = 0f;
            _stuckTime = 0f;
            Teleported?.Invoke();
        }

        /// <summary>Moves this car with a recycled road origin without resetting its motion or progress.</summary>
        public void ShiftWorldOrigin(Vector3 shift)
        {
            if (_body == null) return;
            _body.position += shift;
            _spawnPosition += shift;
            _safeRespawnPosition += shift;
        }


        private void OnDestroy()
        {
            _initialized = false;
        }

        private Vector3 ReadVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            return _body.linearVelocity;
#else
            return _body.velocity;
#endif
        }

        private void WriteVelocity(Vector3 velocity)
        {
#if UNITY_6000_0_OR_NEWER
            _body.linearVelocity = velocity;
#else
            _body.velocity = velocity;
#endif
        }

        private static void SetBodyDamping(Rigidbody body)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearDamping = 0f;
            body.angularDamping = 0.15f;
#else
            body.drag = 0f;
            body.angularDrag = 0.15f;
#endif
        }
    }
}
