using System.Collections.Generic;
using Asteroids.Fields;
using Game;
using UnityEngine;

namespace AI.Navigation.Field
{
    public sealed class TerminalNavFieldSnapshot
    {
        public float[] costs;
        public bool[] blocked;
        public int gridSize;
        public float cellSize;
        public Vector2 origin;
        public Vector2 source;
        public float nominalSpeed;
        public bool hasSolution;
    }

    public static class TerminalNavFieldService
    {
        private sealed class Entry
        {
            public NavField field;
            public Vector2 lastTargetPos;
            public int lastObstacleCount = -1;
            public readonly TerminalNavFieldSnapshot snapshot = new();
        }

        private static readonly Dictionary<Transform, Entry> Cache = new();
        private static readonly List<Transform> StaleTargets = new();
        private static readonly List<LiveAsteroidQueryHit> Hits = new(256);

        public static TerminalNavFieldSnapshot GetForTarget(
            Transform target,
            int gridSize,
            float cellSize,
            float nominalSpeed,
            float shipRadiusBuffer)
        {
            if (!target || gridSize < 2 || cellSize <= 0f) return null;
            Cleanup();

            if (!Cache.TryGetValue(target, out var entry))
            {
                entry = new Entry { field = new NavField(gridSize, cellSize) };
                Cache[target] = entry;
            }
            else if (entry.field.GridSize != gridSize || Mathf.Abs(entry.field.CellSize - cellSize) > 1e-4f)
            {
                entry.field = new NavField(gridSize, cellSize);
                entry.lastObstacleCount = -1;
            }

            var targetPlane = GamePlane.WorldPointToPlane(target.position);
            var halfExtent = gridSize * cellSize * 0.5f;
            var origin = SnapOrigin(targetPlane, cellSize, halfExtent);
            var halfExtents = Vector2.one * halfExtent;
            var obstacleCount = AsteroidFieldRegistry.QueryLiveAsteroidsAabb(targetPlane, halfExtents, Hits);
            var targetMoved = (targetPlane - entry.lastTargetPos).sqrMagnitude > cellSize * cellSize;

            if (entry.lastObstacleCount < 0 || targetMoved || obstacleCount != entry.lastObstacleCount ||
                (entry.field.Origin - origin).sqrMagnitude > 1e-4f)
            {
                Rebuild(entry.field, origin, targetPlane, nominalSpeed, shipRadiusBuffer);
                entry.lastTargetPos = targetPlane;
                entry.lastObstacleCount = obstacleCount;
            }

            var snapshot = entry.snapshot;
            snapshot.costs = entry.field.Costs;
            snapshot.blocked = entry.field.Blocked;
            snapshot.gridSize = entry.field.GridSize;
            snapshot.cellSize = entry.field.CellSize;
            snapshot.origin = entry.field.Origin;
            snapshot.source = targetPlane;
            snapshot.nominalSpeed = Mathf.Max(nominalSpeed, 0.01f);
            snapshot.hasSolution = entry.field.HasSolution;
            return snapshot;
        }

        private static void Rebuild(NavField field, Vector2 origin, Vector2 targetPlane, float nominalSpeed, float shipRadiusBuffer)
        {
            field.Recenter(origin);
            field.ClearObstacles();
            for (var i = 0; i < Hits.Count; i++)
            {
                var hit = Hits[i];
                field.StampObstacle(hit.planePosition, hit.radius + shipRadiusBuffer);
            }
            field.SetSource(targetPlane);
            field.Solve();
        }

        private static Vector2 SnapOrigin(Vector2 center, float cellSize, float halfExtent)
        {
            var x = Mathf.Floor(center.x / cellSize) * cellSize - halfExtent;
            var y = Mathf.Floor(center.y / cellSize) * cellSize - halfExtent;
            return new Vector2(x, y);
        }

        private static void Cleanup()
        {
            StaleTargets.Clear();
            foreach (var kvp in Cache)
                if (!kvp.Key) StaleTargets.Add(kvp.Key);
            for (var i = 0; i < StaleTargets.Count; i++)
                Cache.Remove(StaleTargets[i]);
        }
    }
}
