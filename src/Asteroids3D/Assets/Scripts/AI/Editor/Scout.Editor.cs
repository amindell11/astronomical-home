#if UNITY_EDITOR
using AI.Debug;
using UnityEngine;

namespace AI.Scanning
{
    public partial class Scout
    {
        private AICommander cachedCommander;
        private AIDebugSettings CachedSettings
        {
            get
            {
                if (!cachedCommander)
                    cachedCommander = GetComponent<AICommander>();
                return cachedCommander ? cachedCommander.DebugSettings : null;
            }
        }

        void OnDrawGizmos() => DrawGizmosImpl(false);
        void OnDrawGizmosSelected() => DrawGizmosImpl(true);

        void DrawGizmosImpl(bool isSelected)
        {
            var settings = CachedSettings;
            if (settings == null || !settings.ShouldDraw(isSelected)) return;
            if (!settings.IsActive(AIDebugChannel.Scanning)) return;
            if (!Application.isPlaying || obstacleScanner == null) return;

            var pos = transform.position;

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(pos, nearbyShipRadius);

            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(pos, asteroidCoverRadius);

            DrawObstacleGizmos();
        }

        private void DrawObstacleGizmos()
        {
            if (obstacleScanner == null) return;

            if (obstacleScanner is SphereObstacleScanner sphere)
            {
                Gizmos.color = new Color(1f, 0.75f, 0f, 0.15f);
                Gizmos.DrawWireSphere(transform.position, sphere.Radius);
            }
            else
            {
                Gizmos.color = new Color(1f, 0.75f, 0f, 0.5f);
                var radius = obstacleScanner.LastSphereRadius;
                foreach (var ray in obstacleScanner.DebugRays)
                {
                    Gizmos.DrawLine(transform.position, transform.position + ray);
                    if (radius > 0)
                        Gizmos.DrawWireSphere(transform.position + ray, radius);
                }
            }

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
