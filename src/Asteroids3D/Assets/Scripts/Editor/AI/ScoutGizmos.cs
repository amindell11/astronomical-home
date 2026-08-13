using AI.Scanning;
using Game;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>Scout scan envelopes in plane space: nearby-ship and asteroid-cover radius rings, the fixed worst-case obstacle query box, and a ring per detected obstacle.</summary>
    internal static class ScoutGizmos
    {
        private static readonly Color NearbyShips = new(1f, 1f, 0f, 0.2f);
        private static readonly Color AsteroidCover = new(0f, 1f, 1f, 0.2f);
        private static readonly Color QueryBox = new(1f, 0.75f, 0f, 0.15f);
        private static readonly Vector3 PlaneNormal = GamePlane.Rotation * Vector3.forward;

        [DrawGizmo(GizmoType.Selected, typeof(Scout))]
        private static void Draw(Scout scout, GizmoType gizmoType)
        {
            if (!Application.isPlaying || scout.obstacleScanner == null) return;

            var pos = GamePlane.WorldPointToPlane(scout.transform.position);
            Ring(pos, scout.nearbyShipRadius, NearbyShips);
            Ring(pos, scout.asteroidCoverRadius, AsteroidCover);
            DrawObstacles(scout.obstacleScanner, pos);
        }

        private static void DrawObstacles(ObstacleScanner scanner, Vector2 pos)
        {
            var extent = scanner.HalfExtent;
            Rect(pos, new Vector2(extent * 2f, extent * 2f), QueryBox);
            for (var i = 0; i < scanner.DetectedCount; i++)
            {
                var obstacle = scanner.DetectedBuffer[i];
                Ring(obstacle.position, obstacle.radius, Color.white);
            }
        }

        private static void Rect(Vector2 center, Vector2 size, Color color)
        {
            var half = size * 0.5f;
            var bl = center + new Vector2(-half.x, -half.y);
            var br = center + new Vector2(half.x, -half.y);
            var tr = center + new Vector2(half.x, half.y);
            var tl = center + new Vector2(-half.x, half.y);
            Line(bl, br, color);
            Line(br, tr, color);
            Line(tr, tl, color);
            Line(tl, bl, color);
        }

        private static void Ring(Vector2 center, float radius, Color color)
        {
            Handles.color = color;
            Handles.DrawWireDisc(GamePlane.PlanePointToWorld(center), PlaneNormal, radius);
        }

        private static void Line(Vector2 a, Vector2 b, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(GamePlane.PlanePointToWorld(a), GamePlane.PlanePointToWorld(b));
        }
    }
}
