using System;
using UnityEngine;

namespace TheRacer
{
    /// <summary>Simple decoupled impact hook for later effects systems.</summary>
    public static class FxEvents
    {
        public static event Action<float> OnImpact;
        public static event Action<CarController, float> OnCarImpact;
        public static event Action OnOverclockStart;
        public static event Action OnOverclockEnd;
        public static event Action<int> OnDriftBonus;
        public static event Action OnPlayerReset;

        public static void RaiseImpact(float magnitude)
        {
            OnImpact?.Invoke(magnitude);
        }
        public static void RaiseImpact(CarController car, float magnitude)
        {
            OnCarImpact?.Invoke(car, magnitude);
            OnImpact?.Invoke(magnitude);
        }
        public static void RaiseOverclockStart() { OnOverclockStart?.Invoke(); }
        public static void RaiseOverclockEnd() { OnOverclockEnd?.Invoke(); }
        public static void RaiseDriftBonus(int score) { OnDriftBonus?.Invoke(score); }
        public static void RaisePlayerReset() { OnPlayerReset?.Invoke(); }
    }

    [Serializable]
    public sealed class CameraRigConfig
    {
        [Min(0f)] public float chaseHeight = 3.2f;
        [Min(0f)] public float chaseDistance = 7.2f;
        [Min(0f)] public float speedDistanceBonus = 3.5f;
        [Min(0f)] public float positionDamping = 5f;
        [Min(0f)] public float rotationDamping = 7f;
        [Min(0f)] public float lookAheadDistance = 3.2f;
        [Min(0f)] public float hoodHeight = 1.05f;
        [Min(0f)] public float hoodForwardOffset = 1.12f;
        [Range(1f, 179f)] public float baseFieldOfView = 65f;
        [Range(1f, 179f)] public float maxFieldOfView = 78f;
        [Min(0f)] public float cameraCollisionRadius = 0.28f;
        [Min(0f)] public float shakeDuration = 0.32f;
    }

    /// <summary>Smooth speed-aware chase camera with hood view and collision pull-in.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        private readonly RaycastHit[] _cameraHits = new RaycastHit[12];
        private Camera _camera;
        private CarController _target;
        private PlayerCarInput _input;
        private RollingTrack _track;
        private CameraRigConfig _config;
        private Vector3 _positionVelocity;
        private bool _hoodView;
        private float _shakeRemaining;
        private float _shakeStrength;
        private float _fovKick;
        private bool _snapToTarget;
        private Vector3 _lastTargetPosition;
        private bool _hasLastTargetPosition;

        public void Configure(CarController target, PlayerCarInput input, CameraRigConfig config)
        {
            _target = target;
            _input = input;
            _config = config;
            if (_target != null)
                _target.Teleported += OnTargetTeleported;
        }

        public void BindTrack(RollingTrack track)
        {
            if (_track == track) return;
            if (_track != null) _track.WorldShifted -= OnTrackWorldShifted;
            _track = track;
            if (_track != null) _track.WorldShifted += OnTrackWorldShifted;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_config == null)
                _config = new CameraRigConfig();
            _camera.nearClipPlane = 0.08f;
            _camera.farClipPlane = 700f;
            _camera.fieldOfView = _config.baseFieldOfView;
            _camera.backgroundColor = ThemePalette.Void;
            _camera.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            FxEvents.OnCarImpact += HandleCarImpact;
            FxEvents.OnOverclockStart += OnOverclockStart;
        }

        private void LateUpdate()
        {
            if (_target == null)
                return;
            Vector3 targetPosition = _target.transform.position;
            if (_hasLastTargetPosition && (targetPosition - _lastTargetPosition).sqrMagnitude > 144f)
                OnTargetTeleported();
            _lastTargetPosition = targetPosition;
            _hasLastTargetPosition = true;
            if (_input != null && _input.CameraTogglePressed)
                _hoodView = !_hoodView;

            float speedRatio = _target.SpeedRatio;
            Vector3 desiredPosition;
            Vector3 lookPoint;
            if (_hoodView)
            {
                desiredPosition = _target.transform.position + _target.transform.up * _config.hoodHeight
                    + _target.transform.forward * _config.hoodForwardOffset;
                lookPoint = desiredPosition + _target.transform.forward * _config.lookAheadDistance;
            }
            else
            {
                float distance = _config.chaseDistance + _config.speedDistanceBonus * speedRatio;
                desiredPosition = _target.transform.position - _target.transform.forward * distance
                    + _target.transform.up * _config.chaseHeight;
                float turnOffset = _target.SteerAngle * 0.035f + _target.LateralSpeed * 0.08f;
                lookPoint = _target.transform.position + _target.transform.forward * _config.lookAheadDistance
                    + _target.transform.right * turnOffset;
                desiredPosition = PullInForObstacles(lookPoint, desiredPosition);
            }

            if (_shakeRemaining > 0f)
            {
                _shakeRemaining = Mathf.Max(0f, _shakeRemaining - Time.deltaTime);
                float fade = _config.shakeDuration > 0f ? _shakeRemaining / _config.shakeDuration : 0f;
                float phase = Time.time * 38f;
                desiredPosition += transform.right * (Mathf.Sin(phase) * _shakeStrength * fade)
                    + transform.up * (Mathf.Cos(phase * 1.37f) * _shakeStrength * fade * 0.6f);
            }

            float positionBlend = 1f - Mathf.Exp(-_config.positionDamping * Time.deltaTime);
            Quaternion desiredRotation;
            if (_snapToTarget)
            {
                transform.position = desiredPosition;
                desiredRotation = Quaternion.LookRotation((lookPoint - desiredPosition).normalized, Vector3.up);
                transform.rotation = desiredRotation;
                _positionVelocity = Vector3.zero;
                _snapToTarget = false;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _positionVelocity,
                    1f / Mathf.Max(0.01f, _config.positionDamping));
                desiredRotation = Quaternion.LookRotation((lookPoint - transform.position).normalized, Vector3.up);
                float rotationBlend = 1f - Mathf.Exp(-_config.rotationDamping * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
            }
            _fovKick = Mathf.MoveTowards(_fovKick, 0f, Time.deltaTime * 22f);
            float targetFov = Mathf.Lerp(_config.baseFieldOfView, _config.maxFieldOfView, speedRatio) + _fovKick;
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, positionBlend);
        }

        private Vector3 PullInForObstacles(Vector3 origin, Vector3 destination)
        {
            Vector3 offset = destination - origin;
            float distance = offset.magnitude;
            if (distance < 0.01f)
                return destination;
            Vector3 direction = offset / distance;
            int count = Physics.SphereCastNonAlloc(origin, _config.cameraCollisionRadius, direction, _cameraHits,
                distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = _cameraHits[i].collider;
                if (hitCollider != null && !hitCollider.transform.IsChildOf(_target.transform)
                    && hitCollider.GetComponentInParent<CarController>() == null
                    && _cameraHits[i].distance < nearest)
                    nearest = _cameraHits[i].distance;
            }
            return nearest < distance ? origin + direction * Mathf.Max(3.5f, nearest - 0.35f) : destination;
        }

        /// <summary>Adds a short decaying camera shake after an impact.</summary>
        public void AddShake(float magnitude)
        {
            _shakeStrength = Mathf.Max(_shakeStrength, magnitude * 0.012f);
            _shakeRemaining = _config != null ? _config.shakeDuration : 0.32f;
        }

        private void HandleCarImpact(CarController car, float magnitude)
        {
            if (car == _target) AddShake(magnitude);
        }

        private void OnTargetTeleported()
        {
            _snapToTarget = true;
            _positionVelocity = Vector3.zero;
            _shakeRemaining = 0f;
            _shakeStrength = 0f;
            _fovKick = 0f;
        }

        private void OnTrackWorldShifted(Vector3 shift)
        {
            // The car and road moved together to recycle map tiles. Rebase the camera
            // and teleport detector by the same amount to preserve the current view.
            transform.position += shift;
            if (_hasLastTargetPosition) _lastTargetPosition += shift;
        }

        private void OnOverclockStart() { _fovKick = 10f; AddShake(7f); }

        private void OnDestroy()
        {
            if (_target != null)
                _target.Teleported -= OnTargetTeleported;
            if (_track != null)
                _track.WorldShifted -= OnTrackWorldShifted;
            FxEvents.OnCarImpact -= HandleCarImpact;
            FxEvents.OnOverclockStart -= OnOverclockStart;
        }
    }
}
