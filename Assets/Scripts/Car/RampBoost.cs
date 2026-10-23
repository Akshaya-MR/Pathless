using UnityEngine;

namespace TheRacer
{
    /// <summary>Applies a forward and upward launch when a car collides with a solid ramp.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class RampBoost : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Initial forward speed change in metres per second.")] private float boostStrength = 8f;
        [SerializeField, Min(0.01f), Tooltip("Boost duration in seconds.")] private float boostDuration = 0.8f;
        [SerializeField, Min(0f), Tooltip("Maximum forward speed while boosted.")] private float maxBoostSpeed = 85f;
        [SerializeField, Min(0f), Tooltip("Cooldown before this ramp can trigger again.")] private float boostCooldown = 0.5f;
        [SerializeField, Min(0f), Tooltip("Vertical and forward launch impulse when touching the ramp.")] private float launchImpulse = 7f;
        private Rigidbody _body;
        private CarController _car;
        private float _timer;
        private float _cooldown;

        public void Configure(float strength, float duration, float speedCap, float launchImpulse)
        {
            boostStrength = Mathf.Max(0f, strength);
            boostDuration = Mathf.Max(0.01f, duration);
            maxBoostSpeed = Mathf.Max(0f, speedCap);
            this.launchImpulse = Mathf.Max(0f, launchImpulse);
        }

        private void Awake() { _body = GetComponentInParent<Rigidbody>(); }

        private void OnCollisionEnter(Collision collision)
        {
            CarController car = collision.collider.GetComponentInParent<CarController>();
            if (car == null || _cooldown > 0f) return;
            _car = car;
            _body = car.GetComponent<Rigidbody>();
            _timer = boostDuration;
            _cooldown = boostCooldown;
            if (_body != null)
            {
                _body.AddForce(Vector3.up * launchImpulse, ForceMode.VelocityChange);
                car.ApplyPickupBoost(boostDuration);
            }
        }

        private void FixedUpdate()
        {
            _cooldown = Mathf.Max(0f, _cooldown - Time.fixedDeltaTime);
            if (_timer <= 0f || _body == null || _car == null) return;
            float strength = boostStrength * Mathf.Clamp01(_timer / Mathf.Max(0.01f, boostDuration));
            _body.AddForce(_car.transform.forward * strength, ForceMode.Acceleration);
            Vector3 velocity = ReadVelocity();
            float forward = Vector3.Dot(velocity, _car.transform.forward);
            if (forward > maxBoostSpeed) WriteVelocity(velocity - _car.transform.forward * (forward - maxBoostSpeed));
            _timer = Mathf.Max(0f, _timer - Time.fixedDeltaTime);
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
    }
}
