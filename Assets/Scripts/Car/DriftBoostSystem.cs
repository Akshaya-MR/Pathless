using UnityEngine;

namespace TheRacer
{
    /// <summary>Owns drift detection, BURN charge, overclock force, and tire FX.</summary>
    public sealed class DriftBoostSystem : MonoBehaviour
    {
        private CarController _car;
        private ICarInput _input;
        private CarConfig _config;
        private TrailRenderer[] _trails;
        private ParticleSystem[] _smoke;
        private float _driftTime;
        private bool _overclocking;
        private bool _racingBoosting;
        private bool _wasDrifting;
        private int _driftScore;
        public float Core { get; private set; }
        public float DriftBlend { get; private set; }
        public bool IsOverclocking => _overclocking;
        public float SpeedMultiplier => _racingBoosting ? _config.overclockTopSpeedMultiplier : _overclocking ? _config.overclockTopSpeedMultiplier : 1f;

        public void Configure(CarController car, ICarInput input, CarConfig config)
        { _car = car; _input = input; _config = config; BuildFx(); }

        private void FixedUpdate()
        {
            if (_car == null || _input == null) return;
            bool drifting = _car.IsGrounded && _input.Handbrake && _car.SpeedMS > 15f && Mathf.Abs(_car.LateralSpeed) > 3f;
            DriftBlend = Mathf.MoveTowards(DriftBlend, drifting ? 1f : 0f, Time.fixedDeltaTime * 8f);
            if (drifting)
            {
                _driftTime += Time.fixedDeltaTime;
                if (_driftTime > _config.driftCoreDelay) Core = Mathf.Min(_config.overclockCapacity, Core + _config.driftCoreRate * Time.fixedDeltaTime);
                _driftScore += Mathf.RoundToInt(Mathf.Abs(_car.LateralSpeed) * Time.fixedDeltaTime);
            }
            else if (_wasDrifting && _driftScore > 0) { if (_car.IsPlayerControlled) FxEvents.RaiseDriftBonus(_driftScore); _driftScore = 0; _driftTime = 0f; }
            _wasDrifting = drifting;
            bool wantBoost = _input.Boost;
            _racingBoosting = wantBoost;
            if (!_overclocking && wantBoost && Core >= 20f) { _overclocking = true; if (_car.IsPlayerControlled) FxEvents.RaiseOverclockStart(); }
            if (_overclocking)
            {
                Core = Mathf.Max(0f, Core - _config.overclockDrainPerSecond * Time.fixedDeltaTime);
                _car.ApplyOverclockForce(_config.overclockForce);
                if (Core <= 0f || !wantBoost) { _overclocking = false; if (_car.IsPlayerControlled) FxEvents.RaiseOverclockEnd(); }
            }
            UpdateFx(drifting);
        }

        private void BuildFx()
        {
            _trails = new TrailRenderer[2]; _smoke = new ParticleSystem[2];
            Transform[] wheels = _car.WheelTransforms;
            for (int i = 0; i < 2; i++)
            {
                if (wheels == null || wheels[i + 2] == null) continue;
                GameObject fx = new GameObject("RearTireFX"); fx.transform.SetParent(wheels[i + 2], false); fx.transform.localPosition = Vector3.down * _config.wheelRadius;
                TrailRenderer trail = fx.AddComponent<TrailRenderer>(); trail.time = 0.22f; trail.startWidth = 0.16f; trail.endWidth = 0f; trail.material = new Material(Shader.Find("Sprites/Default")); trail.material.color = ThemePalette.NeonCyan; _trails[i] = trail;
                ParticleSystem ps = fx.AddComponent<ParticleSystem>(); var emission = ps.emission; emission.rateOverTime = 0f; var main = ps.main; main.startLifetime = 0.45f; main.startSpeed = 1.2f; main.startSize = 0.28f; main.startColor = new Color(0.65f, 0.7f, 0.75f, 0.35f); _smoke[i] = ps;
            }
        }

        private void UpdateFx(bool drifting)
        { float slip = Mathf.Clamp01(Mathf.Abs(_car.LateralSpeed) / 12f); for (int i = 0; i < 2; i++) { if (_trails[i] != null) _trails[i].emitting = drifting; if (_smoke[i] != null) { var emission = _smoke[i].emission; emission.rateOverTime = drifting ? slip * 24f : 0f; } } }

        /// <summary>Adds pickup charge while respecting the configured core capacity.</summary>
        public void AddCore(float amount) { if (_config != null) Core = Mathf.Min(_config.overclockCapacity, Core + Mathf.Max(0f, amount)); }

        /// <summary>Cancels active overclock and clears charge during a reset.</summary>
        public void ResetBoost()
        {
            Core = 0f;
            DriftBlend = 0f;
            _driftTime = 0f;
            _driftScore = 0;
            _wasDrifting = false;
            _racingBoosting = false;
            if (_overclocking) { _overclocking = false; if (_car != null && _car.IsPlayerControlled) FxEvents.RaiseOverclockEnd(); }
        }
    }
}
