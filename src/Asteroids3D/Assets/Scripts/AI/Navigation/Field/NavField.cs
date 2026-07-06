using System;
using UnityEngine;

namespace AI.Navigation.Field
{
    public sealed class NavField
    {
        private const float SqrtTwo = 1.41421356f;

        private readonly bool[] blocked;
        private readonly float[] costs;
        private readonly MinHeap heap;
        private int source = -1;

        public int GridSize { get; }
        public float CellSize { get; }
        public Vector2 Origin { get; private set; }
        public bool HasSolution { get; private set; }
        public float[] Costs => costs;

        public NavField(int gridSize, float cellSize)
        {
            if (gridSize < 2) throw new ArgumentException("gridSize must be >= 2", nameof(gridSize));
            if (cellSize <= 0f) throw new ArgumentException("cellSize must be > 0", nameof(cellSize));
            GridSize = gridSize;
            CellSize = cellSize;
            blocked = new bool[gridSize * gridSize];
            costs = new float[gridSize * gridSize];
            heap = new MinHeap(gridSize * gridSize);
            ResetCosts();
        }

        public void Recenter(Vector2 origin)
        {
            Origin = origin;
            source = -1;
            HasSolution = false;
        }

        public void ClearObstacles() => Array.Clear(blocked, 0, blocked.Length);

        public void StampObstacle(Vector2 position, float radius)
        {
            if (radius <= 0f) return;
            ToCellRange(position, radius, out var minX, out var maxX, out var minY, out var maxY);
            var radiusSq = radius * radius;
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                if ((CellCenter(x, y) - position).sqrMagnitude <= radiusSq)
                    blocked[Index(x, y)] = true;
            }
        }

        public void SetSource(Vector2 position) => source = WorldToCellClamped(position);

        public void Solve()
        {
            ResetCosts();
            HasSolution = false;
            if (source < 0) return;

            var start = blocked[source] ? FindNearestFreeCell(source) : source;
            if (start < 0) return;

            costs[start] = 0f;
            heap.Clear();
            heap.Push(start, 0f);

            while (heap.TryPop(out var cell, out var cost))
            {
                if (cost > costs[cell] + 1e-6f) continue;
                var cy = cell / GridSize;
                var cx = cell - cy * GridSize;
                for (var dy = -1; dy <= 1; dy++)
                {
                    var ny = cy + dy;
                    if (ny < 0 || ny >= GridSize) continue;
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var nx = cx + dx;
                        if (nx < 0 || nx >= GridSize) continue;
                        var n = Index(nx, ny);
                        if (blocked[n]) continue;
                        var nextCost = cost + (dx != 0 && dy != 0 ? SqrtTwo : 1f);
                        if (nextCost >= costs[n]) continue;
                        costs[n] = nextCost;
                        heap.Push(n, nextCost);
                    }
                }
            }

            HasSolution = true;
        }

        public bool IsBlocked(Vector2 position)
        {
            var idx = WorldToCellSafe(position);
            return idx < 0 || blocked[idx];
        }

        public float CostToGo(Vector2 position)
        {
            var idx = WorldToCellSafe(position);
            return idx < 0 ? float.PositiveInfinity : costs[idx];
        }

        public float SampleTimeToGo(Vector2 position, float nominalSpeed)
        {
            var cost = CostToGo(position);
            return float.IsInfinity(cost) ? float.PositiveInfinity : cost * CellSize / Mathf.Max(nominalSpeed, 0.01f);
        }

        private void ResetCosts()
        {
            for (var i = 0; i < costs.Length; i++)
                costs[i] = float.PositiveInfinity;
        }

        private int Index(int x, int y) => y * GridSize + x;
        private Vector2 CellCenter(int x, int y) => Origin + new Vector2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);

        private int WorldToCellSafe(Vector2 position)
        {
            var local = position - Origin;
            var x = Mathf.FloorToInt(local.x / CellSize);
            var y = Mathf.FloorToInt(local.y / CellSize);
            return x < 0 || x >= GridSize || y < 0 || y >= GridSize ? -1 : Index(x, y);
        }

        private int WorldToCellClamped(Vector2 position)
        {
            var local = position - Origin;
            var x = Mathf.Clamp(Mathf.FloorToInt(local.x / CellSize), 0, GridSize - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt(local.y / CellSize), 0, GridSize - 1);
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
            var cy = cell / GridSize;
            var cx = cell - cy * GridSize;
            for (var r = 1; r <= 5; r++)
            for (var dy = -r; dy <= r; dy++)
            {
                var y = cy + dy;
                if (y < 0 || y >= GridSize) continue;
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    var x = cx + dx;
                    if (x < 0 || x >= GridSize) continue;
                    var idx = Index(x, y);
                    if (!blocked[idx]) return idx;
                }
            }
            return -1;
        }

        private sealed class MinHeap
        {
            private readonly int[] cells;
            private readonly float[] priorities;
            private int count;

            public MinHeap(int capacity)
            {
                cells = new int[capacity];
                priorities = new float[capacity];
            }

            public void Clear() => count = 0;

            public void Push(int cell, float priority)
            {
                if (count >= cells.Length) return;
                cells[count] = cell;
                priorities[count] = priority;
                var i = count++;
                while (i > 0)
                {
                    var parent = (i - 1) >> 1;
                    if (priorities[parent] <= priorities[i]) break;
                    (cells[i], cells[parent]) = (cells[parent], cells[i]);
                    (priorities[i], priorities[parent]) = (priorities[parent], priorities[i]);
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
                if (count <= 0) return true;
                cells[0] = cells[count];
                priorities[0] = priorities[count];
                var i = 0;
                while (true)
                {
                    var left = i * 2 + 1;
                    var right = left + 1;
                    var smallest = i;
                    if (left < count && priorities[left] < priorities[smallest]) smallest = left;
                    if (right < count && priorities[right] < priorities[smallest]) smallest = right;
                    if (smallest == i) break;
                    (cells[i], cells[smallest]) = (cells[smallest], cells[i]);
                    (priorities[i], priorities[smallest]) = (priorities[smallest], priorities[i]);
                    i = smallest;
                }
                return true;
            }
        }
    }
}
