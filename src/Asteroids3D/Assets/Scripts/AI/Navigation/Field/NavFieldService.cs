using System.Collections.Generic;
using Asteroids.Spawning;
using Game;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Movement.MPC.Field
{
    /// <summary>
    /// Runtime service maintaining one cost-to-go <see cref="NavField"/> per chase target,
    /// shared by every pursuer of that target. Rebuilds run as Burst jobs off the main thread
    /// against a double buffer: the solver keeps sampling the last solved field while the next
    /// one bakes; buffers swap on job completion. Obstacles come from the live asteroid spawn
    /// registry (destroyed asteroids leave immediately) — no physics queries, no
    /// FindObjectsByType. Re-solves when the target moves more than a cell, the live asteroid
    /// count drifts, or the field goes stale.
    /// Created lazily on first query; consumers hold no per-frame lookups (the Navigator asks
    /// through the static <see cref="Instance"/>, which is a cached reference).
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public partial class NavFieldService : MonoBehaviour
    {
        [Header("Grid")]
        [Tooltip("Cells per side. Extent = gridSize * cellSize, centered on the target.")]
        [SerializeField] private int gridSize = 64;
        [Tooltip("Cell size in plane units (~ship length to half a typical gap).")]
        [SerializeField] private float cellSize = 3f;

        [Header("Obstacle inflation")]
        [Tooltip("Added to every asteroid radius before stamping (ship radius + margin).")]
        [SerializeField] private float shipRadiusBuffer = 2f;

        [Header("Rebuild policy")]
        [Tooltip("Minimum interval between rebuilds of one target's field.")]
        [SerializeField] private float minRebuildInterval = 0.15f;
        [Tooltip("Rebuild when the live asteroid count changed by at least this much.")]
        [SerializeField] private int registryDeltaThreshold = 3;
        [Tooltip("Rebuild at least this often regardless of motion (drifting rocks).")]
        [SerializeField] private float maxStaleness = 1f;

        private static NavFieldService instance;

        /// <summary>Lazily-created scene singleton (no scene authoring required).</summary>
        public static NavFieldService Instance
        {
            get
            {
                if (instance) return instance;
                var go = new GameObject("[NavFieldService]");
                instance = go.AddComponent<NavFieldService>();
                return instance;
            }
        }

        /// <summary>True when a live instance exists (query without creating one).</summary>
        public static bool HasInstance => instance;

        private sealed class Entry
        {
            public NavField front;
            public NavField back;
            public NativeArray<float3> obstacles;
            public JobHandle pending;
            public bool jobRunning;
            public float lastBuildTime;
            public float2 lastGoal;
            public int lastObstacleCount;
        }

        private readonly Dictionary<Transform, Entry> fields = new();
        private readonly List<Transform> stale = new();

        private void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            foreach (var e in fields.Values) DisposeEntry(e);
            fields.Clear();
            if (instance == this) instance = null;
        }

        private static void DisposeEntry(Entry e)
        {
            e.pending.Complete();
            e.front?.Dispose();
            e.back?.Dispose();
            if (e.obstacles.IsCreated) e.obstacles.Dispose();
        }

        /// <summary>
        /// Sampling view of the target's field. Returns false while nothing is solved yet
        /// (first frames) — the caller's terminal hook then contributes 0. Kicks the
        /// rebuild machinery as a side effect.
        /// </summary>
        public bool TryGetData(Transform target, float2 targetPlanePos, float nominalSpeed,
            out TerminalFieldData data)
        {
            data = default;
            if (!target) return false;

            if (!fields.TryGetValue(target, out var entry))
            {
                entry = new Entry
                {
                    front = new NavField(gridSize, cellSize),
                    back = new NavField(gridSize, cellSize),
                    obstacles = new NativeArray<float3>(256, Allocator.Persistent),
                };
                fields[target] = entry;
            }

            EnsureFresh(entry, targetPlanePos);

            if (!entry.front.HasSolution) return false;
            data = entry.front.Data(nominalSpeed);
            return true;
        }

        private void EnsureFresh(Entry entry, float2 goal)
        {
            if (entry.jobRunning) return;

            var now = Time.time;
            var obstacleCount = LiveAsteroidCount();
            var neverBuilt = !entry.front.HasSolution && entry.lastBuildTime <= 0f;
            var dueByTimer = now - entry.lastBuildTime >= minRebuildInterval;
            if (!neverBuilt && !dueByTimer) return;

            var moved = math.distancesq(entry.lastGoal, goal) > cellSize * cellSize;
            var delta = math.abs(obstacleCount - entry.lastObstacleCount) >= registryDeltaThreshold;
            var staleTimer = now - entry.lastBuildTime >= maxStaleness;
            if (!neverBuilt && !moved && !delta && !staleTimer) return;

            var count = GatherObstacles(entry);
            entry.pending = entry.back.ScheduleSolve(goal, entry.obstacles, count);
            entry.jobRunning = true;
            entry.lastBuildTime = now;
            entry.lastGoal = goal;
            entry.lastObstacleCount = obstacleCount;
        }

        private static int LiveAsteroidCount()
        {
            var total = 0;
            var spawners = AsteroidSpawner.ActiveSpawners;
            for (var i = 0; i < spawners.Count; i++)
                total += spawners[i].ActiveCount;
            return total;
        }

        private int GatherObstacles(Entry entry)
        {
            var count = 0;
            var spawners = AsteroidSpawner.ActiveSpawners;
            for (var s = 0; s < spawners.Count; s++)
            {
                var live = spawners[s].LiveAsteroids;
                if (live == null) continue;
                foreach (var ast in live)
                {
                    if (!ast) continue;
                    if (count >= entry.obstacles.Length) Grow(entry);
                    var pos = GamePlane.WorldPointToPlane(ast.transform.position);
                    entry.obstacles[count++] = new float3(pos.x, pos.y, ast.Radius + shipRadiusBuffer);
                }
            }
            return count;
        }

        private static void Grow(Entry entry)
        {
            var bigger = new NativeArray<float3>(entry.obstacles.Length * 2, Allocator.Persistent);
            NativeArray<float3>.Copy(entry.obstacles, bigger, entry.obstacles.Length);
            entry.obstacles.Dispose();
            entry.obstacles = bigger;
        }

        private void Update()
        {
            // Swap buffers for completed bakes; drop entries whose target died.
            stale.Clear();
            foreach (var kvp in fields)
            {
                var entry = kvp.Value;
                if (entry.jobRunning && entry.pending.IsCompleted)
                {
                    entry.pending.Complete();
                    entry.back.MarkSolved();
                    (entry.front, entry.back) = (entry.back, entry.front);
                    entry.jobRunning = false;
                }
                if (!kvp.Key) stale.Add(kvp.Key);
            }
            foreach (var key in stale)
            {
                DisposeEntry(fields[key]);
                fields.Remove(key);
            }
        }
    }
}
