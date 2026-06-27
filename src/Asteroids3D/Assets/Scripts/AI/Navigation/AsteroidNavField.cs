using System.Collections.Generic;
using Asteroids.Spawning;
using Game;
using Ships;
using UnityEngine;

namespace AI.Planning
{
    /// <summary>
    /// Scene-level service that maintains 2D Dijkstra flow-fields over the asteroid layout
    /// for high-level chase/evade routing. One field per pursued target ship; multiple AIs
    /// share a field via the gradient queries.
    /// </summary>
    [DefaultExecutionOrder(-75)]
    public partial class AsteroidNavField : MonoBehaviour
    {
        [Header("Scope")]
        [Tooltip("Transform whose position the grid follows (typically WorldFollowerTransform).")]
        [SerializeField] private Transform anchor;
        [Tooltip("Source of asteroid positions/sizes. Optional — null disables asteroid stamping.")]
        [SerializeField] private AsteroidSpawner asteroidSpawner;

        [Header("Grid")]
        [SerializeField] private int gridSize = 50;
        [SerializeField] private float cellSize = 4f;

        [Header("Obstacle Inflation")]
        [Tooltip("Ship radius + safety buffer, added to every obstacle's radius before stamping.")]
        [SerializeField] private float shipRadiusBuffer = 4f;
        [Tooltip("Whether to also stamp other ships (excluding the target) as obstacles.")]
        [SerializeField] private bool includeShipsAsObstacles = true;

        [Header("Replan")]
        [Tooltip("Minimum interval between rebuilds of a given target's field.")]
        [SerializeField] private float replanInterval = 0.2f;
        [Tooltip("Rebuild if the obstacle count has changed by this many since last rebuild.")]
        [SerializeField] private int registryDeltaThreshold = 5;

        [Header("Routing")]
        [Tooltip("How many cells to walk along the gradient when computing a routing point.")]
        [SerializeField] private int routingStepsAhead = 6;

        [Header("Diagnostics")]
        [Tooltip("Log every call to TryGetRoutedPlanePos and every field rebuild. Spammy — enable temporarily.")]
        [SerializeField] private bool verboseLogging;

        /// <summary>
        /// The active scene-level instance. Set on Awake, cleared on OnDestroy.
        /// AI consumers (Info.NavPlanner) read this lazily so init ordering doesn't matter.
        /// </summary>
        public static AsteroidNavField Active { get; private set; }

        private readonly Dictionary<Ship, FieldEntry> cache = new();
        private readonly List<Ship> staleKeys = new();

        public int GridSize => gridSize;
        public float CellSize => cellSize;
        public Transform Anchor => anchor;
        public int CachedTargetCount => cache.Count;

        private void Awake()
        {
            if (Active && Active != this)
                UnityEngine.Debug.LogWarning($"[AsteroidNavField] Multiple instances; replacing existing '{Active.name}' with '{name}'", this);
            Active = this;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        public void SetAnchor(Transform t) => anchor = t;
        public void SetAsteroidSpawner(AsteroidSpawner s) => asteroidSpawner = s;

        /// <summary>
        /// Compute (or reuse cached) routing target for an AI chasing or evading the given target.
        /// Returns true with a 2D plane position if a useful route exists; false if the planner
        /// cannot help (no anchor, no target, off-grid, etc.) — caller should fall back to direct.
        /// </summary>
        public bool TryGetRoutedPlanePos(Vector2 shipPlanePos, Ship target, RoutingMode mode, out Vector2 routedPlanePos)
        {
            routedPlanePos = shipPlanePos;
            if (!anchor)
            {
                if (verboseLogging) UnityEngine.Debug.LogWarning("[AsteroidNavField] TryGetRouted: anchor is null", this);
                return false;
            }
            if (!target)
            {
                if (verboseLogging) UnityEngine.Debug.LogWarning("[AsteroidNavField] TryGetRouted: target is null", this);
                return false;
            }
            if (!GamePlane.IsConfigured)
            {
                if (verboseLogging) UnityEngine.Debug.LogWarning("[AsteroidNavField] TryGetRouted: GamePlane not configured", this);
                return false;
            }

            var entry = GetOrCreateEntry(target);
            EnsureFresh(entry, target);
            if (entry.field == null || !entry.field.HasSolution)
            {
                if (verboseLogging) UnityEngine.Debug.LogWarning($"[AsteroidNavField] TryGetRouted: no solution for target '{target.name}' (mode={mode})", this);
                return false;
            }

            routedPlanePos = entry.field.RoutedCell(shipPlanePos, routingStepsAhead, mode);
            if (verboseLogging) UnityEngine.Debug.Log($"[AsteroidNavField] TryGetRouted ok: ship={shipPlanePos} target='{target.name}' mode={mode} → routed={routedPlanePos} (cache={cache.Count})", this);
            return true;
        }

        /// <summary>World-space convenience overload of <see cref="TryGetRoutedPlanePos"/>.</summary>
        public bool TryGetRoutedTarget(Vector3 shipWorldPos, Ship target, RoutingMode mode, out Vector3 routedWorldPos)
        {
            routedWorldPos = shipWorldPos;
            if (!GamePlane.IsConfigured) return false;
            var shipPlane = GamePlane.WorldPointToPlane(shipWorldPos);
            if (!TryGetRoutedPlanePos(shipPlane, target, mode, out var routedPlane)) return false;
            routedWorldPos = GamePlane.PlanePointToWorld(routedPlane);
            return true;
        }

        /// <summary>Read-only access for the gizmo / inspector.</summary>
        public bool TryGetField(Ship target, out NavField field)
        {
            field = null;
            if (!target || !cache.TryGetValue(target, out var entry)) return false;
            field = entry.field;
            return field != null;
        }

        public IEnumerable<KeyValuePair<Ship, NavField>> CachedFields()
        {
            foreach (var kvp in cache)
            {
                if (kvp.Value?.field != null)
                    yield return new KeyValuePair<Ship, NavField>(kvp.Key, kvp.Value.field);
            }
        }

        private void Update()
        {
            CleanUpDeadTargets();
        }

        private void CleanUpDeadTargets()
        {
            staleKeys.Clear();
            foreach (var kvp in cache)
            {
                if (!kvp.Key) staleKeys.Add(kvp.Key);
            }
            foreach (var k in staleKeys) cache.Remove(k);
        }

        private FieldEntry GetOrCreateEntry(Ship target)
        {
            if (cache.TryGetValue(target, out var entry)) return entry;
            entry = new FieldEntry { field = new NavField(gridSize, cellSize) };
            cache[target] = entry;
            if (verboseLogging) UnityEngine.Debug.Log($"[AsteroidNavField] cache MISS: created new entry for '{target.name}' (cache={cache.Count})", this);
            return entry;
        }

        private void EnsureFresh(FieldEntry entry, Ship target)
        {
            var now = Time.time;
            var asteroidCount = asteroidSpawner ? asteroidSpawner.Registry.ActiveCount : 0;
            var targetPlane = GamePlane.WorldPointToPlane(target.transform.position);

            var dueByTimer = now - entry.lastBuildTime >= replanInterval;
            var sourceMoved = (entry.lastSourceCellCenter - targetPlane).sqrMagnitude > cellSize * cellSize;
            var deltaTooBig = Mathf.Abs(asteroidCount - entry.lastObstacleCount) >= registryDeltaThreshold;

            if (entry.lastBuildTime > 0f && !dueByTimer && !sourceMoved && !deltaTooBig) return;

            RebuildField(entry, target, targetPlane);
            entry.lastBuildTime = now;
            entry.lastSourceCellCenter = targetPlane;
            entry.lastObstacleCount = asteroidCount;
        }

        private void RebuildField(FieldEntry entry, Ship target, Vector2 targetPlane)
        {
            var anchorPlane = GamePlane.WorldPointToPlane(anchor.position);
            var halfExtent = gridSize * cellSize * 0.5f;
            // Snap to cell-aligned positions so the grid only translates in whole-cell jumps —
            // eliminates per-frame gizmo jitter and lets future incremental rebuilds key off
            // a stable origin (only the cells that just entered the window need re-stamping).
            var snappedAnchorX = Mathf.Floor(anchorPlane.x / cellSize) * cellSize;
            var snappedAnchorY = Mathf.Floor(anchorPlane.y / cellSize) * cellSize;
            var origin = new Vector2(snappedAnchorX - halfExtent, snappedAnchorY - halfExtent);
            var asteroidCount = asteroidSpawner ? asteroidSpawner.Registry.ActiveCount : 0;
            if (verboseLogging) UnityEngine.Debug.Log($"[AsteroidNavField] REBUILD field for '{target.name}' source={targetPlane} origin={origin} asteroids={asteroidCount}", this);

            var field = entry.field;
            field.Recenter(origin);
            field.ClearObstacles();
            StampAsteroids(field);
            if (includeShipsAsObstacles) StampOtherShips(field, target);
            field.SetSource(targetPlane);
            field.Solve();
        }

        private void StampAsteroids(NavField field)
        {
            if (!asteroidSpawner) return;
            foreach (var ast in asteroidSpawner.Registry.ActiveAsteroids)
            {
                if (!ast) continue;
                var pos = GamePlane.WorldPointToPlane(ast.transform.position);
                var radius = ast.Radius + shipRadiusBuffer;
                field.StampObstacle(pos, radius);
            }
        }

        private void StampOtherShips(NavField field, Ship target)
        {
            // 5Hz rebuild — FindObjects is fine at this cadence with a handful of ships in scene.
            var ships = Object.FindObjectsByType<Ship>(FindObjectsSortMode.None);
            foreach (var s in ships)
            {
                if (!s || s == target) continue;
                var pos = GamePlane.WorldPointToPlane(s.transform.position);
                field.StampObstacle(pos, shipRadiusBuffer);
            }
        }

        private class FieldEntry
        {
            public NavField field;
            public float lastBuildTime;
            public Vector2 lastSourceCellCenter;
            public int lastObstacleCount;
        }
    }
}
