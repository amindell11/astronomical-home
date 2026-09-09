using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace AI.Navigation.MPC.TerminalField
{
    /// <summary>One synchronous bake: rasterise inflated discs into the occupancy mask, seed the nearest free cell to the goal (ties by index) with its goal distance, then 8-neighbour Dijkstra at h / √2·h with no diagonal corner-cutting. Occupied and disconnected cells stay +∞; <see cref="maxFiniteOut"/> is 0 when nothing is reachable.</summary>
    [BurstCompile]
    public struct TerminalFieldBakeJob : IJob
    {
        [ReadOnly] public NativeArray<float3> discs;   // x, y, inflated radius
        public int discCount;
        public int resolution;
        public float spacing;
        public float2 origin;
        public float2 goal;

        public NativeArray<float> distances;
        public NativeArray<byte> occupied;
        public NativeArray<int> heap;       // scratch: queued cell indices
        public NativeArray<int> heapSlot;   // scratch: cell → heap position, else Unqueued / Settled
        public NativeArray<int> seedIndexOut;
        public NativeArray<float> maxFiniteOut;

        private const int Unqueued = -1;
        private const int Settled = -2;

        public void Execute()
        {
            var n = resolution;
            var cells = n * n;
            for (var i = 0; i < cells; i++)
            {
                occupied[i] = 0;
                distances[i] = float.PositiveInfinity;
                heapSlot[i] = Unqueued;
            }

            Rasterise();

            var seed = NearestCell(goal, freeOnly: true);
            if (seed < 0) seed = NearestCell(goal, freeOnly: false);
            seedIndexOut[0] = seed;
            if (occupied[seed] != 0)
            {
                maxFiniteOut[0] = 0f;
                return;
            }

            var heapCount = 0;
            distances[seed] = math.distance(CellCentre(seed % n, seed / n), goal);
            Push(seed, ref heapCount);

            var diagonalStep = spacing * math.SQRT2;
            while (heapCount > 0)
            {
                var current = Pop(ref heapCount);
                var cx = current % n;
                var cy = current / n;
                var d = distances[current];

                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var nx = cx + dx;
                    var ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                    var neighbour = nx + ny * n;
                    if (occupied[neighbour] != 0 || heapSlot[neighbour] == Settled) continue;
                    var diagonal = dx != 0 && dy != 0;
                    if (diagonal && (occupied[nx + cy * n] != 0 || occupied[cx + ny * n] != 0)) continue;

                    var candidate = d + (diagonal ? diagonalStep : spacing);
                    if (candidate >= distances[neighbour]) continue;
                    distances[neighbour] = candidate;
                    if (heapSlot[neighbour] == Unqueued) Push(neighbour, ref heapCount);
                    else SiftUp(heapSlot[neighbour]);
                }
            }

            var maxFinite = 0f;
            for (var i = 0; i < cells; i++)
            {
                var d = distances[i];
                if (math.isfinite(d)) maxFinite = math.max(maxFinite, d);
            }
            maxFiniteOut[0] = maxFinite;
        }

        private void Rasterise()
        {
            var n = resolution;
            for (var k = 0; k < discCount; k++)
            {
                var disc = discs[k];
                var centre = disc.xy;
                var r = disc.z;
                var rSq = r * r;
                var lo = math.max((int2)math.floor((centre - r - origin) / spacing), 0);
                var hi = math.min((int2)math.ceil((centre + r - origin) / spacing), n - 1);
                for (var y = lo.y; y <= hi.y; y++)
                for (var x = lo.x; x <= hi.x; x++)
                {
                    if (math.distancesq(CellCentre(x, y), centre) < rSq)
                        occupied[x + y * n] = 1;
                }
            }
        }

        private int NearestCell(float2 point, bool freeOnly)
        {
            var n = resolution;
            var best = -1;
            var bestSq = float.PositiveInfinity;
            for (var i = 0; i < n * n; i++)
            {
                if (freeOnly && occupied[i] != 0) continue;
                var dSq = math.distancesq(CellCentre(i % n, i / n), point);
                if (dSq >= bestSq) continue;
                bestSq = dSq;
                best = i;
            }
            return best;
        }

        private float2 CellCentre(int x, int y) => origin + new float2(x, y) * spacing;

        private void Push(int cell, ref int heapCount)
        {
            heap[heapCount] = cell;
            heapSlot[cell] = heapCount;
            heapCount++;
            SiftUp(heapCount - 1);
        }

        private int Pop(ref int heapCount)
        {
            var top = heap[0];
            heapSlot[top] = Settled;
            heapCount--;
            if (heapCount > 0)
            {
                var last = heap[heapCount];
                heap[0] = last;
                heapSlot[last] = 0;
                SiftDown(0, heapCount);
            }
            return top;
        }

        private void SiftUp(int slot)
        {
            var cell = heap[slot];
            var d = distances[cell];
            while (slot > 0)
            {
                var parent = (slot - 1) >> 1;
                var parentCell = heap[parent];
                if (distances[parentCell] <= d) break;
                heap[slot] = parentCell;
                heapSlot[parentCell] = slot;
                slot = parent;
            }
            heap[slot] = cell;
            heapSlot[cell] = slot;
        }

        private void SiftDown(int slot, int heapCount)
        {
            var cell = heap[slot];
            var d = distances[cell];
            while (true)
            {
                var left = 2 * slot + 1;
                if (left >= heapCount) break;
                var right = left + 1;
                var child = right < heapCount && distances[heap[right]] < distances[heap[left]] ? right : left;
                var childCell = heap[child];
                if (distances[childCell] >= d) break;
                heap[slot] = childCell;
                heapSlot[childCell] = slot;
                slot = child;
            }
            heap[slot] = cell;
            heapSlot[cell] = slot;
        }
    }
}
