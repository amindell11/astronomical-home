using Unity.Mathematics;

namespace Movement.MPC
{
    // Solver-owned: effort, smoothness, yaw-rate damping and the momentum prior are ship character
    // from MpcSettings — constant per ship, never varying per decision.
    public static partial class Cost
    {
        /// <summary>State regularizers (obstacle turn-away, yaw-rate damping, momentum) — state functions that ride the terminal ramp; always on. Takes the pre-resolved <paramref name="obstacleCost"/> from <see cref="ObstacleCosts"/>.</summary>
        internal static float StateRegularizers(State s, in CostInput input, in Config cfg, float obstacleCost)
        {
            var yawRate = YawRateCost(s.yawRate, cfg.maxYawRateSq) * cfg.wYawRate;
            var momentum = cfg.wMomentum > 0f ? MomentumCost(s.vel, input.initialVel) * cfg.wMomentum : 0f;
            return obstacleCost + yawRate + momentum;
        }

        /// <summary>Control cost (effort, smoothness) — a function of the input u, not the state, so it is per-step and never ramped.</summary>
        internal static float ControlCost(Control u, Control prevU, in Config cfg)
            => EffortCost(u) * cfg.wEffort + SmoothnessCost(u, prevU, cfg);

        /// <summary>Normalized 0-1: 0 = no spin, 1 = at maxYawRate.</summary>
        internal static float YawRateCost(float yawRate, float maxYawRateSq) =>
            maxYawRateSq > 0f ? (yawRate * yawRate) / maxYawRateSq : 0f;

        /// <summary>Normalized 0-1: 0 = no input, 1 = all controls maxed.</summary>
        internal static float EffortCost(Control u) =>
            (u.thrust * u.thrust + u.strafe * u.strafe + u.yawTorque * u.yawTorque) / 3f;

        /// <summary>Normalized 0-1 per axis: 0 = no change, 1 = full reversal (Δ = 2) in one step. The delta is per-step, not a rate — hence dt-free.</summary>
        internal static float SmoothnessCost(Control u, Control prev, Config cfg)
        {
            const float normFactor = 0.25f;
            var duT = u.thrust - prev.thrust;
            var duS = u.strafe - prev.strafe;
            var duY = u.yawTorque - prev.yawTorque;

            return (duT * duT * normFactor) * cfg.wSmoothnessThrust +
                   (duS * duS * normFactor) * cfg.wSmoothnessStrafe +
                   (duY * duY * normFactor) * cfg.wSmoothnessYaw;
        }

        /// <summary>Penalizes velocity direction change vs the initial velocity: 0 maintaining course → 1 reversed; 0 when either velocity is near-zero.</summary>
        internal static float MomentumCost(float2 vel, float2 initialVel)
        {
            var speedSq = math.lengthsq(vel);
            var initSpeedSq = math.lengthsq(initialVel);
            if (speedSq < 1e-4f || initSpeedSq < 1e-4f) return 0f;

            var cosAngle = math.dot(vel, initialVel) / (math.sqrt(speedSq) * math.sqrt(initSpeedSq));
            return (1f - cosAngle) * 0.5f;
        }
    }
}
