using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
namespace Movement.MPC
{
    public enum GoalMode
    {
        Waypoint = 0,
        MaintainRange = 1,
        Flee = 2,
        // Commanded planar velocity instead of a position goal — the feasibility-tracker a learned goal-policy drives. Not enemy-anchored.
        VelocityReference = 3
    }

    public static class GoalModeExtensions
    {
        /// <summary>The enemy-relative goal modes, whose positional target is the tracked enemy rather than an absolute waypoint — the explicit "the goal is the enemy" indicator.</summary>
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

        public float wEffort;
        public float wSmoothnessThrust;
        public float wSmoothnessStrafe;
        public float wSmoothnessYaw;
        public float wMomentum;

        public float wFacing;
        public float facingTarget;
        public float facingWidth;
        public float wLos;
        public float wExposure;
        public float exposureWidth;
        public float wTangential;
        public float wMissDistance;

        public float wObstacle;
        public float collisionPenalty;
        public float collisionSafetyMargin;

        public float arrivalDistance;
        public float arrivalDistanceSq;
        public float arrivalVelScale;
        public float arrivalYawScale;

        public float wBoostEffort;

        public float maxBankAngleRad;
        public float maxSpeedSq;
        public float maxYawRateSq;
        public float shipRadius;
        public float maxLatAccel;    // Best-case lateral (strafe) acceleration (m/s²) for turn-away admissibility

        public GoalMode goalMode;
        public float desiredRange;
        public float rangeTolerance;

        // Authored combat tactics — on for the scripted controller, off in the velocity-tracker where the reward teaches those behaviors.
        public bool tacticalEnabled;

        // Velocity-track weight — the VelocityReference objective. Unused by other modes.
        public float wVelTrack;

        // Weight on the per-rollout terminal cost-to-go sample, in stage-cost units; 0 disables the hook.
        public float wTerminal;
    }

    public static class ConfigExtensions
    {
        /// <summary>Copies the dynamics-derived fields the cost model needs into the config — the single source of truth for config↔dynamics coupling.</summary>
        public static void ApplyDynamics(ref this Config cfg, in Movement.Dynamics dyn)
        {
            cfg.maxBankAngleRad = dyn.maxBankAngleRad;
            cfg.maxSpeedSq = dyn.maxSpeed * dyn.maxSpeed;
            cfg.maxYawRateSq = dyn.maxYawRate * dyn.maxYawRate;
            cfg.shipRadius = dyn.shipRadius;
            // Strafe force at zero speed — optimistic on purpose so the turn-away term under- not over-triggers.
            cfg.maxLatAccel = dyn.mass > 0f ? dyn.maxStrafeAcc / dyn.mass : dyn.maxStrafeAcc;
        }
    }

    /// <summary>Identifies a single MPC weight (or width) a per-state override can scale; states list only the weights they change (absent = base ×1), avoiding an all-zero serialization footgun.</summary>
    public enum MpcWeight
    {
        Pos, Vel, Yaw, YawRate,
        Effort, SmoothnessThrust, SmoothnessStrafe, SmoothnessYaw, Momentum,
        Facing, FacingWidth, Los, Exposure, ExposureWidth, Tangential, MissDistance,
        Obstacle, BoostEffort, Terminal,
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
        /// <summary>Multiplies each listed weight into the config (absent weights stay at base ×1). Runs managed-side before the Burst job, so the switch is free.</summary>
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
                    case MpcWeight.Terminal:          cfg.wTerminal *= m; break;
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

    /// <summary>Read-only world data for cost evaluation; extend it to add tactical inputs without changing Cost.Evaluate's signature or touching the Burst job.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CostInput
    {
        public float2 goalPos;
        public float2 goalVel;

        /// <summary>Commanded world-plane velocity for GoalMode.VelocityReference (objective ‖s.vel − velocityReference‖²); ignored by the position-goal modes.</summary>
        public float2 velocityReference;

        public NativeArray<ObstacleData> obstacles;
        public int obstacleCount;

        /// <summary>Tracked enemy position/velocity (linear fallback when no rollout exists), independent of the goal — tactical costs always reference the enemy.</summary>
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
        public int enemyStateCount;

        /// <summary>Ship velocity at the start of the rollout, the momentum cost's reference direction.</summary>
        public float2 initialVel;

        /// <summary>Cost-to-go field sampled once per rollout at the terminal state; isValid == 0 (the default) makes the hook contribute 0.</summary>
        public Field.TerminalFieldData terminalField;

    }

    internal readonly struct EditorProfilingScope : System.IDisposable
    {
        public static EditorProfilingScope Begin(string sampleName)
        {
#if UNITY_EDITOR
            UnityEngine.Profiling.Profiler.BeginSample(sampleName);
#endif
            return new EditorProfilingScope();
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            UnityEngine.Profiling.Profiler.EndSample();
#endif
        }
    }

#if UNITY_EDITOR
    public struct CostBreakdown
    {
        public float pos;
        public float vel;
        public float closing;
        public float heading;
        public float velocityTrack;
        public float facing;
        public float yawRate;
        public float obstacle;
        public float collision;
        public float los;
        public float exposure;
        public float tangential;
        public float missDistance;
        public float momentum;
        public float effort;
        public float boostEffort;
        public float smoothness;
        public float total;

        public void Add(CostBreakdown other)
        {
            pos += other.pos;
            vel += other.vel;
            closing += other.closing;
            heading += other.heading;
            velocityTrack += other.velocityTrack;
            facing += other.facing;
            yawRate += other.yawRate;
            obstacle += other.obstacle;
            collision += other.collision;
            los += other.los;
            exposure += other.exposure;
            tangential += other.tangential;
            missDistance += other.missDistance;
            momentum += other.momentum;
            effort += other.effort;
            boostEffort += other.boostEffort;
            smoothness += other.smoothness;
            total += other.total;
        }
    }
#endif
}
