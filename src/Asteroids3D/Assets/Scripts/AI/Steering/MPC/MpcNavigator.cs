using System;
using AI.Scanning;
using AI.Steering.MPC;
using Ships;
using Ships.Movement;
using UnityEngine;
namespace AI.Steering.MPC
{
    public partial class MpcNavigator : Navigator
    {
        [Header("Settings")]
        public MPC.Settings settings;

        [Header("Obstacle Avoidance")]
        public bool enableObstacleAvoidance = true;

        private Control[] bestSequence;
        private State[] predictedStates;
        private Config config;
        private float lastBestCost;

        public override void Initialize(Func<Ships.State> stateProvider, Dynamics dynamics, Scout scout)
        {
            base.Initialize(stateProvider, dynamics, scout);
            
            var horizon = settings.Horizon;
            bestSequence = new Control[horizon];
            predictedStates = new State[horizon];
            
            config = BuildConfig();
        }

        private Config BuildConfig()
        {
            return new Config
            {
                dt = settings.rolloutDt,
                horizon = settings.Horizon,
                
                wPos = settings.wPos,
                wVel = settings.wVel,
                wYaw = settings.wYaw,
                wYawRate = settings.wYawRate,
                wEffort = settings.wEffort,
                wSmoothness = settings.wSmoothness,
                wObstacle = settings.wObstacle,
                wFacing = settings.wFacing,
                terminalMultiplier = settings.terminalMultiplier,
                obstacleThreshold = settings.obstacleThreshold,
                facingTarget = float.NaN
            };
        }

        public override void GenerateNavCommands(Ships.State state, ref Command cmd)
        {
            if (!currentWaypoint.isValid)
            {
                cmd.TargetAngle = state.Kinematics.Yaw;
                return;
            }

            RefreshWeights();

            if (HasArrived(state.Kinematics))
            {
                cmd.Thrust = 0;
                cmd.Strafe = 0;
                cmd.YawTorque = 0;
                cmd.RotateToTarget = false;
                return;
            }

            var mpcState = ToMpcState(state.Kinematics);
            var (obstacles, count) = GetObstacles();
            StoreDebugObstacles(obstacles, count);

            ShiftWarmStart();
            lastBestCost = Sampler.Solve(mpcState, bestSequence, currentWaypoint.position, 
                obstacles, count, config, dynamics, settings.samples, settings.noiseStd, bestSequence);

            UpdatePredictedStates(mpcState);
            ApplyControl(ref cmd, bestSequence[0]);
        }



        private bool HasArrived(Kinematics kin)
        {
            var toGoal = currentWaypoint.position - kin.Pos;
            var posArrived = toGoal.sqrMagnitude < arriveRadius * arriveRadius;
            var velStopped = kin.Vel.sqrMagnitude < 0.1f;
            
            if (!posArrived || !velStopped) return false;
            
            // If facing override active, also check yaw
            if (!facingOverride) return true;
            var yawErr = Mathf.DeltaAngle(kin.Yaw, facingAngle);
            return !(Mathf.Abs(yawErr) > 5f);
        }

        private static MPC.State ToMpcState(Kinematics kin) => new()
        {
            pos = kin.Pos,
            vel = kin.Vel,
            yaw = kin.Yaw * Mathf.Deg2Rad,
            yawRate = kin.YawRate * Mathf.Deg2Rad
        };

        private (DetectedObstacle[] obstacles, int count) GetObstacles()
        {
            var obstacles = scout ? scout.Obstacles.DetectedBuffer : null;
            var count = enableObstacleAvoidance && scout ? scout.Obstacles.DetectedCount : 0;
            return (obstacles, count);
        }

        private void ShiftWarmStart() =>
            System.Array.Copy(bestSequence, 1, bestSequence, 0, bestSequence.Length - 1);

        private void UpdatePredictedStates(State initial)
        {
            var current = initial;
            for (var i = 0; i < predictedStates.Length; i++)
            {
                current = Model.Step(current, bestSequence[i], config, dynamics);
                predictedStates[i] = current;
            }
        }

        private static void ApplyControl(ref Ships.Command cmd, Control u)
        {
            cmd.Thrust = u.thrust;
            cmd.Strafe = u.strafe;
            cmd.YawTorque = u.yawTorque;
            cmd.RotateToTarget = false;
        }

        protected override void OnSetNavigationPoint(bool avoid) { }

        partial void StoreDebugObstacles(DetectedObstacle[] obstacles, int count);
        partial void RefreshWeights();
    }
}
