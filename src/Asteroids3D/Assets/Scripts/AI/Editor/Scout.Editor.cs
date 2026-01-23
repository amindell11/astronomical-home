#if UNITY_EDITOR
using UnityEngine;

namespace AI.Scanning
{
    public partial class Scout
    {
        [Header("Debug")]
        [Tooltip("Show debug gizmos in scene view")]
        public bool showDebugGizmos = true;

        void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying || obstacleScanner == null) return;

            var pos = transform.position;

            // Nearby ship radius (yellow)
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(pos, nearbyShipRadius);

            // Asteroid cover radius (cyan)
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(pos, asteroidCoverRadius);
            
            DrawObstacleGizmos();
        }
        
        private void DrawObstacleGizmos()
        {
            if (obstacleScanner == null) return;
            
            // Draw raycast fan from scanner's debug rays (orange)
            Gizmos.color = new Color(1f, 0.75f, 0f, 0.5f);
            var radius = obstacleScanner.LastSphereRadius;
            foreach (var ray in obstacleScanner.DebugRays)
            {
                Gizmos.DrawLine(transform.position, transform.position + ray);
                if (radius > 0)
                    Gizmos.DrawWireSphere(transform.position + ray, radius);
            }

            // Draw white circles around detected obstacles
            if (obstacleScanner.DetectedCount <= 0) return;
            
            Gizmos.color = Color.white;
            for (var i = 0; i < obstacleScanner.DetectedCount; i++)
            {
                var obstacle = obstacleScanner.DetectedBuffer[i];
                var p = obstacle.collider.transform.position;
                Gizmos.DrawWireSphere(p, obstacle.radius);
            }
        }
    }
}
#endif
