using System;
using UnityEngine;

namespace TheRacer
{
    /// <summary>Turns the shared collision event into health, cosmetic flicker and pooled sparks.</summary>
    public sealed class CarDamage : MonoBehaviour
    {
        [SerializeField, Range(0f, 100f)] private float health = 100f;
        [SerializeField, Min(0f)] private float impactDamageScale = 0.9f;
        [SerializeField, Min(0f)] private float flickerDuration = 1.4f;
        [SerializeField, Min(0f)] private float flickerFrequency = 24f;

        private CarVisuals _visuals;
        private ParticleSystem _sparks;
        private float _flickerUntil;
        private bool _listening;
        private CarController _car;

        /// <summary>Current cosmetic health between zero and one hundred.</summary>
        public float Health => health;

        /// <summary>Damage reduction reserved for later durability upgrades.</summary>
        public float DurabilityMultiplier { get; set; } = 1f;

        /// <summary>Invoked with remaining health whenever damage is applied.</summary>
        public event Action<float> OnDamaged;

        public void Configure(CarVisuals visuals)
        {
            _visuals = visuals;
            _car = GetComponent<CarController>();
            CreateSparkPool();
            _visuals?.ApplyDamage(health / 100f);
        }

        private void OnEnable()
        {
            FxEvents.OnCarImpact += HandleImpact;
            _listening = true;
        }

        private void OnDisable()
        {
            if (_listening)
            {
                FxEvents.OnCarImpact -= HandleImpact;
                _listening = false;
            }
        }

        private void Update()
        {
            if (_visuals == null)
                return;
            float flicker = health < 100f && Time.time < _flickerUntil
                ? (Mathf.Sin(Time.time * flickerFrequency) > 0.05f ? 1f : 0.18f) : 1f;
            _visuals.SetDamageFlicker(flicker);
        }

        /// <summary>Applies explicit damage and refreshes the visible damage state.</summary>
        public void TakeDamage(float amount)
        {
            if (amount <= 0f || health <= 0f)
                return;
            health = Mathf.Max(0f, health - amount / Mathf.Max(0.1f, DurabilityMultiplier));
            _flickerUntil = Time.time + flickerDuration;
            _visuals?.ApplyDamage(health / 100f);
            if (_sparks != null)
                _sparks.Emit(Mathf.Clamp(Mathf.RoundToInt(amount * 0.5f), 4, 32));
            OnDamaged?.Invoke(health);
        }

        private void HandleImpact(CarController car, float magnitude)
        {
            if (car != _car) return;
            TakeDamage(magnitude * impactDamageScale);
        }

        private void CreateSparkPool()
        {
            GameObject sparkObject = new GameObject(ThemeText.SparkPoolName);
            sparkObject.transform.SetParent(transform, false);
            sparkObject.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            _sparks = sparkObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _sparks.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 48;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.085f);
            main.startColor = ThemePalette.AlertRed;
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = _sparks.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.16f;
            ParticleSystemRenderer renderer = sparkObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = NeonMaterialFactory.NeonBase;
        }
    }
}
