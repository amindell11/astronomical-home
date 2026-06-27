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
        public float obstacleThreshold;
        public float obstacleSpeedMargin;
        public float obstacleFalloffCurve;
        public float obstacleClosingScale;
        public float obstacleClosingHalfSpeed;

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

        // Goal
        public GoalMode goalMode;
        public float desiredRange;
        public float rangeTolerance;
    }

    /// <summary>
    /// Per-state weight multipliers. Each field scales the corresponding base weight
    /// from MpcSettings. Default (1.0) = use base as-is, 0 = disable, 2 = double.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [System.Serializable]
    public struct WeightMultipliers
    {
        // Navigation
        public float pos;
        public float vel;
        public float yaw;
        public float yawRate;

        // Control
        public float effort;
        public float smoothnessThrust;
        public float smoothnessStrafe;
        public float smoothnessYaw;
        public float momentum;

        // Tactical
        public float facing;
        public float facingWidth;
        public float los;
        public float exposure;
        public float exposureWidth;
        public float tangential;
        public float missDistance;

        // Obstacle
        public float obstacle;

        // Boost
        public float boostEffort;

        public static WeightMultipliers Default => new WeightMultipliers
        {
            pos = 1f, vel = 1f, yaw = 1f, yawRate = 1f,
            effort = 1f, smoothnessThrust = 1f, smoothnessStrafe = 1f, smoothnessYaw = 1f, momentum = 1f,
            facing = 1f, facingWidth = 1f, los = 1f, exposure = 1f, exposureWidth = 1f, tangential = 1f, missDistance = 1f,
            obstacle = 1f,
            boostEffort = 1f,
        };

        public void Apply(ref Config cfg)
        {
            // Navigation
            cfg.wPos *= pos;
            cfg.wVel *= vel;
            cfg.wYaw *= yaw;
            cfg.wYawRate *= yawRate;
            // Control
            cfg.wEffort *= effort;
            cfg.wSmoothnessThrust *= smoothnessThrust;
            cfg.wSmoothnessStrafe *= smoothnessStrafe;
            cfg.wSmoothnessYaw *= smoothnessYaw;
            cfg.wMomentum *= momentum;
            // Tactical
            cfg.wFacing *= facing;
            cfg.facingWidth *= facingWidth;
            cfg.wLos *= los;
            cfg.wExposure *= exposure;
            cfg.exposureWidth *= exposureWidth;
            cfg.wTangential *= tangential;
            cfg.wMissDistance *= missDistance;
            // Obstacle
            cfg.wObstacle *= obstacle;
            // Boost
            cfg.wBoostEffort *= boostEffort;
        }
    }

    /// <summary>
    /// Identifies a single MPC weight (or width) that a per-state override can scale.
    /// Replaces the fixed <see cref="WeightMultipliers"/> mirror: states list only the
    /// weights they actually change, and an absent entry means "use base as-is" (×1) —
    /// no more all-zero serialization footgun.
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
        /// Multiplies each listed weight into the config. Mirrors the per-field mapping of
        /// the legacy <see cref="WeightMultipliers.Apply"/> exactly. Runs managed-side in
        /// MpcNavigator.RefreshConfig (before the Burst job), so the switch is free.
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

        /// <summary>
        /// Override target for position + heading costs from a high-level planner.
        /// When set (non-NaN), position cost becomes a Waypoint-style pull toward this point
        /// regardless of goalMode, and heading cost faces it. Range-band, Flee, and tactical
        /// costs continue to use goalPos. Set to (NaN, NaN) to disable.
        /// </summary>
        public float2 navigationTarget;
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
