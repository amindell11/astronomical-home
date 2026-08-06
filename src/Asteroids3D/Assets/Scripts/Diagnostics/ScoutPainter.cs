using System.Collections.Generic;
using AI;
using AI.Scanning;
using Ships;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>Scout scan envelopes in plane-space: nearby-ship and asteroid-cover radius rings, the fixed worst-case obstacle query box, and a ring per detected obstacle. Scouts are cached at construction.</summary>
    public sealed class ScoutPainter : IDiagnosticPainter
    {
        private static readonly Color NearbyShips = new(1f, 1f, 0f, 0.2f);
        private static readonly Color AsteroidCover = new(0f, 1f, 1f, 0.2f);
        private static readonly Color QueryBox = new(1f, 0.75f, 0f, 0.15f);

        private readonly List<Scout> scouts = new();

        public ScoutPainter(Ship a, Ship b)
        {
            Cache(a);
            Cache(b);
        }

        public string Name => DiagnosticPainters.ScoutScan;

        public void Paint(IDiagnosticCanvas canvas)
        {
            foreach (var scout in scouts) Draw(canvas, scout);
        }

        private void Cache(Ship ship)
        {
            if (!ship) return;
            var scout = ship.GetComponentInChildren<Scout>();
            if (scout) scouts.Add(scout);
        }

        public static void Draw(IDiagnosticCanvas canvas, Scout scout)
        {
            if (scout.obstacleScanner == null) return;
            var pos = GamePlane.WorldPointToPlane(scout.transform.position);
            canvas.Ring(pos, scout.nearbyShipRadius, NearbyShips);
            canvas.Ring(pos, scout.asteroidCoverRadius, AsteroidCover);
            DrawObstacles(canvas, scout.obstacleScanner, pos);
        }

        private static void DrawObstacles(IDiagnosticCanvas canvas, ObstacleScanner scanner, Vector2 pos)
        {
            var extent = scanner.HalfExtent;
            canvas.Rect(pos, new Vector2(extent * 2f, extent * 2f), QueryBox);
            for (var i = 0; i < scanner.DetectedCount; i++)
            {
                var obstacle = scanner.DetectedBuffer[i];
                canvas.Ring(obstacle.position, obstacle.radius, Color.white);
            }
        }
    }
}
