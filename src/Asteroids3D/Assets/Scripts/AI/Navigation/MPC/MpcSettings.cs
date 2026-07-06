using UnityEngine;
using UnityEngine.Serialization;

namespace Movement.MPC
{
    [CreateAssetMenu(menuName = "AI/MPC Settings", fileName = "MpcSettings")]
    public class MpcSettings : ScriptableObject
    {
        [Header("Solver")]
        [Tooltip("Total lookahead time in seconds. Longer = smoother paths but slower to react.")]
        public float horizonSeconds = 1.5f;
        [Tooltip("Time step between rollout samples. Smaller = finer resolution but more steps per horizon.")]
        public float rolloutDt = 0.1f;
        [Tooltip("Number of random control sequences evaluated each solve step.")]
        public int samples = 128;
        [Tooltip("Standard deviation of Gaussian noise added to the warm-start sequence for exploration.")]
        public float noiseStd = 0.25f;
        [Tooltip("Number of interpolated noise knots per candidate. Correlated noise draws K knot " +
                 "values per channel and linearly interpolates them across the horizon, so a single " +
                 "draw expresses a coherent maneuver held over ~horizon/K steps. Min 2.")]
        public int noiseKnots = 4;
        [Tooltip("Fraction of top candidates to average (elite averaging). Higher = more stable but less reactive.")]
        [Range(0.01f, 0.5f)]
        public float eliteFraction = 0.1f;
        [Tooltip("Cross-Entropy Method iterations per solve. The 'samples' budget is split across " +
                 "these (M = samples / cemIterations), so total evaluations stay ~constant. Each " +
                 "iteration refits the sampling mean + per-channel sigma from the elites. 1 = single-shot.")]
        [Range(1, 8)]
        public int cemIterations = 2;
        [Tooltip("iCEM mean momentum: each iteration blends the mean toward the elite average by " +
                 "this fraction instead of jumping to it (lerp). Keeps the returned mean anchored to " +
                 "the shifted warm-start, restoring frame-to-frame continuity. 1 = no momentum " +
                 "(raw elite average), lower = smoother/stickier.")]
        [Range(0.05f, 1f)]
        public float meanMomentum = 0.5f;
        [Tooltip("Lower bound on the strafe channel's adaptive sampling sigma. Prevents CEM from " +
                 "collapsing lateral exploration once elites briefly agree — keeps evasive/gap-threading " +
                 "maneuvers reachable.")]
        public float strafeSigmaFloor = 0.3f;
        [Tooltip("Lower bound on the thrust/yaw adaptive sampling sigma so no channel fully collapses.")]
        public float sigmaFloor = 0.05f;

        [Header("Navigation")]
        [Tooltip("Position cost weight. Drives the ship toward the goal (Waypoint mode), " +
                 "into the range band (MaintainRange), or away from the target (Flee).")]
        public float wPos = 1.0f;
        [Tooltip("Velocity cost weight. Penalizes residual speed; scaled up near the goal for clean stops.")]
        public float wVel = 0.5f;
        [Tooltip("Closing velocity reward weight. Rewards velocity component pointing at the goal " +
                 "(Lyapunov-style gradient). Provides a continuous escape signal from spinning local " +
                 "optima. Smoothstep-gated to zero within closingFadeDistance so it doesn't fight arrival.")]
        public float wClosing = 1f;
        [Tooltip("Distance below which the closing-velocity reward fades smoothly to zero. " +
                 "Lets the velocity-damping arrival logic take over near the goal without overshooting.")]
        public float closingFadeDistance = 10f;
        [Tooltip("Heading cost weight. Aligns the ship's nose toward the goal when no facing override is set. " +
                 "Disabled when a facing override is active.")]
        public float wYaw = 0.5f;
        [Tooltip("Distance scaling for heading cost: heading_cost *= 1 + wYawDistanceScale * dist. " +
                 "Keeps the heading signal visible against position cost at long range (matches " +
                 "∂PositionCost/∂heading for positionCurve=2). 0 = current behavior, 1 = full physical scaling.")]
        public float wYawDistanceScale = 1f;
        [Tooltip("Yaw rate cost weight. Penalizes spinning; keeps rotations smooth.")]
        public float wYawRate = 0.1f;
        [Tooltip("Position cost distance exponent. 2 = quadratic (default), 1 = linear, 1.5 = compromise. " +
                 "Lower values keep facing/heading costs relevant at long range.")]
        public float positionCurve = 2f;
        [Tooltip("Distance at which position cost half-saturates (Lorentzian cap). 0 = no cap (current behavior). " +
                 "Past this distance, position cost flattens toward an asymptote — preventing far-distance " +
                 "position cost from drowning out heading/closing/yaw signals. Closing-velocity reward " +
                 "provides the urgency that quadratic position cost used to.")]
        public float positionSaturationDistance = 35f;
        [Tooltip("Peak multiplier for state costs at the end of the horizon. " +
                 "Ramps up from 0 at step 0 to this value at the final step, shaped by terminalCurve.")]
        public float terminalMultiplier = 10f;
        [Tooltip("Exponent shaping the terminal ramp. 1 = linear, >1 = late ramp (convex), <1 = early ramp (concave). " +
                 "High values approximate the old step-function behavior.")]
        public float terminalCurve = 1f;

        [Header("Control")]
        [Tooltip("Effort cost weight. Penalizes large control inputs (thrust, strafe, yaw torque).")]
        public float wEffort = 0.05f;
        [Tooltip("Smoothness weight for thrust changes between steps. Reduces forward/back jitter.")]
        public float wSmoothnessThrust = 0.5f;
        [Tooltip("Smoothness weight for strafe changes between steps. High value suppresses lateral oscillation.")]
        public float wSmoothnessStrafe = 5.0f;
        [Tooltip("Smoothness weight for yaw torque changes between steps. Reduces rotational jitter.")]
        public float wSmoothnessYaw = 0.2f;
        [Tooltip("Momentum cost weight. Penalizes velocity direction changes, rewarding smooth trajectories that maintain course.")]
        public float wMomentum = 0f;
        [Tooltip("Boost effort cost weight. Penalizes boost usage so the solver doesn't boost gratuitously.")]
        public float wBoostEffort = 0.5f;
        [Tooltip("Probability of sampling boost=1 at each step during candidate generation.")]
        [Range(0f, 1f)]
        public float boostSampleProbability = 0.15f;

        [Header("Tactical")]
        [Tooltip("Facing override weight. Steers the nose toward a specific angle (e.g. lead target in Attack). " +
                 "Only active when a facing override is set.")]
        public float wFacing = 1.0f;
        [Tooltip("Facing Huber dead-zone width in radians. Errors within this range get a gentle quadratic penalty; beyond it cost grows linearly.")]
        public float facingWidth = 0.5f;
        [Tooltip("Line-of-sight cost weight. Penalizes positions where obstacles block the view to the enemy.")]
        public float wLos = 1f;
        [Tooltip("Exposure cost weight. Penalizes being in the enemy's forward weapon arc.")]
        public float wExposure = 1f;
        [Tooltip("Exposure Gaussian width in radians. Smaller = narrower danger cone in front of enemy.")]
        public float exposureWidth = 0.5f;
        [Tooltip("Tangential velocity cost weight. Rewards lateral movement relative to enemy, making the ship harder to track.")]
        public float wTangential = 1f;
        [Tooltip("Miss distance cost weight. Penalizes being easy to hit by computing how far a projectile would miss " +
                 "given the ship's lateral velocity and range. Naturally captures speed, evasive movement, and distance.")]
        public float wMissDistance = 0f;

        [Header("Obstacle Avoidance (collision + stopping-distance admissibility)")]
        [Tooltip("Admissibility weight. Scales the stopping-distance cost that penalizes states from " +
                 "which braking/turning away is no longer possible. Higher = the ship keeps more speed " +
                 "margin around obstacles.")]
        public float wObstacle = 5.0f;
        [Tooltip("Fixed penalty added per overlapping obstacle per rollout step when the (bank-narrowed) " +
                 "hull overlaps an obstacle. Must decisively dominate stage costs so any colliding rollout " +
                 "is excluded from the elite set.")]
        public float collisionPenalty = 10000f;
        [Tooltip("Constant safety margin (world units) added to the hull for the hard collision test. " +
                 "Covers model error; NOT speed-scaled.")]
        public float obstacleSafetyMargin = 0.3f;

        [Header("Arrival Stabilization")]
        [Tooltip("Distance to goal at which arrival stabilization begins ramping up.")]
        public float arrivalDistance = 3.0f;
        [Tooltip("Velocity cost multiplier at the goal center. Ramps from 1x at arrivalDistance to this value at 0.")]
        public float arrivalVelScale = 5.0f;
        [Tooltip("Yaw cost multiplier at the goal center. Ramps down near the goal so the ship prioritizes stopping over turning.")]
        public float arrivalYawScale = 0.1f;

        public int Horizon => Mathf.CeilToInt(horizonSeconds / rolloutDt);

        public Config ToConfig(float facingTargetRad = float.NaN,
            GoalMode goalMode = GoalMode.Waypoint,
            float desiredRange = 0f, float rangeTolerance = 0f)
        {
            return new Config
            {
                dt = rolloutDt,
                invDt = rolloutDt > 0f ? 1f / rolloutDt : 0f,
                horizon = Horizon,
                // Navigation
                wPos = wPos,
                wVel = wVel,
                wClosing = wClosing,
                closingFadeDistance = closingFadeDistance,
                wYaw = wYaw,
                wYawDistanceScale = wYawDistanceScale,
                wYawRate = wYawRate,
                positionCurve = positionCurve,
                positionSaturationDistance = positionSaturationDistance,
                terminalMultiplier = terminalMultiplier,
                terminalCurve = terminalCurve,
                // Control
                wEffort = wEffort,
                wSmoothnessThrust = wSmoothnessThrust,
                wSmoothnessStrafe = wSmoothnessStrafe,
                wSmoothnessYaw = wSmoothnessYaw,
                wMomentum = wMomentum,
                wBoostEffort = wBoostEffort,
                // Tactical
                wFacing = wFacing,
                facingWidth = facingWidth,
                facingTarget = facingTargetRad,
                wLos = wLos,
                wExposure = wExposure,
                exposureWidth = exposureWidth,
                wTangential = wTangential,
                wMissDistance = wMissDistance,
                // Obstacle (shipRadius / maxLatAccel are set by Mpc from Dynamics)
                wObstacle = wObstacle,
                collisionPenalty = collisionPenalty,
                obstacleSafetyMargin = obstacleSafetyMargin,
                // Arrival
                arrivalDistance = arrivalDistance,
                arrivalDistanceSq = arrivalDistance * arrivalDistance,
                arrivalVelScale = arrivalVelScale,
                arrivalYawScale = arrivalYawScale,
                // Goal
                goalMode = goalMode,
                desiredRange = desiredRange,
                rangeTolerance = rangeTolerance,
            };
        }
    }
}
