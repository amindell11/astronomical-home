using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
namespace Movement.MPC
{
    public enum GoalMode
    {
        Waypoint = 0,
        MaintainRange = 1,
        Flee = 2
    }

    public static class GoalModeExtensions
    {
        /// <summary>
        /// The enemy-relative goal modes. Their positional target IS the tracked enemy
        /// (the cost reads the predicted enemy track), as opposed to an absolute waypoint.
        /// This is the explicit "the goal is the enemy" indicator.
        /// </summary>
        public static bool IsEnemyAnchored(this GoalMode mode) =>
            mode == GoalMode.MaintainRange || mode == GoalMode.Flee;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct State
    {
        public float2 pos;
        public float2 vel;
        public float yaw;     // Radians
        public float yawRate; // Radians per second
        public float boostCooldownRemaining;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Control
    {
        public float thrust;
        public float strafe;
        public float yawTorque;
        public float boost;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Config
    {
        public float dt;
        public float invDt;
        public int horizon;

        // Navigation
        public float wPos;
        public float wVel;
        public float wClosing;
        public float closingFadeDistance;
        public float wYaw;
        public float wYawDistanceScale;
        public float wYawRate;
        public float positionCurve;
        public float positionSaturationDistance;
        public float terminalMultiplier;
        public float terminalCurve;

        // Control
        public float wEffort;
        public float wSmoothnessThrust;
        public float wSmoothnessStrafe;
        public float wSmoothnessYaw;
        public float wMomentum;

        // Tactical
        public float wFacing;
        public float facingTarget;
        public float facingWidth;
        public float wLos;
        public float wExposure;
        public float exposureWidth;
        public float wTangential;
        public float wMissDistance;

        // Obstacle
        public float wObstacle;
        public float wTerminal;
        public int terminalGridSize;
        public float terminalCellSize;
        public float terminalFallbackSpeed;

        // Arrival
        public float arrivalDistance;
        public float arrivalDistanceSq;
        public float arrivalVelScale;
        public float arrivalYawScale;

        // Boost
        public float wBoostEffort;

        // Ship geometry / dynamics (for cost normalization)
        public float maxBankAngleRad;
        public float maxSpeedSq;
        public float maxYawRateSq;
        public float shipRadius;
        public float maxDecel;

        // Goal
        public GoalMode goalMode;
        public float desiredRange;
        public float rangeTolerance;
    }

    /// <summary>
    /// Identifies a single MPC weight (or width) that a per-state override can scale.
    /// States list only the weights they actually change, and an absent entry means
    /// "use base as-is" (×1) — no all-zero serialization footgun.
    /// </summary>
    public enum MpcWeight
    {
        Pos, Vel, Yaw, YawRate,
        Effort, SmoothnessThrust, SmoothnessStrafe, SmoothnessYaw, Momentum,
        Facing, FacingWidth, Los, Exposure, ExposureWidth, Tangential, MissDistance,
        Obstacle, BoostEffort,
    }

    /// <summary>A single per-state multiplier applied to one base MPC weight.</summary>
    [System.Serializable]
    public struct WeightOverride
    {
        public MpcWeight weight;
        public float multiplier;
    }

    public static class WeightOverrideExtensions
    {
        /// <summary>
        /// Multiplies each listed weight into the config. Absent weights are left at their
        /// base value (×1). Runs managed-side in Navigator.RefreshConfig (before the
        /// Burst job), so the switch is free.
        /// </summary>
        public static void Apply(this WeightOverride[] overrides, ref Config cfg)
        {
            if (overrides == null) return;
            for (var i = 0; i < overrides.Length; i++)
            {
                var m = overrides[i].multiplier;
                switch (overrides[i].weight)
                {
                    case MpcWeight.Pos:               cfg.wPos *= m; break;
                    case MpcWeight.Vel:               cfg.wVel *= m; break;
                    case MpcWeight.Yaw:               cfg.wYaw *= m; break;
                    case MpcWeight.YawRate:           cfg.wYawRate *= m; break;
                    case MpcWeight.Effort:            cfg.wEffort *= m; break;
                    case MpcWeight.SmoothnessThrust:  cfg.wSmoothnessThrust *= m; break;
                    case MpcWeight.SmoothnessStrafe:  cfg.wSmoothnessStrafe *= m; break;
                    case MpcWeight.SmoothnessYaw:     cfg.wSmoothnessYaw *= m; break;
                    case MpcWeight.Momentum:          cfg.wMomentum *= m; break;
                    case MpcWeight.Facing:            cfg.wFacing *= m; break;
                    case MpcWeight.FacingWidth:       cfg.facingWidth *= m; break;
                    case MpcWeight.Los:               cfg.wLos *= m; break;
                    case MpcWeight.Exposure:          cfg.wExposure *= m; break;
                    case MpcWeight.ExposureWidth:     cfg.exposureWidth *= m; break;
                    case MpcWeight.Tangential:        cfg.wTangential *= m; break;
                    case MpcWeight.MissDistance:      cfg.wMissDistance *= m; break;
                    case MpcWeight.Obstacle:          cfg.wObstacle *= m; break;
                    case MpcWeight.BoostEffort:       cfg.wBoostEffort *= m; break;
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ObstacleData
    {
        public float2 position;
        public float radius;
        public float weight;
    }

    /// <summary>
    /// Read-only world data for cost evaluation. Extend this struct to add
    /// tactical inputs (enemy positions, cover points, LOS data, etc.)
    /// without changing Cost.Evaluate's signature or touching the Burst job.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CostInput
    {
        public float2 goalPos;
        public float2 goalVel;
        public NativeArray<ObstacleData> obstacles;
        public int obstacleCount;

        /// <summary>Tracked enemy position/velocity. Independent of the goal: an enemy-anchored
        /// goal reads the predicted enemy track for its position, while tactical costs always
        /// reference the enemy. (For the linear fallback when no rollout exists.)</summary>
        public float2 enemyPos;
        public float2 enemyVel;

        /// <summary>Enemy facing direction in radians (same convention as State.yaw). NaN if no enemy.</summary>
        public float enemyYaw;
        /// <summary>Enemy yaw rate in radians/second for projection over the horizon.</summary>
        public float enemyYawRate;
        /// <summary>Projectile speed for computing lead-target facing angle. 0 = no dynamic facing.</summary>
        public float projectileSpeed;

        /// <summary>Pre-rolled enemy trajectory over the horizon. If valid, overrides linear extrapolation.</summary>
        public NativeArray<State> enemyStates;
        /// <summary>Number of valid entries in enemyStates.</summary>
        public int enemyStateCount;

        /// <summary>Ship velocity at the start of the rollout. Used by momentum cost to reward maintaining direction.</summary>
        public float2 initialVel;

        public NativeArray<float> terminalCosts;
        public int terminalGridSize;
        public float terminalCellSize;
        public float2 terminalOrigin;
        public float2 terminalSource;
        public float terminalNominalSpeed;
        public int terminalHasSolution;

    }

    internal readonly partial struct EditorProfilingScope : System.IDisposable
    {
        public static EditorProfilingScope Begin(string sampleName)
        {
            BeginSample(sampleName);
            return new EditorProfilingScope();
        }

        public void Dispose()
        {
            EndSample();
        }

        static partial void BeginSample(string sampleName);
        static partial void EndSample();
    }
}
