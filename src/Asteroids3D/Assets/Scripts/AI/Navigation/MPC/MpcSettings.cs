using UnityEngine;

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
        [Tooltip("Number of noise knots spread evenly over the horizon. Gaussian noise is drawn " +
                 "at each knot and linearly interpolated between them (time-correlated exploration), " +
                 "so one draw can hold a maneuver for several steps. 2 = one linear ramp across the " +
                 "whole horizon; higher = choppier, approaching per-step i.i.d. noise.")]
        [Range(2, 16)]
        public int noiseKnots = 5;
        [Tooltip("Fraction of top candidates to average (elite averaging). Higher = more stable but less reactive.")]
        [Range(0.01f, 0.5f)]
        public float eliteFraction = 0.1f;

        [Header("Tracking")]
        [Tooltip("Weight on the velocity-tracking objective: cost = wVelTrack * ‖vel - velocityReference‖² / maxSpeed². " +
                 "Per-step (un-ramped) so tracking is uniform across the horizon rather than terminal-weighted.")]
        public float wVelTrack = 5f;
        [Tooltip("Yaw rate cost weight. Penalizes spinning; keeps rotations smooth.")]
        public float wYawRate = 0.1f;
        [Tooltip("Peak multiplier for state costs at the end of the horizon. " +
                 "Ramps up from 0 at step 0 to this value at the final step, shaped by terminalCurve.")]
        public float terminalMultiplier = 10f;
        [Tooltip("Exponent shaping the terminal ramp. 1 = linear, >1 = late ramp (convex), <1 = early ramp (concave).")]
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

        [Header("Aim")]
        [Tooltip("Facing weight. Steers the nose toward the intercept-lead angle (or an explicit facing override).")]
        public float wFacing = 1.0f;
        [Tooltip("Facing Huber dead-zone width in radians. Errors within this range get a gentle quadratic penalty; beyond it cost grows linearly.")]
        public float facingWidth = 0.5f;
        [Tooltip("Velocity-aligned facing prior weight — the weight-0 delegation floor: with facing authority at 0 " +
                 "the nose eases toward the direction of travel instead of drifting. Keep well below wFacing so it " +
                 "only wins when the commanded facing abstains. 0 disables (production default).")]
        public float wFacingPrior = 0f;

        [Header("Obstacle Avoidance")]
        [Tooltip("Admissibility (turn-away) weight. Penalizes rollout states whose velocity leads " +
                 "into an obstacle that the ship's lateral thrust can no longer sidestep before " +
                 "reaching it (collision-course-gated, continuous, C1 at the boundary). Obstacles " +
                 "the ship already passes clear of cost nothing — a weaving pursuer steers around " +
                 "off-course rocks for free.")]
        public float wObstacle = 5f;
        [Tooltip("Fixed cost added for every rollout step whose (bank-narrowed) hull overlaps an obstacle. " +
                 "Near-binary: must decisively dominate any per-step stage cost (>=10x) so colliding " +
                 "rollouts never win the elite set.")]
        public float collisionPenalty = 10000f;
        [Tooltip("Constant safety margin added to the hull radius in the collision test, absorbing " +
                 "model error. Deliberately NOT speed-scaled — speed safety is the admissibility term's job.")]
        public float collisionSafetyMargin = 0.3f;
        [Tooltip("Represent an elongated asteroid as its 2 tighter baked lobe spheres instead of " +
                 "one fat covering circle, freeing the space beside the rod that the single circle " +
                 "blocked. Kill switch: OFF reverts to single-circle-per-rock (byte-identical to " +
                 "pre-multi-sphere behaviour). Rocks with ≤1 baked lobe are unaffected either way.")]
        public bool multiSphereObstacles = true;

        public int Horizon => Mathf.CeilToInt(horizonSeconds / rolloutDt);

        public Config ToConfig(float facingTargetRad = float.NaN)
        {
            return new Config
            {
                dt = rolloutDt,
                invDt = rolloutDt > 0f ? 1f / rolloutDt : 0f,
                horizon = Horizon,
                wYawRate = wYawRate,
                terminalMultiplier = terminalMultiplier,
                terminalCurve = terminalCurve,
                wEffort = wEffort,
                wSmoothnessThrust = wSmoothnessThrust,
                wSmoothnessStrafe = wSmoothnessStrafe,
                wSmoothnessYaw = wSmoothnessYaw,
                wMomentum = wMomentum,
                wBoostEffort = wBoostEffort,
                wFacing = wFacing,
                facingWidth = facingWidth,
                facingTarget = facingTargetRad,
                wFacingPrior = wFacingPrior,
                wObstacle = wObstacle,
                collisionPenalty = collisionPenalty,
                collisionSafetyMargin = collisionSafetyMargin,
                wVelTrack = wVelTrack,
            };
        }
    }
}
