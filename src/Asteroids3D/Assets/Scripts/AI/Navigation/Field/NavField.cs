using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Movement.MPC.Field
{
    /// <summary>
    /// One solvable cost-to-go buffer: a square grid, circular obstacles stamped into a blocked
    /// mask, 8-connected Dijkstra from a source cell (the chase target), costs in grid-step
    /// units. Resurrection of the pre-#43 NavField Dijkstra core (<c>5f5a4530~1</c>) in
    /// Burst/job form: flat NativeArrays, the stamp + sweep run as a single Burst job off the
    /// main thread. Deliberately NOT resurrected: RoutedCell / gradient walking / goal
    /// substitution — consumption is exclusively via <see cref="TerminalFieldData"/> sampling
    /// as an MPC terminal cost.
    /// </summary>
    public sealed class NavField : IDisposable
    {
        public int GridSize { get; }
        public float CellSize { get; }
        public float2 Origin { get; private set; }
        public float2 Goal { get; private set; }
        public bool HasSolution { get; private set; }

        public NativeArray<float> CostToGo => costToGo;
        public NativeArray<byte> Blocked => blocked;

        private NativeArray<float> costToGo;
        private NativeArray<byte> blocked;

        public NavField(int gridSize, float cellSize)
        {
            if (gridSize < 2) throw new ArgumentException("gridSize must be >= 2", nameof(gridSize));
            if (cellSize <= 0f) throw new ArgumentException("cellSize must be > 0", nameof(cellSize));
            GridSize = gridSize;
            CellSize = cellSize;
            costToGo = new NativeArray<float>(gridSize * gridSize, Allocator.Persistent);
            blocked = new NativeArray<byte>(gridSize * gridSize, Allocator.Persistent);
        }

        public void Dispose()
        {
            if (costToGo.IsCreated) costToGo.Dispose();
            if (blocked.IsCreated) blocked.Dispose();
        }

        /// <summary>
        /// Schedule a full rebuild (stamp + Dijkstra) as a Burst job. Obstacles are
        /// (x, y, inflatedRadius) in plane space. The caller owns the obstacle array's lifetime
        /// until the returned handle completes; call <see cref="MarkSolved"/> after completion.
        /// </summary>
        public JobHandle ScheduleSolve(float2 goal, NativeArray<float3> obstacles, int obstacleCount,
            JobHandle dependsOn = default)
        {
            Goal = goal;
            var halfExtent = GridSize * CellSize * 0.5f;
            // Snap origin so the grid translates in whole-cell steps (stable sampling).
            var snappedX = math.floor((goal.x - halfExtent) / CellSize) * CellSize;
            var snappedY = math.floor((goal.y - halfExtent) / CellSize) * CellSize;
            Origin = new float2(snappedX, snappedY);
            HasSolution = false;

            return new SolveJob
            {
                gridSize = GridSize,
                cellSize = CellSize,
                origin = Origin,
                source = goal,
                obstacles = obstacles,
                obstacleCount = obstacleCount,
                blocked = blocked,
                costToGo = costToGo,
            }.Schedule(dependsOn);
        }

        /// <summary>Call once the solve job has completed.</summary>
        public void MarkSolved() => HasSolution = true;

        /// <summary>Synchronous convenience for tests/tools.</summary>
        public void SolveImmediate(float2 goal, NativeArray<float3> obstacles, int obstacleCount)
        {
            ScheduleSolve(goal, obstacles, obstacleCount).Complete();
            MarkSolved();
        }

        /// <summary>Read-only sampling view (valid while this field's arrays are alive).</summary>
        public TerminalFieldData Data(float nominalSpeed) => new TerminalFieldData
        {
            costToGo = costToGo,
            blocked = blocked,
            gridSize = GridSize,
            cellSize = CellSize,
            origin = Origin,
            goal = Goal,
            secondsPerStep = CellSize / math.max(nominalSpeed, 0.1f),
            isValid = HasSolution ? 1 : 0,
        };

        [BurstCompile]
        private struct SolveJob : IJob
        {
            public int gridSize;
            public float cellSize;
            public float2 origin;
            public float2 source;
            [ReadOnly] public NativeArray<float3> obstacles;
            public int obstacleCount;

            public NativeArray<byte> blocked;
            public NativeArray<float> costToGo;

            public void Execute()
            {
                var n = gridSize;
                var cellCount = n * n;

                // ── Stamp obstacles ──
                for (var i = 0; i < cellCount; i++)
                {
                    blocked[i] = 0;
                    costToGo[i] = float.PositiveInfinity;
                }
                for (var o = 0; o < obstacleCount; o++)
                {
                    var obs = obstacles[o];
                    var r = obs.z;
                    if (r <= 0f) continue;
                    var minX = math.clamp((int)math.floor((obs.x - r - origin.x) / cellSize), 0, n - 1);
                    var maxX = math.clamp((int)math.ceil((obs.x + r - origin.x) / cellSize), 0, n - 1);
                    var minY = math.clamp((int)math.floor((obs.y - r - origin.y) / cellSize), 0, n - 1);
                    var maxY = math.clamp((int)math.ceil((obs.y + r - origin.y) / cellSize), 0, n - 1);
                    var r2 = r * r;
                    for (var y = minY; y <= maxY; y++)
                    {
                        var cy = origin.y + (y + 0.5f) * cellSize;
                        for (var x = minX; x <= maxX; x++)
                        {
                            var cx = origin.x + (x + 0.5f) * cellSize;
                            var dx = cx - obs.x;
                            var dy = cy - obs.y;
                            if (dx * dx + dy * dy <= r2)
                                blocked[y * n + x] = 1;
                        }
                    }
                }

                // ── Source cell (nearest free if stamped) ──
                var sx = math.clamp((int)math.floor((source.x - origin.x) / cellSize), 0, n - 1);
                var sy = math.clamp((int)math.floor((source.y - origin.y) / cellSize), 0, n - 1);
                var src = sy * n + sx;
                if (blocked[src] != 0)
                {
                    src = FindNearestFree(sx, sy);
                    if (src < 0) return; // fully enclosed — all costs stay infinite
                }

                // ── 8-connected Dijkstra (binary heap in Temp arrays) ──
                var heapCells = new NativeArray<int>(cellCount * 4, Allocator.Temp);
                var heapCosts = new NativeArray<float>(cellCount * 4, Allocator.Temp);
                var heapCount = 0;

                costToGo[src] = 0f;
                Push(heapCells, heapCosts, ref heapCount, src, 0f);

                const float sqrtTwo = 1.41421356f;
                while (heapCount > 0)
                {
                    Pop(heapCells, heapCosts, ref heapCount, out var cell, out var cost);
                    if (cost > costToGo[cell] + 1e-6f) continue;
                    var cy = cell / n;
                    var cx = cell - cy * n;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        var ny = cy + dy;
                        if (ny < 0 || ny >= n) continue;
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            var nx = cx + dx;
                            if (nx < 0 || nx >= n) continue;
                            var nIdx = ny * n + nx;
                            if (blocked[nIdx] != 0) continue;
                            var step = (dx != 0 && dy != 0) ? sqrtTwo : 1f;
                            var newCost = cost + step;
                            if (newCost < costToGo[nIdx])
                            {
                                costToGo[nIdx] = newCost;
                                Push(heapCells, heapCosts, ref heapCount, nIdx, newCost);
                            }
                        }
                    }
                }

                heapCells.Dispose();
                heapCosts.Dispose();
            }

            private int FindNearestFree(int cx, int cy)
            {
                var n = gridSize;
                for (var r = 1; r <= 5; r++)
                {
                    for (var dy = -r; dy <= r; dy++)
                    {
                        var ny = cy + dy;
                        if (ny < 0 || ny >= n) continue;
                        for (var dx = -r; dx <= r; dx++)
                        {
                            if (math.abs(dx) != r && math.abs(dy) != r) continue;
                            var nx = cx + dx;
                            if (nx < 0 || nx >= n) continue;
                            var idx = ny * n + nx;
                            if (blocked[idx] == 0) return idx;
                        }
                    }
                }
                return -1;
            }

            private static void Push(NativeArray<int> cells, NativeArray<float> costs,
                ref int count, int cell, float priority)
            {
                if (count >= cells.Length) return; // heap sized for revisit pushes; drop on overflow
                cells[count] = cell;
                costs[count] = priority;
                var i = count++;
                while (i > 0)
                {
                    var parent = (i - 1) >> 1;
                    if (costs[parent] <= costs[i]) break;
                    (cells[i], cells[parent]) = (cells[parent], cells[i]);
                    (costs[i], costs[parent]) = (costs[parent], costs[i]);
                    i = parent;
                }
            }

            private static void Pop(NativeArray<int> cells, NativeArray<float> costs,
                ref int count, out int cell, out float priority)
            {
                cell = cells[0];
                priority = costs[0];
                count--;
                if (count <= 0) return;
                cells[0] = cells[count];
                costs[0] = costs[count];
                var i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, smallest = i;
                    if (l < count && costs[l] < costs[smallest]) smallest = l;
                    if (r < count && costs[r] < costs[smallest]) smallest = r;
                    if (smallest == i) break;
                    (cells[i], cells[smallest]) = (cells[smallest], cells[i]);
                    (costs[i], costs[smallest]) = (costs[smallest], costs[i]);
                    i = smallest;
                }
            }
        }
    }
}
