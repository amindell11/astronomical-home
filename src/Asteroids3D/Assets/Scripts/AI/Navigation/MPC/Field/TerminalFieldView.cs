using Unity.Collections;
using Unity.Mathematics;

namespace AI.Navigation.MPC.Field
{
    /// <summary>Read-only sampling view of one baked field, riding <c>CostInput</c> into the Burst rollout. Cells hold traversable route distance to the goal in metres (+∞ = occupied or disconnected). The cost term charges <see cref="DetourExcess"/>: route distance minus the octile straight-line distance from the seed cell, so an empty grid samples 0 everywhere, unreachable cells take a finite bound before interpolation, and a point outside the domain pays the Euclidean distance back in — every value the sampler sees is finite.</summary>
    public struct TerminalFieldView
    {
        private const float OctileDiagonal = math.SQRT2 - 1f;

        [ReadOnly] public NativeArray<float> distances;
        [ReadOnly] public NativeArray<byte> occupied;
        public int valid;
        public int resolution;
        public float spacing;
        public float2 origin;        // plane position of the centre of cell (0,0)
        public float2 goal;
        public int seedIndex;
        public float seedToGoal;     // the seed's initial distance: |seed centre − goal|
        public float maxFiniteDistance;  // 0 when nothing is reachable

        public bool IsValid => valid != 0 && distances.IsCreated;

        public float GridDiagonal => (resolution - 1) * spacing * math.SQRT2;

        public float UnreachableBound => maxFiniteDistance + GridDiagonal;

        public float2 CellCentre(int x, int y) => origin + new float2(x, y) * spacing;

        public float CellDistance(int x, int y) => distances[x + y * resolution];

        /// <summary>The octile route length an empty grid would bake for this cell, seed offset included.</summary>
        public float EmptyDistance(int x, int y)
        {
            var a = math.abs(x - seedIndex % resolution);
            var b = math.abs(y - seedIndex / resolution);
            return spacing * (math.max(a, b) + OctileDiagonal * math.min(a, b)) + seedToGoal;
        }

        // An occupied cell one step from a reachable neighbour carries that neighbour's excess plus the edge,
        // not the unreachable bound: bilinear interpolation blends its corners into free points beside a rock.
        public float CellExcess(int x, int y)
        {
            var d = CellDistance(x, y);
            if (math.isfinite(d)) return math.max(0f, d - EmptyDistance(x, y));

            var best = UnreachableBound;
            if (occupied[x + y * resolution] == 0) return best;
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= resolution || ny >= resolution) continue;
                var nd = CellDistance(nx, ny);
                if (!math.isfinite(nd)) continue;
                var edge = spacing * (dx != 0 && dy != 0 ? math.SQRT2 : 1f);
                best = math.min(best, math.max(0f, nd - EmptyDistance(nx, ny)) + edge);
            }
            return best;
        }

        /// <summary>Bilinear detour excess at a plane point; outside the cell-centre domain, the clamped sample plus the distance back in.</summary>
        public float DetourExcess(float2 pos)
        {
            var n = resolution;
            var g = (pos - origin) / spacing;
            var clamped = math.clamp(g, 0f, n - 1);
            var outside = math.distance(g, clamped) * spacing;

            var x0 = (int)math.floor(clamped.x);
            var y0 = (int)math.floor(clamped.y);
            var x1 = math.min(x0 + 1, n - 1);
            var y1 = math.min(y0 + 1, n - 1);
            var t = clamped - new float2(x0, y0);

            var bottom = math.lerp(CellExcess(x0, y0), CellExcess(x1, y0), t.x);
            var top = math.lerp(CellExcess(x0, y1), CellExcess(x1, y1), t.x);
            return math.lerp(bottom, top, t.y) + outside;
        }
    }
}
