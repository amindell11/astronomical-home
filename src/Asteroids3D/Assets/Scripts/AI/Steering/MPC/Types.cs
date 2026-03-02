using UnityEngine;

namespace Movement.MPC
{
    /// <summary>
    /// MPC state and control types, plus shared configuration.
    /// </summary>
    public struct State
    {
        public Vector2 pos;
        public Vector2 vel;
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
        public float arrivalVelScale;
        public float arrivalYawScale;
        
        // Facing override (radians, NaN if disabled)
        public float facingTarget;
    }
}
