using UnityEngine;

namespace AI.Steering.MPC
{
    [CreateAssetMenu(menuName = "AI/MPC Settings", fileName = "MpcSettings")]
    public class Settings : ScriptableObject
    {
        [Header("Solver")]
        public float horizonSeconds = 1.5f;
        public float rolloutDt = 0.1f;
        public int samples = 128;
        public float noiseStd = 0.25f;

        [Header("Weights")]
        public float wPos = 1.0f;
        public float wVel = 0.5f;
        public float wYaw = 0.5f;
        public float wYawRate = 0.1f;
        public float wEffort = 0.05f;
        
        [Header("Smoothness Weights")]
        public float wSmoothnessThrust = 0.5f;
        public float wSmoothnessStrafe = 5.0f; 
        public float wSmoothnessYaw = 0.2f;

        public float wObstacle = 10.0f;
        public float wFacing = 1.0f;
        public float terminalMultiplier = 10f;

        [Header("Obstacle Avoidance")]
        public float obstacleThreshold = 5.0f;

        [Header("Arrival Stabilization")]
        public float arrivalDistance = 3.0f;
        public float arrivalVelScale = 5.0f;
        public float arrivalYawScale = 0.1f;

        public int Horizon => Mathf.CeilToInt(horizonSeconds / rolloutDt);
    }
}
