using Unity.Collections;
using Unity.Mathematics;

namespace Movement.MPC
{
    public struct State
    {
        public float2 pos;
        public float2 vel;
        public float yaw;     // Radians
        public float yawRate; // Radians per second
    }

    public struct Control
    {
        public float thrust;
        public float strafe;
        public float yawTorque;
    }

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

        // Arrival Stabilization
        public float arrivalDistance;
        public float arrivalDistanceSq;
        public float arrivalVelScale;
        public float arrivalYawScale;

        // Facing override (radians, NaN if disabled)
        public float facingTarget;
    }

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
    public struct CostInput
    {
        public float2 goalPos;
        public NativeArray<ObstacleData> obstacles;
        public int obstacleCount;
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
