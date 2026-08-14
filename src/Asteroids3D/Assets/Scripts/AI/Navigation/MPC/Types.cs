using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
namespace Movement.MPC
{
    [StructLayout(LayoutKind.Sequential)]
    public struct State
    {
        public float2 pos;
        public float2 vel;
        public float yaw;     // Radians
        public float yawRate; // Radians per second
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Control
    {
        public float thrust;
        public float strafe;
        public float yawTorque;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Config
    {
        public float dt;
        public float invDt;
        public int horizon;

        public float wYawRate;
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
        public float wFacingPrior;

        public float wPos;
        public float posWidth;

        public float wLane;
        public float laneRange;
        public float laneWidth;

        public float wObstacle;
        public float collisionPenalty;
        public float collisionSafetyMargin;

        public float maxBankAngleRad;
        public float maxSpeedSq;
        public float maxYawRateSq;
        public float shipRadius;
        public float maxLatAccel;    // Best-case lateral (strafe) acceleration (m/s²) for turn-away admissibility

        // Velocity-track weight — the tracking objective's gain.
        public float wVelTrack;
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

    [StructLayout(LayoutKind.Sequential)]
    public struct ObstacleData
    {
        public float2 position;
        public float radius;
        public float weight;
    }

    /// <summary>Read-only world data for cost evaluation; extend it to add inputs without changing Cost.Evaluate's signature or touching the Burst job.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CostInput
    {
        /// <summary>Commanded world-plane velocity (objective ‖s.vel − velocityReference‖²); NaN.x = no world command, the tracker drops.</summary>
        public float2 velocityReference;

        public NativeArray<ObstacleData> obstacles;
        public int obstacleCount;

        /// <summary>Tracked enemy position/velocity (linear fallback when no rollout exists) — referent 0 for sentence slots.</summary>
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

        /// <summary>The decision's intent sentence; default = nothing armed (legacy path, bit-unchanged).</summary>
        public IntentSentence sentence;

        /// <summary>Synthetic referents 1–3 for slots binding past the enemy — the sentence's own max (AIM/POS/VEL, one distinct rock each).</summary>
        public ReferentSnapshot referent1;
        public ReferentSnapshot referent2;
        public ReferentSnapshot referent3;
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

    // Unguarded: it is the solver rig's trace-row payload, which compiles into the player.
    public struct CostBreakdown
    {
        public float velocityTrack;
        public float facing;
        public float facingPrior;
        public float pos;
        public float lane;
        public float yawRate;
        public float obstacle;
        public float collision;
        public float momentum;
        public float effort;
        public float smoothness;
        public float total;

        public void Add(CostBreakdown other)
        {
            velocityTrack += other.velocityTrack;
            facing += other.facing;
            facingPrior += other.facingPrior;
            pos += other.pos;
            lane += other.lane;
            yawRate += other.yawRate;
            obstacle += other.obstacle;
            collision += other.collision;
            momentum += other.momentum;
            effort += other.effort;
            smoothness += other.smoothness;
            total += other.total;
        }
    }
}
