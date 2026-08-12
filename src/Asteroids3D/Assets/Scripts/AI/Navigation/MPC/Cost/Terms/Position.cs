using Unity.Mathematics;

namespace Movement.MPC
{
    // Objective term: where the ship should be. The POS sentence slot places a point (setpoint 0)
    // or a hold-ring (setpoint r₀) in its referent's chosen frame, re-resolved per rollout step.
    public static partial class Cost
    {
        /// <summary>Ring cost at the sentence-resolved point; 0 when POS is unarmed or its referent is gone; scaled by the slot's signed authority × the wPos ceiling. Rides the terminal ramp.</summary>
        internal static float Pos(State s, in EvalContext ctx, in Config cfg)
            => ctx.posWeightScale != 0f
                ? RingCost(s.pos, ctx.posPoint, ctx.posSetpoint, cfg.posWidth) * cfg.wPos * ctx.posWeightScale
                : 0f;

        /// <summary>Normalized 0–1 saturating: 0 at setpoint distance from the point (setpoint 0 = at the point), 0.5 at posWidth of error, →1 far away — err²/(err² + posWidth²) keeps distant errors bounded so POS stays comparable to the other terms.</summary>
        internal static float RingCost(float2 pos, float2 point, float setpoint, float posWidth)
        {
            var err = math.distance(pos, point) - setpoint;
            var errSq = err * err;
            return errSq / math.max(errSq + posWidth * posWidth, 1e-6f);
        }
    }
}
