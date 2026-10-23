using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheRacer
{
    [Serializable]
    public sealed class TestRoadConfig
    {
        [Min(10f)] public float width = 34f;
        [Min(30f)] public float length = 560f;
        [Min(0f)] public float bendAmplitude = 16f;
        [Min(30f)] public float bendWavelength = 210f;
        [Min(1)] public int segmentsAhead = 8;
        [Min(1)] public int segmentsBehind = 2;
        [Min(1f)] public float tileLength = 70f;
        public float centerZ = 52f;
        [Min(1f)] public float gridSpacing = 2f;
        [Min(0.005f)] public float gridThickness = 0.025f;
        [Min(1f)] public float barrierOffset = 13f;
        [Min(1f)] public float barrierSpacing = 16f;
        [Min(1f)] public float rampLength = 6f;
        [Range(0f, 25f)] public float rampAngle = 10f;
        [Tooltip("Shift the world origin after this many metres of driving to protect float precision. 0 disables it.")]
        [Min(0f)] public float originShiftDistance = 10000f;
    }

    /// <summary>Builds the temporary neon-grid driving and suspension test surface.</summary>
    public static class TestRoad
    {
        public static RollingTrack Build(GameBootstrap owner, Transform parent, TestRoadConfig config, TrackData trackData,
            Material roadMaterial, Material gridMaterial, Material obstacleMaterial, Material coneMaterial,
            Material[] zoneMaterials, Transform car)
        {
            RollingTrack rolling = parent.gameObject.AddComponent<RollingTrack>();
            rolling.Configure(owner, parent, config, trackData, roadMaterial, gridMaterial, obstacleMaterial, coneMaterial, zoneMaterials, car);
            return rolling;
        }

        public static Vector3 GetRoadPoint(float distance, float lateral, TestRoadConfig config)
        {
            float tangent = (GetCenterX(distance + 0.1f, config) - GetCenterX(distance - 0.1f, config)) / 0.2f;
            Vector3 right = new Vector3(1f, 0f, -tangent).normalized;
            return new Vector3(GetCenterX(distance, config), 0f, distance) + right * lateral;
        }

        private static float GetCenterX(float distance, TestRoadConfig config)
        {
            float wavelength = Mathf.Max(30f, config.bendWavelength);
            return config.bendAmplitude * Mathf.Sin(distance * Mathf.PI * 2f / wavelength);
        }

    }
}
