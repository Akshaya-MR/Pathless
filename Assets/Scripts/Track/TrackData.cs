using System;
using UnityEngine;

namespace TheRacer
{
    public enum TrackZone
    {
        NeonStrip,
        Docks,
        Undercity,
        CorpSpire
    }

    /// <summary>Distance and width profile for the temporary straight sprint road.</summary>
    [Serializable]
    public sealed class TrackData
    {
        [Min(1f)] public float lapLength = 140f;
        [Range(0.1f, 0.4f)] public float firstSplit = 0.25f;
        [Range(0.2f, 0.7f)] public float secondSplit = 0.5f;
        [Range(0.6f, 0.9f)] public float thirdSplit = 0.75f;
        [Min(0f)] public float blendDistance = 8f;
        [Min(1f)] public float neonStripWidth = 34f;
        [Min(1f)] public float docksWidth = 24f;
        [Min(1f)] public float undercityWidth = 11f;
        [Min(1f)] public float corpSpireWidth = 17f;

        public float FirstBoundary => Mathf.Clamp(firstSplit, 0.05f, 0.45f) * lapLength;
        public float SecondBoundary => Mathf.Clamp(secondSplit, firstSplit + 0.05f, 0.75f) * lapLength;
        public float ThirdBoundary => Mathf.Clamp(thirdSplit, secondSplit + 0.05f, 0.95f) * lapLength;

        public void Configure(float length, float baseWidth)
        {
            lapLength = Mathf.Max(1f, length);
            neonStripWidth = Mathf.Max(1f, baseWidth);
        }

        public TrackZone GetZone(float distance)
        {
            float d = Mathf.Clamp(distance, 0f, lapLength);
            if (d < FirstBoundary) return TrackZone.NeonStrip;
            if (d < SecondBoundary) return TrackZone.Docks;
            if (d < ThirdBoundary) return TrackZone.Undercity;
            return TrackZone.CorpSpire;
        }

        public float GetWidth(float distance)
        {
            float d = Mathf.Clamp(distance, 0f, lapLength);
            float width = neonStripWidth;
            width = BlendWidth(d, FirstBoundary, width, docksWidth);
            width = BlendWidth(d, SecondBoundary, width, undercityWidth);
            return BlendWidth(d, ThirdBoundary, width, corpSpireWidth);
        }

        public float GetDistance(Vector3 worldPosition, float roadStartZ)
        {
            return Mathf.Clamp(worldPosition.z - roadStartZ, 0f, lapLength);
        }

        private float BlendWidth(float distance, float boundary, float from, float to)
        {
            if (blendDistance <= 0f)
                return distance < boundary ? from : to;
            float blend = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(boundary - blendDistance, boundary + blendDistance, distance));
            return Mathf.Lerp(from, to, blend);
        }
    }
}
