using System;
using UnityEngine;

namespace TheRacer
{
    /// <summary>Arcade vehicle handling and suspension tuning.</summary>
    [Serializable]
    public sealed class CarConfig
    {
        [Min(100f)] public float mass = 1300f;
        [Min(1f)] public float maxSpeed = 60f;
        [Min(1f)] public float reverseMaxSpeed = 15f;
        [Min(0f)] public float driveAcceleration = 8f;
        [Min(1f)] public float brakeDecel = 35f;
        [Min(0f)] public float lateralGrip = 8f;
        [Min(0f)] public float driftGrip = 1.5f;
        [Min(0f)] public float overclockCapacity = 100f;
        [Min(0f)] public float overclockDrainPerSecond = 33f;
        [Min(0f)] public float overclockForce = 18f;
        [Min(0f)] public float overclockTopSpeedMultiplier = 1.25f;
        [Min(0f)] public float driftCoreDelay = 0.8f;
        [Min(0f)] public float driftCoreRate = 100f / 6f;
        [Range(0.05f, 1f)] public float minimumPowerFraction = 0.18f;
        [Min(0.1f)] public float powerCurveExponent = 2f;
        [Min(0f)] public float downforceCoefficient = 16f;
        [Min(0f)] public float dragCoefficient = 0.00042f;
        [Min(0f)] public float rollingResistance = 1.2f;
        public Vector3 centerOfMass = new Vector3(0f, -0.4f, 0f);
        public Vector3 bodyColliderCenter = new Vector3(0f, 0.12f, 0f);
        public Vector3 bodyColliderSize = new Vector3(1.9f, 0.62f, 3.7f);
        [Min(0f)] public float suspensionSideOffset = 0.78f;
        [Min(0f)] public float suspensionAxleOffset = 1.25f;
        public float suspensionAnchorHeight = 0.05f;
        [Min(0.1f)] public float suspensionRayLength = 1.15f;
        [Min(0.1f)] public float suspensionRestLength = 0.82f;
        [Min(100f)] public float suspensionSpring = 24000f;
        [Min(0f)] public float suspensionDamper = 3200f;
        [Min(0.05f)] public float wheelRadius = 0.34f;
        [Min(0.05f)] public float wheelWidth = 0.28f;
        [Min(0f)] public float wheelSideOffset = 1.02f;
        public float wheelVisualHeight = -0.28f;
        [Min(0f)] public float wheelAxleOffset = 1.22f;
        [Range(0f, 60f)] public float maxWheelSteerAngle = 28f;
        [Min(0.1f)] public float spawnHeight = 0.9f;
        public float fallResetHeight = -50f;
        [Min(0f)] public float maxYawRateLowSpeed = 3.2f;
        [Min(0f)] public float maxYawRateHighSpeed = 0.95f;
        [Min(0f)] public float steeringResponse = 8f;
        [Min(0f)] public float airControlMultiplier = 0.22f;
        [Min(0f)] public float airUprightStrength = 8f;
        [Min(0f)] public float airAngularDamping = 1.8f;
        [Range(30f, 180f)] public float flipResetAngle = 105f;
        [Min(0f)] public float flipResetDelay = 1.25f;
        [Min(0f)] public float stuckResetDelay = 3f;
        [Min(0f)] public float impactThreshold = 8f;
        [Range(0f, 1f)] public float impactSpeedRetention = 0.62f;
    }
}
