using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace AI.Navigation.MPC.TerminalField
{
    [BurstCompile(CompileSynchronously = true)]
    public struct TerminalFieldBakeJob : IJob
    {
        [ReadOnly] public NativeArray<ObstacleData> obstacles;
        public int obstacleCount;
        public int resolution;
        public float spacing;
        public float2 origin;
        public float2 goal;
        public float clearance;
        public NativeArray<float> distances;
        public NativeArray<byte> occupied;
        public NativeArray<int> heap;
        public NativeArray<int> heapPosition;
        public NativeArray<BakeResult> result;
        private int heapCount;

        public struct BakeResult
        {
            public int seedIndex;
            public float maxFiniteDistance;
        }

        public void Execute()
        {
            heapCount = 0;
            var seed = -1;
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < distances.Length; i++)
            {
                distances[i] = float.PositiveInfinity;
                heapPosition[i] = -1;
                var center = Center(i);
                var blocked = false;
                for (var j = 0; j < obstacleCount; j++)
                {
                    var obstacle = obstacles[j];
                    var delta = center - obstacle.position;
                    var radius = obstacle.radius + clearance;
                    if (math.lengthsq(delta) <= radius * radius)
                    {
                        blocked = true;
                        break;
                    }
                }
                occupied[i] = (byte)(blocked ? 1 : 0);
                var distance = math.distancesq(center, goal);
                if (blocked || distance >= nearest) continue;
                nearest = distance;
                seed = i;
            }

            var maximum = 0f;
            if (seed >= 0)
            {
                distances[seed] = math.sqrt(nearest);
                InsertOrDecrease(seed);
            }
            while (heapCount > 0)
            {
                var cell = Pop();
                maximum = math.max(maximum, distances[cell]);
                var x = cell % resolution;
                var y = cell / resolution;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= resolution || ny >= resolution) continue;
                    var next = nx + ny * resolution;
                    if (occupied[next] != 0 || heapPosition[next] == -2) continue;
                    if (dx != 0 && dy != 0
                        && (occupied[cell + dx] != 0 || occupied[cell + dy * resolution] != 0)) continue;
                    var distance = distances[cell] + spacing * (dx != 0 && dy != 0 ? math.sqrt(2f) : 1f);
                    if (distance >= distances[next]) continue;
                    distances[next] = distance;
                    InsertOrDecrease(next);
                }
            }
            result[0] = new BakeResult { seedIndex = seed, maxFiniteDistance = maximum };
        }

        private float2 Center(int index) => origin + spacing * new float2(index % resolution, index / resolution);

        private bool Before(int a, int b) => distances[a] < distances[b]
            || distances[a] == distances[b] && a < b;

        private void InsertOrDecrease(int cell)
        {
            var position = heapPosition[cell];
            if (position == -1) position = heapCount++;
            while (position > 0)
            {
                var parent = (position - 1) / 2;
                var above = heap[parent];
                if (!Before(cell, above)) break;
                heap[position] = above;
                heapPosition[above] = position;
                position = parent;
            }
            heap[position] = cell;
            heapPosition[cell] = position;
        }

        private int Pop()
        {
            var first = heap[0];
            heapPosition[first] = -2;
            var last = heap[--heapCount];
            if (heapCount == 0) return first;
            var position = 0;
            while (position * 2 + 1 < heapCount)
            {
                var child = position * 2 + 1;
                if (child + 1 < heapCount && Before(heap[child + 1], heap[child])) child++;
                if (!Before(heap[child], last)) break;
                heap[position] = heap[child];
                heapPosition[heap[position]] = position;
                position = child;
            }
            heap[position] = last;
            heapPosition[last] = position;
            return first;
        }
    }
}
