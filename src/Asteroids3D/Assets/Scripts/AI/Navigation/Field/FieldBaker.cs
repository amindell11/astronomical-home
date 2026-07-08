using AI.Scanning;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Movement.MPC.Field
{
    /// <summary>
    /// Keeps one cost-to-go <see cref="NavField"/> continuously baked against a live obstacle
    /// source: solves run as Burst jobs against a double buffer (consumers keep sampling the
    /// last solved field while the next bakes; buffers swap on <see cref="Pump"/>), obstacles
    /// come from an <see cref="IObstacleField"/> query over the grid's AABB, and the rebuild
    /// policy decides when a fresh solve is due. Plain class — the owner pumps it every frame
    /// and disposes it. Shared by <see cref="NavFieldService"/>'s per-target registry and any
    /// component-owned field.
    /// </summary>
    public sealed class FieldBaker : System.IDisposable
    {
        public struct Policy
        {
            /// <summary>Minimum interval between rebuilds.</summary>
            public float minRebuildInterval;
            /// <summary>Rebuild when the live obstacle count changed by at least this much.</summary>
            public int registryDeltaThreshold;
            /// <summary>Rebuild at least this often regardless of motion (drifting rocks).</summary>
            public float maxStaleness;
        }

        // Hard ceiling on obstacles gathered per rebuild (dense-field guard).
        private const int MaxObstacles = 8192;

        private readonly int gridSize;
        private readonly float cellSize;
        private readonly float shipRadiusBuffer;
        private readonly Policy policy;

        private NavField front;
        private NavField back;
        private NativeArray<float3> obstacles;   // packed (x, y, inflatedRadius)
        private DetectedObstacle[] scratch;      // reused query buffer
        private JobHandle pending;
        private bool jobRunning;
        private float lastBuildTime;
        private float2 lastGoal;
        private int lastObstacleCount;

        /// <summary>Last solved field (gizmos/inspection); sampling goes via <see cref="TryGetData"/>.</summary>
        public NavField Front => front;

        public FieldBaker(int gridSize, float cellSize, float shipRadiusBuffer, Policy policy)
        {
            this.gridSize = gridSize;
            this.cellSize = cellSize;
            this.shipRadiusBuffer = shipRadiusBuffer;
            this.policy = policy;
            front = new NavField(gridSize, cellSize);
            back = new NavField(gridSize, cellSize);
            obstacles = new NativeArray<float3>(256, Allocator.Persistent);
            scratch = new DetectedObstacle[256];
        }

        public void Dispose()
        {
            pending.Complete();
            front?.Dispose();
            back?.Dispose();
            if (obstacles.IsCreated) obstacles.Dispose();
        }

        /// <summary>Swap buffers when a scheduled solve has completed. Call once per frame.</summary>
        public void Pump()
        {
            if (!jobRunning || !pending.IsCompleted) return;
            pending.Complete();
            back.MarkSolved();
            (front, back) = (back, front);
            jobRunning = false;
        }

        /// <summary>Sampling view of the last solved field; false while nothing is solved yet.</summary>
        public bool TryGetData(float nominalSpeed, out TerminalFieldData data)
        {
            if (!front.HasSolution)
            {
                data = default;
                return false;
            }
            data = front.Data(nominalSpeed);
            return true;
        }

        /// <summary>
        /// Kick a rebuild toward <paramref name="goal"/> if the policy says one is due.
        /// <paramref name="field"/> is the live obstacle source (may be null between sectors,
        /// in which case the field bakes with no obstacles).
        /// </summary>
        public void RequestBake(float2 goal, IObstacleField field)
        {
            if (jobRunning) return;

            var now = Time.time;
            var neverBuilt = !front.HasSolution && lastBuildTime <= 0f;
            var dueByTimer = now - lastBuildTime >= policy.minRebuildInterval;
            if (!neverBuilt && !dueByTimer) return;

            var count = GatherObstacles(goal, field);

            var moved = math.distancesq(lastGoal, goal) > cellSize * cellSize;
            var delta = math.abs(count - lastObstacleCount) >= policy.registryDeltaThreshold;
            var staleTimer = now - lastBuildTime >= policy.maxStaleness;
            if (!neverBuilt && !moved && !delta && !staleTimer) return;

            pending = back.ScheduleSolve(goal, obstacles, count);
            jobRunning = true;
            lastBuildTime = now;
            lastGoal = goal;
            lastObstacleCount = count;
        }

        /// <summary>
        /// Query the obstacle field for every live asteroid inside the grid AABB around the
        /// goal and pack them (position + inflated radius) into the native buffer. Grows the
        /// scratch/native buffers on truncation up to <see cref="MaxObstacles"/>.
        /// </summary>
        private int GatherObstacles(float2 goal, IObstacleField field)
        {
            if (field == null) return 0;

            var center = new Vector2(goal.x, goal.y);
            var halfExtent = gridSize * cellSize * 0.5f;

            var n = field.QueryObstacles(center, halfExtent, scratch);
            // Filled the buffer exactly ⇒ likely truncated; grow and re-query (dense fields).
            while (n == scratch.Length && scratch.Length < MaxObstacles)
            {
                scratch = new DetectedObstacle[scratch.Length * 2];
                n = field.QueryObstacles(center, halfExtent, scratch);
            }

            if (n > obstacles.Length)
            {
                var cap = obstacles.Length;
                while (cap < n) cap *= 2;
                if (obstacles.IsCreated) obstacles.Dispose();
                obstacles = new NativeArray<float3>(cap, Allocator.Persistent);
            }

            for (var i = 0; i < n; i++)
            {
                var d = scratch[i];
                obstacles[i] = new float3(d.position.x, d.position.y, d.radius + shipRadiusBuffer);
            }
            return n;
        }
    }
}
