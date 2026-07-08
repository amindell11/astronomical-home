using Unity.Collections;
using Unity.Mathematics;

namespace Movement.MPC.Field
{
    /// <summary>
    /// Read-only view of a solved cost-to-go field, embedded in <see cref="CostInput"/> and
    /// sampled once per rollout at the terminal state (trade study §3.1 / Option C: cost-to-go
    /// as terminal cost — no goal substitution, no waypoints, no gradient walking).
    /// Costs are stored in grid-step units; sampling converts to seconds via
    /// cellSize / nominalSpeed so <c>wTerminal ≈ 1</c> is meaningful against the
    /// time-denominated stage costs. Cells without a finite routed cost (off-grid, blocked,
    /// unreachable in a Goal-seeded field) sample a large-but-finite distance-to-goal
    /// fallback (never infinity — that would destroy elite ranking); BorderEscape-seeded
    /// fields bake a finite pessimistic value into those cells at solve time, so the
    /// fallback never fires in-grid. An invalid field contributes 0 (guarded at the call site).
    /// </summary>
    public struct TerminalFieldData
    {
        [ReadOnly] public NativeArray<float> costToGo; // grid-step units; +inf = no routed cost
        [ReadOnly] public NativeArray<byte> blocked;   // stamp mask (inspection/gizmos; not sampled)
        public int gridSize;
        public float cellSize;
        public float2 origin;       // world/plane position of grid corner (cell 0,0 min corner)
        public float2 goal;         // grid anchor (chase target / fleeing ship) in plane space
        public float secondsPerStep; // cellSize / nominalChaseSpeed
        public int isValid;         // 1 when a solved field is present

        /// <summary>Multiplier applied to the straight-line time-to-goal for off-grid or
        /// unrouted terminal states — pessimistic but finite, so a routed terminal state
        /// always beats an unrouted one at equal distance.</summary>
        public const float FallbackFactor = 3f;

        public static float Sample(float2 pos, in TerminalFieldData f)
        {
            var fallback = math.distance(pos, f.goal) / math.max(f.cellSize, 1e-3f)
                           * f.secondsPerStep * FallbackFactor;

            var n = f.gridSize;
            // Continuous cell coords of the sample relative to cell centers.
            var gx = (pos.x - f.origin.x) / f.cellSize - 0.5f;
            var gy = (pos.y - f.origin.y) / f.cellSize - 0.5f;
            var x0 = (int)math.floor(gx);
            var y0 = (int)math.floor(gy);

            if (x0 < 0 || y0 < 0 || x0 + 1 >= n || y0 + 1 >= n)
                return fallback; // off-grid (or on the outer border)

            var tx = gx - x0;
            var ty = gy - y0;

            var c00 = CellValue(f, x0, y0, fallback);
            var c10 = CellValue(f, x0 + 1, y0, fallback);
            var c01 = CellValue(f, x0, y0 + 1, fallback);
            var c11 = CellValue(f, x0 + 1, y0 + 1, fallback);

            var a = math.lerp(c00, c10, tx);
            var b = math.lerp(c01, c11, tx);
            return math.lerp(a, b, ty);
        }

        // A finite cost always wins; +inf (blocked/unreachable in a Goal-seeded field —
        // BorderEscape bakes those finite) falls back to the distance-shaped estimate.
        private static float CellValue(in TerminalFieldData f, int x, int y, float fallback)
        {
            var c = f.costToGo[y * f.gridSize + x];
            return math.isfinite(c) ? c * f.secondsPerStep : fallback;
        }
    }
}
