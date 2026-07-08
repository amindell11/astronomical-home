using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Movement.MPC.Field
{
    /// <summary>How a solve seeds the Dijkstra sweep — the one axis field types differ on.</summary>
    public enum SeedMode
    {
        /// <summary>Single zero-cost source at the goal point (pursuit: flow toward the chase target).</summary>
        Goal = 0,
        /// <summary>Every free border cell is a source, with a continuous threat-bearing initial
        /// cost (0 for the exit opposite the threat, rising to ~a grid crossing for exits toward
        /// it). Descending the field routes around obstacles toward openings away from the
        /// threat (flee: escape routing).</summary>
        BorderEscape = 1,
    }

    /// <summary>One solve's seeding parameters.</summary>
    public struct SeedSpec
    {
        public SeedMode mode;
        /// <summary>Grid anchor: the chase target (Goal) or the fleeing ship (BorderEscape).</summary>
        public float2 center;
        /// <summary>BorderEscape only: pursuer position biasing exit costs.</summary>
        public float2 threat;
        /// <summary>BorderEscape only: fraction of a full grid crossing charged to the
        /// worst-bearing exit. 1 = fleeing past the threat costs as much as detouring
        /// the entire grid.</summary>
        public float threatBias;

        public static SeedSpec ForGoal(float2 goal) =>
            new SeedSpec { mode = SeedMode.Goal, center = goal };

        public static SeedSpec ForBorderEscape(float2 self, float2 threat, float threatBias) =>
            new SeedSpec { mode = SeedMode.BorderEscape, center = self, threat = threat, threatBias = threatBias };
    }

    /// <summary>
    /// One solvable cost-to-go buffer: a square grid, circular obstacles stamped into a blocked
    /// mask, 8-connected Dijkstra from a <see cref="SeedSpec"/>-defined source set, costs in
    /// grid-step units. Resurrection of the pre-#43 NavField Dijkstra core (<c>5f5a4530~1</c>)
    /// in Burst/job form: flat NativeArrays, the stamp + seed + sweep run as a single Burst job
    /// off the main thread. Deliberately NOT resurrected: RoutedCell / gradient walking / goal
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
        /// Schedule a full rebuild (stamp + seed + Dijkstra) as a Burst job. Obstacles are
        /// (x, y, inflatedRadius) in plane space. The caller owns the obstacle array's lifetime
        /// until the returned handle completes; call <see cref="MarkSolved"/> after completion.
        /// </summary>
        public JobHandle ScheduleSolve(in SeedSpec seed, NativeArray<float3> obstacles, int obstacleCount,
            JobHandle dependsOn = default)
        {
            Goal = seed.center;
            var halfExtent = GridSize * CellSize * 0.5f;
            // Snap origin so the grid translates in whole-cell steps (stable sampling).
            var snappedX = math.floor((seed.center.x - halfExtent) / CellSize) * CellSize;
            var snappedY = math.floor((seed.center.y - halfExtent) / CellSize) * CellSize;
            Origin = new float2(snappedX, snappedY);
            HasSolution = false;

            return new SolveJob
            {
                gridSize = GridSize,
                cellSize = CellSize,
                origin = Origin,
                seed = seed,
                obstacles = obstacles,
                obstacleCount = obstacleCount,
                blocked = blocked,
                costToGo = costToGo,
            }.Schedule(dependsOn);
        }

        /// <summary>Goal-mode convenience (pursuit fields, tests).</summary>
        public JobHandle ScheduleSolve(float2 goal, NativeArray<float3> obstacles, int obstacleCount,
            JobHandle dependsOn = default)
            => ScheduleSolve(SeedSpec.ForGoal(goal), obstacles, obstacleCount, dependsOn);

        /// <summary>Call once the solve job has completed.</summary>
        public void MarkSolved() => HasSolution = true;

        /// <summary>Synchronous convenience for tests/tools.</summary>
        public void SolveImmediate(in SeedSpec seed, NativeArray<float3> obstacles, int obstacleCount)
        {
            ScheduleSolve(seed, obstacles, obstacleCount).Complete();
            MarkSolved();
        }

        /// <summary>Goal-mode convenience (pursuit fields, tests).</summary>
        public void SolveImmediate(float2 goal, NativeArray<float3> obstacles, int obstacleCount)
            => SolveImmediate(SeedSpec.ForGoal(goal), obstacles, obstacleCount);

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
            public SeedSpec seed;
            [ReadOnly] public NativeArray<float3> obstacles;
            public int obstacleCount;

            public NativeArray<byte> blocked;
            public NativeArray<float> costToGo;

            private const float SqrtTwo = 1.41421356f;

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

                // ── Seed + sweep ──
                var heapCells = new NativeArray<int>(cellCount * 4, Allocator.Temp);
                var heapCosts = new NativeArray<float>(cellCount * 4, Allocator.Temp);
                var heapCount = 0;

                if (seed.mode == SeedMode.BorderEscape)
                    SeedBorder(heapCells, heapCosts, ref heapCount);
                else
                    SeedGoal(heapCells, heapCosts, ref heapCount);

                Dijkstra(heapCells, heapCosts, ref heapCount);

                heapCells.Dispose();
                heapCosts.Dispose();

                // BorderEscape samples must be finite everywhere: blocked and unreachable
                // (pocket) cells bake a flat pessimistic value — worst-bearing exit plus a
                // full grid crossing — so the shared sampler never falls back to the
                // goal-distance formula (whose anchor here is the fleeing ship itself).
                if (seed.mode == SeedMode.BorderEscape)
                {
                    var pessimistic = seed.threatBias * (n - 1) * SqrtTwo + (n - 1) * SqrtTwo;
                    for (var i = 0; i < cellCount; i++)
                        if (blocked[i] != 0 || !math.isfinite(costToGo[i]))
                            costToGo[i] = pessimistic;
                }
            }

            /// <summary>Single zero-cost source at the seed center (nearest free cell if stamped).</summary>
            private void SeedGoal(NativeArray<int> heapCells, NativeArray<float> heapCosts, ref int heapCount)
            {
                var n = gridSize;
                var sx = math.clamp((int)math.floor((seed.center.x - origin.x) / cellSize), 0, n - 1);
                var sy = math.clamp((int)math.floor((seed.center.y - origin.y) / cellSize), 0, n - 1);
                var src = sy * n + sx;
                if (blocked[src] != 0)
                {
                    src = FindNearestFree(sx, sy);
                    if (src < 0) return; // fully enclosed — all costs stay infinite
                }
                costToGo[src] = 0f;
                Push(heapCells, heapCosts, ref heapCount, src, 0f);
            }

            /// <summary>
            /// Every free border cell is a source. Initial cost is continuous in the cell's
            /// bearing relative to the threat: 0 directly opposite, threatBias grid-crossings
            /// directly toward it — exits past the threat are never forbidden, just priced
            /// like a full-grid detour, so they win only when they are the only way out.
            /// No threat (degenerate) seeds the whole border at 0.
            /// </summary>
            private void SeedBorder(NativeArray<int> heapCells, NativeArray<float> heapCosts, ref int heapCount)
            {
                var n = gridSize;
                var toThreat = seed.threat - seed.center;
                var hasThreat = math.lengthsq(toThreat) > 1e-6f;
                var threatDir = hasThreat ? math.normalize(toThreat) : float2.zero;
                var biasMax = seed.threatBias * (n - 1) * SqrtTwo;

                for (var y = 0; y < n; y++)
                {
                    var isEdgeRow = y == 0 || y == n - 1;
                    var stepX = isEdgeRow ? 1 : n - 1; // full edge rows; side columns otherwise
                    for (var x = 0; x < n; x += stepX)
                    {
                        var idx = y * n + x;
                        if (blocked[idx] != 0) continue;

                        var cost0 = 0f;
                        if (hasThreat)
                        {
                            var cellCenter = new float2(
                                origin.x + (x + 0.5f) * cellSize,
                                origin.y + (y + 0.5f) * cellSize);
                            var dir = math.normalizesafe(cellCenter - seed.center);
                            // dot in [-1,1]: 1 = exit toward the threat, -1 = directly away.
                            cost0 = biasMax * (1f + math.dot(dir, threatDir)) * 0.5f;
                        }

                        if (cost0 < costToGo[idx])
                        {
                            costToGo[idx] = cost0;
                            Push(heapCells, heapCosts, ref heapCount, idx, cost0);
                        }
                    }
                }
            }

            private void Dijkstra(NativeArray<int> heapCells, NativeArray<float> heapCosts, ref int heapCount)
            {
                var n = gridSize;
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
                            var step = (dx != 0 && dy != 0) ? SqrtTwo : 1f;
                            var newCost = cost + step;
                            if (newCost < costToGo[nIdx])
                            {
                                costToGo[nIdx] = newCost;
                                Push(heapCells, heapCosts, ref heapCount, nIdx, newCost);
                            }
                        }
                    }
                }
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
