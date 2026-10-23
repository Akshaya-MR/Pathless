using UnityEngine;

namespace TheRacer
{
    /// <summary>Reusable authored piece for the endless track window.</summary>
    public sealed class TrackSegment : MonoBehaviour
    {
        [SerializeField, Tooltip("Exact entry anchor used to align this piece.")] private Transform startAnchor;
        [SerializeField, Tooltip("Exact exit anchor used to align the next piece.")] private Transform endAnchor;
        [SerializeField, Tooltip("Marks ramps so obstacles and curve bias can avoid them.")] private bool ramp;
        [SerializeField, Min(0f), Tooltip("Distance after this ramp's end where obstacles remain disabled. Carries over into following segments.")] private float landingNoSpawnDistance = 18f;
        [SerializeField, Min(0.5f), Tooltip("Half of the drivable width of this segment in metres. Obstacles are placed sideways using this.")] private float halfWidth = 5f;

        public Transform StartAnchor => startAnchor;
        public Transform EndAnchor => endAnchor;
        public bool IsRamp => ramp;
        public float LandingNoSpawnDistance => landingNoSpawnDistance;
        public float HalfWidth => halfWidth;

        /// <summary>Distance from this segment's start where obstacles must not spawn (set by TrackGenerator).</summary>
        public float NoObstacleDistance { get; private set; }

        public float Length => startAnchor != null && endAnchor != null ? Vector3.Distance(startAnchor.position, endAnchor.position) : 0f;

        public void SetNoObstacleDistance(float distance)
        {
            NoObstacleDistance = Mathf.Max(0f, distance);
        }

        private void OnDrawGizmosSelected()
        {
            if (startAnchor == null || endAnchor == null) return;
            Gizmos.color = ThemePalette.NeonCyan;
            Gizmos.DrawLine(startAnchor.position, endAnchor.position);
            Gizmos.DrawWireSphere(startAnchor.position, 0.25f);
            Gizmos.color = ThemePalette.NeonMagenta;
            Gizmos.DrawWireSphere(endAnchor.position, 0.25f);
        }
    }
}
