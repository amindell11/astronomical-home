using Unity.Collections;
using Unity.Mathematics;

namespace AI.Navigation.MPC.TerminalField
{
    public struct TerminalFieldView
    {
        [ReadOnly] public NativeArray<float> distances;
        [ReadOnly] public NativeArray<float> emptyDistances;
        [ReadOnly] public NativeArray<byte> occupied;
        public bool valid;
        public float2 origin;
        public float spacing;
        public int resolution;
        public float2 goal;
        public float goalRadius;
        public int seedIndex;
        public float maxFiniteDistance;

        public float2 CellCenter(int index) => origin + spacing * new float2(index % resolution, index / resolution);

        public bool Contains(float2 point) => math.all(point >= origin)
            && math.all(point <= origin + spacing * (resolution - 1));

        public float Excess(int index)
        {
            var distance = distances[index];
            if (math.isfinite(distance)) return ReachableExcess(index, distance);
            var best = maxFiniteDistance + spacing * (resolution - 1) * math.sqrt(2f);
            if (occupied[index] == 0) return best;
            var x = index % resolution;
            var y = index / resolution;
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= resolution || ny >= resolution) continue;
                var next = nx + ny * resolution;
                if (!math.isfinite(distances[next])) continue;
                var edge = spacing * (dx != 0 && dy != 0 ? math.sqrt(2f) : 1f);
                best = math.min(best, ReachableExcess(next, distances[next]) + edge);
            }
            return best;
        }

        private float ReachableExcess(int index, float distance)
        {
            if (goalRadius > 0f) return math.max(0f, distance - emptyDistances[index]);
            var delta = math.abs(new int2(index % resolution, index / resolution)
                - new int2(seedIndex % resolution, seedIndex / resolution));
            var empty = spacing * (math.cmax(delta) + (math.sqrt(2f) - 1f) * math.cmin(delta))
                + math.distance(CellCenter(seedIndex), goal);
            return math.max(0f, distance - empty);
        }

        public float Sample(float2 point)
        {
            if (!valid) return 0f;
            var grid = math.clamp((point - origin) / spacing, 0f, resolution - 1f);
            var lo = math.min((int2)math.floor(grid), resolution - 2);
            var fraction = grid - lo;
            var index = lo.x + lo.y * resolution;
            var lower = math.lerp(Excess(index), Excess(index + 1), fraction.x);
            var upper = math.lerp(Excess(index + resolution), Excess(index + resolution + 1), fraction.x);
            return math.lerp(lower, upper, fraction.y) + math.distance(point, origin + spacing * grid);
        }
    }
}
