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

    /// <summary>Which live frame of the bound referent a slot's free parameters are expressed in: its position frame (world axes riding its position), its facing frame, or its velocity-direction frame.</summary>
    public enum ReferentFrame
    {
        Position = 0,
        Facing = 1,
        Velocity = 2,
    }

    /// <summary>AIM sentence slot: a facing offset around the referent's intercept anchor. Signed weight, CCW-positive offset (sign pins: doc/Glossary.md → anchored intent).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AimSlot
    {
        public bool armed;
        public float offsetRad;    // CCW offset from the intercept yaw
        public float weight;       // Signed authority × the config's wFacing ceiling
        public int referent;
    }

    /// <summary>POS sentence slot: a point at polar offset (r, θ) in the referent's chosen frame; the setpoint turns the point into a hold-ring around it (setpoint 0 = be at the point). Cost is (distance-to-point − setpoint)², saturating at posWidth.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PosSlot
    {
        public bool armed;
        public float offsetR;         // Meters from the referent along θ
        public float offsetThetaRad;  // CCW from the frame's forward (+Y world / referent nose / referent velocity)
        public float setpoint;        // Ring radius around the resolved point; 0 = be at the point
        public float weight;          // Signed authority × the config's wPos ceiling
        public int referent;
        public ReferentFrame frame;
    }

    /// <summary>VEL sentence slot: a polar velocity in the referent frame, relative to the referent's motion. vr &gt; 0 closes along +losHat, vt &gt; 0 orbits CCW.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VelSlot
    {
        public bool armed;
        public float radialSpeed;      // m/s; > 0 closes along +losHat
        public float tangentialSpeed;  // m/s; > 0 orbits CCW around the referent
        public float weight;           // Signed authority × the config's wVelTrack ceiling
        public int referent;
    }

    /// <summary>FIELD sentence slot: hazard-repulsion authority scaling the turn-away branch only — the collision penalty stays character-axis and un-zeroable. Unarmed = ×1 (today's character-ceiling shaping).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FieldSlot
    {
        public bool armed;
        public float weight;
    }

    /// <summary>An intent sentence: the decision-varying slice of the MPC cost as a fixed set of typed sentence slots — AIM/POS/VEL instance slots binding one referent each, FIELD a class slot binding none. The solver re-resolves every armed slot against live referent state each rollout step, so the command stays correct as the world moves (doc/Feature_Plans/Intent_Grammar.md). Referent 0 is the solver's bound enemy (rolled prediction stream); 1–2 are <see cref="CostInput"/>'s synthetic snapshots. Default (nothing armed) = the legacy world-frame path, bit-unchanged; the legacy anchored intent is the AIM+VEL degenerate sentence.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct IntentSentence
    {
        public AimSlot aim;
        public PosSlot pos;
        public VelSlot vel;
        public FieldSlot field;

        /// <summary>An armed slot at weight 0 still counts: "nothing matters" is a sentence, absence of one is not.</summary>
        public bool AnyArmed => aim.armed || pos.armed || vel.armed || field.armed;
    }

    /// <summary>A synthetic referent for sentence slots: a (pos, vel, yaw) snapshot the cost model extrapolates linearly in-rollout. Invalid = despawned — slots bound to it drop to weight 0 until the next decision.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ReferentSnapshot
    {
        public bool valid;
        public float2 pos;
        public float2 vel;
        public float yaw;    // Radians, MPC convention (fwd = (-sin, cos)); held constant in-rollout
    }

    /// <summary>Read-only world data for cost evaluation; extend it to add inputs without changing Cost.Evaluate's signature or touching the Burst job.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CostInput
    {
        /// <summary>Commanded world-plane velocity (objective ‖s.vel − velocityReference‖²). NaN.x = no world velocity command (a sentence-only objective) — the tracker drops instead of commanding a stop.</summary>
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

        /// <summary>The decision's intent sentence; default = nothing armed (legacy world-frame path, bit-unchanged).</summary>
        public IntentSentence sentence;

        /// <summary>Synthetic referents 1 and 2 for sentence slots that bind past the enemy.</summary>
        public ReferentSnapshot referent1;
        public ReferentSnapshot referent2;
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

    // Unguarded: the trajectory painter compiles into the player.
    public struct CostBreakdown
    {
        public float velocityTrack;
        public float facing;
        public float facingPrior;
        public float pos;
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
