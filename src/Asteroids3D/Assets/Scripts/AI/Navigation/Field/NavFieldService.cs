using System.Collections.Generic;
using AI.Scanning;
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
    /// one bakes; buffers swap on job completion. Obstacles come from Track B2's deterministic
    /// field query (<see cref="IObstacleField"/>, handed in by the Navigator) — live state, so
    /// destroyed asteroids drop out immediately, with no physics scan and no
    /// <c>FindObjectsByType</c>. Re-solves when the target moves more than a cell, the live
    /// obstacle count drifts, or the field goes stale.
    /// Created lazily on first query; consumers hold no per-frame component lookups (the
    /// Navigator asks through the static <see cref="Instance"/>, a cached reference).
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
        [Tooltip("Rebuild when the live obstacle count changed by at least this much.")]
        [SerializeField] private int registryDeltaThreshold = 3;
        [Tooltip("Rebuild at least this often regardless of motion (drifting rocks).")]
        [SerializeField] private float maxStaleness = 1f;

        // Hard ceiling on obstacles gathered per rebuild (dense-field guard).
        private const int MaxObstacles = 8192;

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
            public NativeArray<float3> obstacles;   // packed (x, y, inflatedRadius)
            public DetectedObstacle[] scratch;      // reused query buffer
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
        /// (first frames) — the caller's terminal hook then contributes 0. Kicks the rebuild
        /// machinery as a side effect. <paramref name="field"/> is B2's live obstacle source
        /// (may be null between sectors, in which case the field bakes with no obstacles).
        /// </summary>
        public bool TryGetData(Transform target, float2 targetPlanePos, float nominalSpeed,
            IObstacleField field, out TerminalFieldData data)
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
                    scratch = new DetectedObstacle[256],
                };
                fields[target] = entry;
            }

            EnsureFresh(entry, targetPlanePos, field);

            if (!entry.front.HasSolution) return false;
            data = entry.front.Data(nominalSpeed);
            return true;
        }

        private void EnsureFresh(Entry entry, float2 goal, IObstacleField field)
        {
            if (entry.jobRunning) return;

            var now = Time.time;
            var neverBuilt = !entry.front.HasSolution && entry.lastBuildTime <= 0f;
            var dueByTimer = now - entry.lastBuildTime >= minRebuildInterval;
            if (!neverBuilt && !dueByTimer) return;

            var count = GatherObstacles(entry, goal, field);

            var moved = math.distancesq(entry.lastGoal, goal) > cellSize * cellSize;
            var delta = math.abs(count - entry.lastObstacleCount) >= registryDeltaThreshold;
            var staleTimer = now - entry.lastBuildTime >= maxStaleness;
            if (!neverBuilt && !moved && !delta && !staleTimer) return;

            entry.pending = entry.back.ScheduleSolve(goal, entry.obstacles, count);
            entry.jobRunning = true;
            entry.lastBuildTime = now;
            entry.lastGoal = goal;
            entry.lastObstacleCount = count;
        }

        /// <summary>
        /// Query B2's field for every live asteroid inside the grid AABB around the target and
        /// pack them (position + inflated radius) into the entry's native buffer. Grows the
        /// scratch/native buffers on truncation up to <see cref="MaxObstacles"/>.
        /// </summary>
        private int GatherObstacles(Entry entry, float2 goal, IObstacleField field)
        {
            if (field == null) return 0;

            var center = new Vector2(goal.x, goal.y);
            var halfExtent = gridSize * cellSize * 0.5f;

            var n = field.QueryObstacles(center, halfExtent, entry.scratch);
            // Filled the buffer exactly ⇒ likely truncated; grow and re-query (dense fields).
            while (n == entry.scratch.Length && entry.scratch.Length < MaxObstacles)
            {
                entry.scratch = new DetectedObstacle[entry.scratch.Length * 2];
                n = field.QueryObstacles(center, halfExtent, entry.scratch);
            }

            if (n > entry.obstacles.Length)
            {
                var cap = entry.obstacles.Length;
                while (cap < n) cap *= 2;
                if (entry.obstacles.IsCreated) entry.obstacles.Dispose();
                entry.obstacles = new NativeArray<float3>(cap, Allocator.Persistent);
            }

            for (var i = 0; i < n; i++)
            {
                var d = entry.scratch[i];
                entry.obstacles[i] = new float3(d.position.x, d.position.y, d.radius + shipRadiusBuffer);
            }
            return n;
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
