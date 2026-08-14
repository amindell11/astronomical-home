using Unity.Mathematics;

namespace Movement.MPC
{
    // Objective term: where the ship should be. The POS sentence slot places a point (setpoint 0)
    // or a hold-ring (setpoint r₀) in its referent's chosen frame, re-resolved per rollout step.
    public static partial class Cost
    {
        /// <summary>Ring cost at the sentence-resolved point; 0 when POS is unarmed or its referent is gone. Rides the terminal ramp.</summary>
        internal static float Pos(State s, in EvalContext ctx, in Config cfg)
            => ctx.posWeightScale != 0f
                ? RingCost(s.pos, ctx.posPoint, ctx.posSetpoint, cfg.posWidth) * cfg.wPos * ctx.posWeightScale
                : 0f;

        /// <summary>Normalized saturating 0–1: 0 at setpoint distance, 0.5 at posWidth of error, →1 far away — distant errors stay bounded.</summary>
        internal static float RingCost(float2 pos, float2 point, float setpoint, float posWidth)
        {
            var err = math.distance(pos, point) - setpoint;
            var errSq = err * err;
            return errSq / math.max(errSq + posWidth * posWidth, 1e-6f);
        }

        /// <summary>Error-relative POS width, fixed per solve: clamp(slope·err₀, posWidth, ∞) with err₀ the ring
        /// error at the solve's initial state — reach gets a far-field gradient, settle keeps the posWidth floor.
        /// cfg.posWidth unchanged when POS is unarmed, unresolved, or carries no authority.</summary>
        internal static float EffectivePosWidth(State initial, in CostInput input, in Config cfg, float slope)
        {
            if (slope <= 0f || !input.sentence.pos.armed) return cfg.posWidth;
            var ctx = EvalContext.Create(initial, input, cfg, 0);
            if (ctx.posWeightScale == 0f) return cfg.posWidth;
            var err0 = math.abs(math.distance(initial.pos, ctx.posPoint) - ctx.posSetpoint);
            return math.max(slope * err0, cfg.posWidth);
        }
    }
}
