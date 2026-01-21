using UnityEngine;

namespace AI.Steering.MPC
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
        public float maxSpeed;
        public float maxYawRate;
        public float forwardAcc;
        public float reverseAcc;
        public float strafeAcc;
        public float alphaMax;
        public float damping;

        // Weights
        public float wPos;
        public float wVel;
        public float wYaw;
        public float wYawRate;
        public float wEffort;
        public float wSmoothness;
        public float wObstacle;
        public float wFacing;
        public float terminalMultiplier;
        public float obstacleThreshold;
        
        // Facing override (radians, NaN if disabled)
        public float facingTarget;
    }
}
