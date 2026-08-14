using Unity.Mathematics;

namespace Movement.MPC
{
    // Objective term: the enemy's fire lane — a ray-segment from the enemy along its facing,
    // re-resolved per rollout step. Positive weight holds the lane, negative dodges it.
    public static partial class Cost
    {
        /// <summary>Lane-distance cost at the sentence-resolved segment; 0 when LANE is unarmed or the enemy is gone. Rides the terminal ramp.</summary>
        internal static float Lane(State s, in EvalContext ctx, in Config cfg)
            => ctx.laneWeightScale != 0f
                ? LaneCost(s.pos, ctx.laneStart, ctx.laneEnd, cfg.laneWidth) * cfg.wLane * ctx.laneWeightScale
                : 0f;

        /// <summary>Normalized saturating 0–1: 0 on the segment, 0.5 at laneWidth of lateral error, →1 far away. Beyond either end the error is to the endpoint — behind the enemy is off-lane.</summary>
        internal static float LaneCost(float2 pos, float2 start, float2 end, float laneWidth)
        {
            var seg = end - start;
            var t = math.saturate(math.dot(pos - start, seg) / math.max(math.lengthsq(seg), 1e-6f));
            var errSq = math.distancesq(pos, start + t * seg);
            return errSq / math.max(errSq + laneWidth * laneWidth, 1e-6f);
        }
    }
}
