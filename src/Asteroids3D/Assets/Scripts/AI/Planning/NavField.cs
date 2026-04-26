using System;
using UnityEngine;

namespace AI.Planning
{
    public enum RoutingMode
    {
        Chase,
        Evade,
    }

    /// <summary>
    /// 2D Dijkstra flow-field over a regular grid. Stamps circular obstacles into cells,
    /// computes cost-to-go from a source via 8-connected wavefront, exposes gradient queries.
    /// Pure C#: no MonoBehaviour, no scene state. Coordinates are 2D world (caller projects).
    /// </summary>
    public sealed class NavField
    {
        public int GridSize { get; }
        public float CellSize { get; }
        public Vector2 Origin { get; private set; }
        public bool HasSolution { get; private set; }

        private readonly bool[] blocked;
        private readonly float[] costToGo;
        private readonly MinHeap heap;

        private int sourceCellIndex = -1;

        public NavField(int gridSize, float cellSize)
        {
            if (gridSize < 2) throw new ArgumentException("gridSize must be >= 2", nameof(gridSize));
            if (cellSize <= 0f) throw new ArgumentException("cellSize must be > 0", nameof(cellSize));
            GridSize = gridSize;
            CellSize = cellSize;
            blocked = new bool[gridSize * gridSize];
            costToGo = new float[gridSize * gridSize];
            heap = new MinHeap(gridSize * gridSize);
            ResetCostsToInfinity();
        }

        public Vector2 Extent => new(GridSize * CellSize, GridSize * CellSize);

        public void Recenter(Vector2 origin)
        {
            Origin = origin;
            HasSolution = false;
            sourceCellIndex = -1;
        }

        public void ClearObstacles()
        {
            Array.Clear(blocked, 0, blocked.Length);
        }

        /// <summary>Mark cells whose center lies within radius of worldPos as blocked.</summary>
        public void StampObstacle(Vector2 worldPos, float radius)
        {
            if (radius <= 0f) return;
            ToCellRange(worldPos, radius, out var minX, out var maxX, out var minY, out var maxY);
            var r2 = radius * radius;
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var c = CellCenter(x, y);
                    if ((c - worldPos).sqrMagnitude <= r2)
                    {
                        blocked[Index(x, y)] = true;
                    }
                }
            }
        }

        public void SetSource(Vector2 worldPos)
        {
            sourceCellIndex = WorldToCellClamped(worldPos);
        }

        /// <summary>Run 8-connected Dijkstra from the configured source.</summary>
        public void Solve()
        {
            ResetCostsToInfinity();
            HasSolution = false;
            if (sourceCellIndex < 0) return;

            var source = sourceCellIndex;
            if (blocked[source])
            {
                source = FindNearestFreeCell(source);
                if (source < 0) return;
            }

            costToGo[source] = 0f;
            heap.Clear();
            heap.Push(source, 0f);

            const float sqrtTwo = 1.41421356f;
            var n = GridSize;
            while (heap.TryPop(out var cell, out var cost))
            {
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
                        if (blocked[nIdx]) continue;
                        var stepCost = (dx != 0 && dy != 0) ? sqrtTwo : 1f;
                        var newCost = cost + stepCost;
                        if (newCost < costToGo[nIdx])
                        {
                            costToGo[nIdx] = newCost;
                            heap.Push(nIdx, newCost);
                        }
                    }
                }
            }
            HasSolution = true;
        }

        public bool IsBlocked(Vector2 worldPos)
        {
            var idx = WorldToCellSafe(worldPos);
            return idx < 0 || blocked[idx];
        }

        public float CostToGo(Vector2 worldPos)
        {
            var idx = WorldToCellSafe(worldPos);
            return idx < 0 ? float.PositiveInfinity : costToGo[idx];
        }

        /// <summary>
        /// Walk the gradient from shipPos for stepsAhead cells; return the resulting cell center.
        /// Chase descends cost-to-go, Evade ascends. Returns shipPos unchanged if no solution
        /// or no progress is possible.
        /// </summary>
        public Vector2 RoutedCell(Vector2 shipPos, int stepsAhead, RoutingMode mode)
        {
            if (!HasSolution || stepsAhead <= 0) return shipPos;
            var cur = WorldToCellSafe(shipPos);
            if (cur < 0) return shipPos;
            if (blocked[cur])
            {
                cur = FindNearestFreeCell(cur);
                if (cur < 0) return shipPos;
            }
            for (var step = 0; step < stepsAhead; step++)
            {
                var next = NextCellAlongGradient(cur, mode);
                if (next < 0 || next == cur) break;
                cur = next;
            }
            return CellCenterFromIndex(cur);
        }

        public Vector2 CellCenterAt(int x, int y) => CellCenter(x, y);
        public bool IsCellBlocked(int x, int y) => blocked[Index(x, y)];
        public float CostToGoAt(int x, int y) => costToGo[Index(x, y)];

        private void ResetCostsToInfinity()
        {
            for (var i = 0; i < costToGo.Length; i++) costToGo[i] = float.PositiveInfinity;
        }

        private int Index(int x, int y) => y * GridSize + x;

        private Vector2 CellCenter(int x, int y) =>
            Origin + new Vector2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);

        private Vector2 CellCenterFromIndex(int idx)
        {
            var y = idx / GridSize;
            var x = idx - y * GridSize;
            return CellCenter(x, y);
        }

        private int WorldToCellClamped(Vector2 worldPos)
        {
            var local = worldPos - Origin;
            var x = Mathf.Clamp(Mathf.FloorToInt(local.x / CellSize), 0, GridSize - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt(local.y / CellSize), 0, GridSize - 1);
            return Index(x, y);
        }

        private int WorldToCellSafe(Vector2 worldPos)
        {
            var local = worldPos - Origin;
            var x = Mathf.FloorToInt(local.x / CellSize);
            var y = Mathf.FloorToInt(local.y / CellSize);
            if (x < 0 || x >= GridSize || y < 0 || y >= GridSize) return -1;
            return Index(x, y);
        }

        private void ToCellRange(Vector2 center, float radius, out int minX, out int maxX, out int minY, out int maxY)
        {
            var local = center - Origin;
            minX = Mathf.Clamp(Mathf.FloorToInt((local.x - radius) / CellSize), 0, GridSize - 1);
            maxX = Mathf.Clamp(Mathf.CeilToInt((local.x + radius) / CellSize), 0, GridSize - 1);
            minY = Mathf.Clamp(Mathf.FloorToInt((local.y - radius) / CellSize), 0, GridSize - 1);
            maxY = Mathf.Clamp(Mathf.CeilToInt((local.y + radius) / CellSize), 0, GridSize - 1);
        }

        private int FindNearestFreeCell(int cell)
        {
            var n = GridSize;
            var cy = cell / n;
            var cx = cell - cy * n;
            for (var r = 1; r <= 5; r++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    var ny = cy + dy;
                    if (ny < 0 || ny >= n) continue;
                    for (var dx = -r; dx <= r; dx++)
                    {
                        // Only check the ring at distance r (skip interior)
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        var nx = cx + dx;
                        if (nx < 0 || nx >= n) continue;
                        var idx = ny * n + nx;
                        if (!blocked[idx]) return idx;
                    }
                }
            }
            return -1;
        }

        private int NextCellAlongGradient(int cell, RoutingMode mode)
        {
            var n = GridSize;
            var cy = cell / n;
            var cx = cell - cy * n;
            var best = -1;
            var bestVal = costToGo[cell];
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
                    if (blocked[nIdx]) continue;
                    var c = costToGo[nIdx];
                    if (float.IsPositiveInfinity(c)) continue;
                    var better = mode == RoutingMode.Chase ? c < bestVal : c > bestVal;
                    if (better)
                    {
                        bestVal = c;
                        best = nIdx;
                    }
                }
            }
            return best;
        }

        private sealed class MinHeap
        {
            private int[] cells;
            private float[] priorities;
            private int count;

            public MinHeap(int capacity)
            {
                cells = new int[Mathf.Max(16, capacity)];
                priorities = new float[Mathf.Max(16, capacity)];
            }

            public void Clear() => count = 0;

            public void Push(int cell, float priority)
            {
                if (count >= cells.Length)
                {
                    var newCap = cells.Length * 2;
                    Array.Resize(ref cells, newCap);
                    Array.Resize(ref priorities, newCap);
                }
                cells[count] = cell;
                priorities[count] = priority;
                var i = count++;
                while (i > 0)
                {
                    var parent = (i - 1) >> 1;
                    if (priorities[parent] <= priorities[i]) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            public bool TryPop(out int cell, out float priority)
            {
                if (count == 0)
                {
                    cell = -1;
                    priority = 0f;
                    return false;
                }
                cell = cells[0];
                priority = priorities[0];
                count--;
                if (count > 0)
                {
                    cells[0] = cells[count];
                    priorities[0] = priorities[count];
                    var i = 0;
                    while (true)
                    {
                        int l = i * 2 + 1, r = l + 1, smallest = i;
                        if (l < count && priorities[l] < priorities[smallest]) smallest = l;
                        if (r < count && priorities[r] < priorities[smallest]) smallest = r;
                        if (smallest == i) break;
                        Swap(i, smallest);
                        i = smallest;
                    }
                }
                return true;
            }

            private void Swap(int a, int b)
            {
                (cells[a], cells[b]) = (cells[b], cells[a]);
                (priorities[a], priorities[b]) = (priorities[b], priorities[a]);
            }
        }
    }
}
