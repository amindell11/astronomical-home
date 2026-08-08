using Unity.Mathematics;

namespace Movement.MPC
{
    // Objective term: what the nose is for. Both the commanded target and its authority come from
    // the decision's NavObjective; the prior is the weight-0 floor underneath it.
    public static partial class Cost
    {
        /// <summary>Intercept-facing geometry. Ramped; 0 when no facing target is set; scaled by the anchored authority (×1 on the legacy path).</summary>
        internal static float Aim(State s, in EvalContext ctx, in Config cfg)
            => FacingCost(s.yaw, ctx.facingTarget, cfg.facingWidth) * cfg.wFacing * ctx.facingWeightScale;

        /// <summary>Facing resolver for the enemy anchor: intercept lead when a projectile speed exists, pure bearing for hitscan.</summary>
        internal static float AnchorYaw(float2 shipPos, float2 targetPos, float2 targetVel, float projectileSpeed)
        {
            if (projectileSpeed > 0f) return InterceptYaw(shipPos, targetPos, targetVel, projectileSpeed);
            var toTarget = targetPos - shipPos;
            return math.atan2(-toTarget.x, toTarget.y);
        }

        /// <summary>Yaw to a first-order intercept point, using t = dist / projectileSpeed as the time-of-flight estimate.</summary>
        internal static float InterceptYaw(float2 shipPos, float2 targetPos, float2 targetVel, float projectileSpeed)
        {
            var toTarget = targetPos - shipPos;
            var dist = math.length(toTarget);
            if (dist < 1e-4f) return math.atan2(-toTarget.x, toTarget.y);
            var tof = dist / projectileSpeed;
            var intercept = targetPos + targetVel * tof;
            var toIntercept = intercept - shipPos;
            return math.atan2(-toIntercept.x, toIntercept.y);
        }

        /// <summary>Velocity-aligned facing prior — the weight-0 delegation floor (nose eases toward the direction of travel instead of drifting). Speed-gated so a near-rest ship's nose is free. Ramped alongside the facing term.</summary>
        internal static float FacingPriorCost(float yaw, float2 vel, in Config cfg)
        {
            if (cfg.wFacingPrior <= 0f || math.lengthsq(vel) < 1e-4f) return 0f;
            return FacingCost(yaw, math.atan2(-vel.x, vel.y), cfg.facingWidth) * cfg.wFacingPrior;
        }

        /// <summary>Normalized 0-1: 0 = on target, 1 = worst possible facing (π error).</summary>
        internal static float FacingCost(float yaw, float targetYaw, float width = 1f)
        {
            if (math.isnan(targetYaw)) return 0f;
            var err = math.abs(WrapRadians(yaw - targetYaw));
            var raw = err < width ? err * err : 2f * width * err - width * width;
            var maxRaw = 2f * width * math.PI - width * width;
            return raw / math.max(maxRaw, 1e-4f);
        }
    }
}
