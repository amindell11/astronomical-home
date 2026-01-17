using AI.Computers;
using AI.Context;
using Game;
using Ships;
using Ships.Control;
using Ships.Movement;
using UnityEngine;

namespace AI
{
    public class MpcNavigator : Navigator
    {
        [Header("MPC Settings")]
        public float horizonSeconds = 1.5f;
        public float rolloutDt = 0.1f;
        public int samples = 128;
        public float noiseStd = 0.25f;

        [Header("MPC Weights")]
        public float wPos = 1.0f;
        public float wVel = 0.5f;
        public float wYaw = 0.5f;
        public float wYawRate = 0.1f;
        public float wEffort = 0.05f;
        public float wSmoothness = 0.1f;
        public float terminalMultiplier = 10f;

        private Steering.MpcControl[] bestSequence;
        private Steering.MpcState[] predictedStates;
        private Steering.MpcController.Config mpcConfig;
        private float lastBestCost;

        public override void Initialize(Ship ship, Sensors sensors)
        {
            base.Initialize(ship, sensors);
            
            var horizon = Mathf.CeilToInt(horizonSeconds / rolloutDt);
            bestSequence = new Steering.MpcControl[horizon];
            predictedStates = new Steering.MpcState[horizon];
            
            var mass = ship.Movement.Mass;
            var settings = ship.settings;
            
            mpcConfig = new Steering.MpcController.Config
            {
                dt = rolloutDt,
                horizon = horizon,
                maxSpeed = settings.maxSpeed,
                maxYawRate = settings.maxYawRate * Mathf.Deg2Rad,
                forwardAcc = settings.forwardAccel / mass,
                reverseAcc = settings.reverseAccel / mass,
                strafeAcc = settings.maxStrafeForce / mass,
                alphaMax = 10f, // Estimated yaw acceleration
                damping = 2f,   // Estimated yaw damping
                
                wPos = wPos,
                wVel = wVel,
                wYaw = wYaw,
                wYawRate = wYawRate,
                wEffort = wEffort,
                wSmoothness = wSmoothness,
                terminalMultiplier = terminalMultiplier
            };
        }

        public override void GenerateNavCommands(State state, ref Command cmd)
        {
            if (!ship || !currentWaypoint.isValid)
            {
                cmd.TargetAngle = state.Kinematics.Yaw;
                return;
            }

            // Refresh weights in case they were changed (e.g. in inspector or tests)
            mpcConfig.wPos = wPos;
            mpcConfig.wVel = wVel;
            mpcConfig.wYaw = wYaw;
            mpcConfig.wYawRate = wYawRate;
            mpcConfig.wEffort = wEffort;
            mpcConfig.wSmoothness = wSmoothness;
            mpcConfig.terminalMultiplier = terminalMultiplier;

            // 1. Arrive check
            var toGoal = currentWaypoint.position - state.Kinematics.Pos;
            if (toGoal.sqrMagnitude < arriveRadius * arriveRadius && state.Kinematics.Vel.sqrMagnitude < 0.1f)
            {
                cmd.Thrust = 0;
                cmd.Strafe = 0;
                cmd.YawTorque = 0;
                cmd.RotateToTarget = false;
                return;
            }

            // 2. Prepare solver state
            var mpcState = new Steering.MpcState
            {
                pos = state.Kinematics.Pos,
                vel = state.Kinematics.Vel,
                yaw = state.Kinematics.Yaw * Mathf.Deg2Rad,
                yawRate = state.Kinematics.YawRate * Mathf.Deg2Rad
            };

            // 3. Warm start: shift the best sequence
            System.Array.Copy(bestSequence, 1, bestSequence, 0, bestSequence.Length - 1);
            // bestSequence[bestSequence.Length - 1] remains same as previous frame's last command

            // 4. Solve
            lastBestCost = Steering.MpcController.Solve(mpcState, bestSequence, currentWaypoint.position, mpcConfig, samples, noiseStd, bestSequence);

            // 5. Update predicted states for visualization
            var current = mpcState;
            for (var i = 0; i < predictedStates.Length; i++)
            {
                current = Steering.MpcController.Step(current, bestSequence[i], mpcConfig);
                predictedStates[i] = current;
            }

            // 6. Apply first control
            var u0 = bestSequence[0];
            cmd.Thrust = u0.thrust;
            cmd.Strafe = u0.strafe;
            cmd.YawTorque = u0.yawTorque;
            cmd.RotateToTarget = false;
        }

        private void OnDrawGizmos()
        {
            if (predictedStates == null || predictedStates.Length == 0) return;

            Gizmos.color = Color.cyan;
            var prevPos = GamePlane.PlanePointToWorld(predictedStates[0].pos);
            for (var i = 1; i < predictedStates.Length; i++)
            {
                var pos = GamePlane.PlanePointToWorld(predictedStates[i].pos);
                Gizmos.DrawLine(prevPos, pos);
                Gizmos.DrawSphere(pos, 0.1f);
                prevPos = pos;
            }
            
            // Draw goal
            if (currentWaypoint.isValid)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(GamePlane.PlanePointToWorld(currentWaypoint.position), arriveRadius);
            }
        }
    }
}
