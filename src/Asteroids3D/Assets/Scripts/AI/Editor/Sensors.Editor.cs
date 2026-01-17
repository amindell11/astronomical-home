#if UNITY_EDITOR
using UnityEngine;

namespace AI
{
    public partial class Sensors
    {
        [Header("Debug")]
        [Tooltip("Show debug gizmos in scene view")]
        public bool showDebugGizmos = true;

        void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying || ShipScanner == null) return;

            var pos = transform.position;

            // Nearby ship radius (yellow)
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(pos, nearbyShipRadius);

            // Asteroid cover radius (cyan)
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(pos, asteroidCoverRadius);

            // Draw obstacle scanner debug rays
            if (ObstacleScanner != null)
            {
                Gizmos.color = new Color(1f, 0.75f, 0f, 0.5f);
                foreach (var ray in ObstacleScanner.DebugRays)
                {
                    Gizmos.DrawLine(pos, pos + ray);
                    if (sphereCastRadius > 0)
                        Gizmos.DrawWireSphere(pos + ray, sphereCastRadius);
                }
            }
        }
    }
}
#endif
