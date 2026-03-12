using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Serialization;

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

        // Weights
        public float wPos;
        public float wVel;
        public float wYaw;
        public float wYawRate;
        public float wEffort;
        public float wSmoothnessThrust;
        public float wSmoothnessStrafe;
        public float wSmoothnessYaw;
        public float wObstacle;
        public float wFacing;
        public float terminalMultiplier;
        public float obstacleThreshold;
        public float obstacleSpeedMargin;

        // Arrival Stabilization
        public float arrivalDistance;
        public float arrivalDistanceSq;
        public float arrivalVelScale;
        public float arrivalYawScale;

        // Facing override (radians, NaN if disabled)
        public float facingTarget;
        public float facingWidth;

        // Goal mode
        public GoalMode goalMode;
        public float desiredRange;
        public float rangeTolerance;

        // Tactical LOS
        public float wLos;
        public float wExposure;
        public float exposureWidth;

        // Tangential velocity (evasive lateral movement)
        public float wTangential;
    }

    /// <summary>
    /// Per-state weight multipliers. Each field scales the corresponding base weight
    /// from MpcSettings. Default (1.0) = use base as-is, 0 = disable, 2 = double.
    /// Width fields (facingWidth, exposureWidth) are absolute values, not multipliers.
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
        public float effort;
        public float smoothnessThrust;
        public float smoothnessStrafe;
        public float smoothnessYaw;
        public float obstacle;

        // Tactical
        public float facing;
        public float exposure;
        public float los;
        public float tangential;

        // Widths (absolute, not multiplied — 0 = use base)
        [FormerlySerializedAs("facingPower")]
        public float facingWidth;
        [FormerlySerializedAs("exposurePower")]
        public float exposureWidth;

        public static WeightMultipliers Default => new WeightMultipliers
        {
            pos = 1f, vel = 1f, yaw = 1f, yawRate = 1f,
            effort = 1f, smoothnessThrust = 1f, smoothnessStrafe = 1f, smoothnessYaw = 1f,
            obstacle = 1f,
            facing = 1f, exposure = 1f, los = 1f, tangential = 1f,
            facingWidth = 0f, exposureWidth = 0f,
        };

        public void Apply(ref Config cfg)
        {
            cfg.wPos *= pos;
            cfg.wVel *= vel;
            cfg.wYaw *= yaw;
            cfg.wYawRate *= yawRate;
            cfg.wEffort *= effort;
            cfg.wSmoothnessThrust *= smoothnessThrust;
            cfg.wSmoothnessStrafe *= smoothnessStrafe;
            cfg.wSmoothnessYaw *= smoothnessYaw;
            cfg.wObstacle *= obstacle;
            cfg.wFacing *= facing;
            cfg.wExposure *= exposure;
            cfg.wLos *= los;
            cfg.wTangential *= tangential;
            if (facingWidth > 0f) cfg.facingWidth = facingWidth;
            if (exposureWidth > 0f) cfg.exposureWidth = exposureWidth;
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
