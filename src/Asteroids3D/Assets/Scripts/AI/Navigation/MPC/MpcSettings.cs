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
        [Tooltip("Fraction of top candidates to average (elite averaging). Higher = more stable but less reactive.")]
        [Range(0.01f, 0.5f)]
        public float eliteFraction = 0.1f;

        [Header("Adaptive Timestep")]
        [Tooltip("Max dt scale factor. 0 = disabled. In Flee: scales by closing speed. " +
                 "Otherwise: scales by distance to goal. dt is multiplied by up to (1 + this value).")]
        public float adaptiveDtScale = 0f;
        [Tooltip("Reference distance for distance-based dt scaling (non-Flee modes). " +
                 "At this distance, dt scale is at maximum.")]
        public float adaptiveDtRefDistance = 50f;

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
        [Tooltip("EMA smoothing factor for applied controls. 0 = no smoothing, 0.95 = very smooth (slow response).")]
        [Range(0f, 0.95f)]
        public float controlSmoothing = 0.5f;

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

        [Header("Obstacle Avoidance")]
        [Tooltip("Obstacle avoidance weight. Inverse-distance cost near obstacles; higher = wider berth.")]
        public float wObstacle = 10.0f;
        [Tooltip("Distance beyond an obstacle's radius at which the avoidance cost begins. " +
                 "Effectively inflates obstacles by this amount.")]
        public float obstacleThreshold = 5.0f;
        [Tooltip("Extra clearance added per unit speed. effectiveThreshold = obstacleThreshold + speed * this value.")]
        public float obstacleSpeedMargin = 0.3f;
        [Tooltip("Obstacle cost falloff exponent. Higher = cost concentrated near surface, lower = spreads further out. 2 = inverse-square (default).")]
        public float obstacleFalloffCurve = 2f;
        [Tooltip("Peak extra multiplier applied to per-obstacle cost when ship is closing on it at high speed. " +
                 "0 = disabled. Multiplier saturates: cost *= 1 + scale * v / (v + halfSpeed), where v is closing speed.")]
        public float obstacleClosingScale = 1f;
        [Tooltip("Closing speed at which the closing-scale multiplier reaches half its peak. " +
                 "Lower = ramps up faster with closing speed. Ignored when obstacleClosingScale = 0.")]
        public float obstacleClosingHalfSpeed = 5f;

        [Header("Arrival Stabilization")]
        [Tooltip("Distance to goal at which arrival stabilization begins ramping up.")]
        public float arrivalDistance = 3.0f;
        [Tooltip("Velocity cost multiplier at the goal center. Ramps from 1x at arrivalDistance to this value at 0.")]
        public float arrivalVelScale = 5.0f;
        [Tooltip("Yaw cost multiplier at the goal center. Ramps down near the goal so the ship prioritizes stopping over turning.")]
        public float arrivalYawScale = 0.1f;

        [Header("Relaxation")]
        [Tooltip("Cost at or below which controls are fully zeroed (ship coasts).")]
        public float relaxMin = 0.5f;
        [Tooltip("Cost at or above which controls are applied at full authority.")]
        public float relaxMax = 2.0f;
        [Tooltip("Curve exponent for the relaxation ramp. 1 = linear, <1 = aggressive early ramp, >1 = gentle early ramp.")]
        public float relaxCurve = 1.0f;

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
                // Obstacle
                wObstacle = wObstacle,
                obstacleThreshold = obstacleThreshold,
                obstacleSpeedMargin = obstacleSpeedMargin,
                obstacleFalloffCurve = obstacleFalloffCurve,
                obstacleClosingScale = obstacleClosingScale,
                obstacleClosingHalfSpeed = obstacleClosingHalfSpeed,
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
