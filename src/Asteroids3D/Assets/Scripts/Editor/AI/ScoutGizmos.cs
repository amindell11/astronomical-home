using AI.Debug;
using UnityEditor;
using UnityEngine;

namespace AI
{
    internal static class ScoutGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Scout))]
        private static void Draw(Scout scout, GizmoType gizmoType)
        {
            var isSelected = (gizmoType & GizmoType.Selected) != 0;
            if (!AIDebugContext.ShouldDraw(AIDebugChannel.Scanning, isSelected)) return;
            if (!Application.isPlaying || scout.obstacleScanner == null) return;

            var pos = scout.transform.position;

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(pos, scout.nearbyShipRadius);

            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(pos, scout.asteroidCoverRadius);

            DrawObstacles(scout, pos);
        }

        private static void DrawObstacles(Scout scout, Vector3 pos)
        {
            var scanner = scout.obstacleScanner;

            // Fixed worst-case query box (half-extent per axis) the field query fills from.
            var extent = scanner.HalfExtent;
            Gizmos.color = new Color(1f, 0.75f, 0f, 0.15f);
            Gizmos.DrawWireCube(pos, new Vector3(extent * 2f, extent * 2f, extent * 2f));

            if (scanner.DetectedCount <= 0) return;

            Gizmos.color = Color.white;
            for (var i = 0; i < scanner.DetectedCount; i++)
            {
                var obstacle = scanner.DetectedBuffer[i];
                Gizmos.DrawWireSphere(obstacle.collider.transform.position, obstacle.radius);
            }
        }
    }
}
