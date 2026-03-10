using UnityEngine;

namespace Movement.MPC
{
    [CreateAssetMenu(menuName = "AI/MPC Settings", fileName = "MpcSettings")]
    public class Settings : ScriptableObject
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

        [Header("Weights")]
        [Tooltip("Position cost weight. Drives the ship toward the goal (Waypoint mode), " +
                 "into the range band (MaintainRange), or away from the target (Flee).")]
        public float wPos = 1.0f;
        [Tooltip("Velocity cost weight. Penalizes residual speed; scaled up near the goal for clean stops.")]
        public float wVel = 0.5f;
        [Tooltip("Heading cost weight. Aligns the ship's nose toward the goal when no facing override is set. " +
                 "Disabled when a facing override is active.")]
        public float wYaw = 0.5f;
        [Tooltip("Yaw rate cost weight. Penalizes spinning; keeps rotations smooth.")]
        public float wYawRate = 0.1f;
        [Tooltip("Effort cost weight. Penalizes large control inputs (thrust, strafe, yaw torque).")]
        public float wEffort = 0.05f;

        [Header("Smoothness Weights")]
        [Tooltip("Smoothness weight for thrust changes between steps. Reduces forward/back jitter.")]
        public float wSmoothnessThrust = 0.5f;
        [Tooltip("Smoothness weight for strafe changes between steps. High value suppresses lateral oscillation.")]
        public float wSmoothnessStrafe = 5.0f;
        [Tooltip("Smoothness weight for yaw torque changes between steps. Reduces rotational jitter.")]
        public float wSmoothnessYaw = 0.2f;

        [Tooltip("Obstacle avoidance weight. Inverse-distance cost near obstacles; higher = wider berth.")]
        public float wObstacle = 10.0f;
        [Tooltip("Facing override weight. Steers the nose toward a specific angle (e.g. lead target in Attack). " +
                 "Only active when a facing override is set.")]
        public float wFacing = 1.0f;

        [Header("Tactical LOS")]
        [Tooltip("Line-of-sight cost weight. Penalizes positions where obstacles block the view to the enemy.")]
        public float wLos = 0f;
        [Tooltip("Exposure cost weight. Penalizes being in the enemy's forward weapon arc.")]
        public float wExposure = 0f;

        [Tooltip("Multiplier applied to state costs at the final rollout step. " +
                 "Encourages the solver to reach a good terminal state.")]
        public float terminalMultiplier = 10f;

        [Header("Obstacle Avoidance")]
        [Tooltip("Distance beyond an obstacle's radius at which the avoidance cost begins. " +
                 "Effectively inflates obstacles by this amount.")]
        public float obstacleThreshold = 5.0f;

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
                wPos = wPos,
                wVel = wVel,
                wYaw = wYaw,
                wYawRate = wYawRate,
                wEffort = wEffort,
                wSmoothnessThrust = wSmoothnessThrust,
                wSmoothnessStrafe = wSmoothnessStrafe,
                wSmoothnessYaw = wSmoothnessYaw,
                wObstacle = wObstacle,
                wFacing = wFacing,
                wLos = wLos,
                wExposure = wExposure,
                terminalMultiplier = terminalMultiplier,
                obstacleThreshold = obstacleThreshold,
                arrivalDistance = arrivalDistance,
                arrivalDistanceSq = arrivalDistance * arrivalDistance,
                arrivalVelScale = arrivalVelScale,
                arrivalYawScale = arrivalYawScale,
                facingTarget = facingTargetRad,
                goalMode = goalMode,
                desiredRange = desiredRange,
                rangeTolerance = rangeTolerance
            };
        }
    }
}
