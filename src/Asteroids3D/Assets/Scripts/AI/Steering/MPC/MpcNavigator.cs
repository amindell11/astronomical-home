using System;
using AI;
using AI.Scanning;
using Movement.MPC;
using Ships;
using Ships.Command;
using Movement;
using UnityEngine;
using Unity.Profiling;
namespace Movement.MPC
{
    [DefaultExecutionOrder(-60)]
    public partial class MpcNavigator : Navigator
    {
        private static readonly ProfilerMarker GenerateNavCommandsMarker = new("MPC.MpcNavigator.GenerateNavCommands");
        private static readonly ProfilerMarker SolveMarker = new("MPC.MpcNavigator.Solve");
        private static readonly ProfilerMarker UpdatePredictedStatesMarker = new("MPC.MpcNavigator.UpdatePredictedStates");

        [Header("Settings")]
        public MPC.Settings settings;
 
        [Header("Obstacle Avoidance")]
        public bool enableObstacleAvoidance = true;

        private Control[] bestSequence;
        private State[] predictedStates;
        private Config config;
#if UNITY_EDITOR
        public float lastBestCost;
#else
        private float lastBestCost;
#endif
        private Control lastControl;
        
        public override void Initialize(Func<Ships.Command.State> stateProvider, Dynamics dynamics, Scout scout)
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
                wSmoothnessThrust = settings.wSmoothnessThrust,
                wSmoothnessStrafe = settings.wSmoothnessStrafe,
                wSmoothnessYaw = settings.wSmoothnessYaw,
                wObstacle = settings.wObstacle,
                wFacing = settings.wFacing,
                terminalMultiplier = settings.terminalMultiplier,
                obstacleThreshold = settings.obstacleThreshold,
                arrivalDistance = settings.arrivalDistance,
                arrivalVelScale = settings.arrivalVelScale,
                arrivalYawScale = settings.arrivalYawScale,
                facingTarget = float.NaN
            };
        }

        public override void GenerateNavCommands(Ships.Command.State state, ref Command cmd)
        {
            using var _ = GenerateNavCommandsMarker.Auto();

            if (!currentWaypoint.isValid || HasArrived(state.kinematics)) return;

            RefreshWeights();

            var mpcState = ToMpcState(state.kinematics);
            var scan = scout.ObstacleScan;
            StoreDebugObstacles(scan);

            ShiftWarmStart();

#if UNITY_EDITOR
            var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
            using (SolveMarker.Auto())
            {
                lastBestCost = Sampler.Solve(mpcState, bestSequence, currentWaypoint.position,
                    scan, config, dynamics, settings.samples, settings.noiseStd, bestSequence, lastControl);
            }
#if UNITY_EDITOR
            sw.Stop();
            
            lastSolveTimeMs = (float)sw.Elapsed.TotalMilliseconds;
            lastCostBreakdown = Sampler.EvaluateTrajectoryBreakdown(mpcState, bestSequence, 
                currentWaypoint.position, scan, config, dynamics, lastControl);
#endif

            using (UpdatePredictedStatesMarker.Auto())
            {
                UpdatePredictedStates(mpcState);
            }
            lastControl = bestSequence[0];
            ApplyControl(ref cmd, bestSequence[0]);
        }



        private bool HasArrived(Kinematics kin)
        {
            var toGoal = currentWaypoint.position - kin.pos;
            var posArrived = toGoal.sqrMagnitude < arriveRadius * arriveRadius;
            var velStopped = kin.vel.sqrMagnitude < 0.1f;
            
            if (!posArrived || !velStopped) return false;
            
            // If facing override active, also check yaw
            if (!facingOverride) return true;
            var yawErr = Mathf.DeltaAngle(kin.yaw, facingAngle);
            return !(Mathf.Abs(yawErr) > 5f);
        }

        private static MPC.State ToMpcState(Kinematics kin) => new()
        {
            pos = kin.pos,
            vel = kin.vel,
            yaw = kin.yaw * Mathf.Deg2Rad,
            yawRate = kin.yawRate * Mathf.Deg2Rad
        };

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

        private static void ApplyControl(ref Command cmd, Control u)
        {
            cmd.thrust = u.thrust;
            cmd.strafe = u.strafe;
            cmd.yawTorque = u.yawTorque;
        }

        private void RefreshWeights()
        {
            config.dt = settings.rolloutDt;
            var newHorizon = settings.Horizon;
            if (config.horizon != newHorizon)
            {
                bestSequence = new Control[newHorizon];
                predictedStates = new State[newHorizon];
                config.horizon = newHorizon;
            }
            config.wPos = settings.wPos;
            config.wVel = settings.wVel;
            config.wYaw = settings.wYaw;
            config.wYawRate = settings.wYawRate; // Boost damping to prevent tailspins
            config.wEffort = settings.wEffort;
            config.wSmoothnessThrust = settings.wSmoothnessThrust;
            config.wSmoothnessStrafe = settings.wSmoothnessStrafe;
            config.wSmoothnessYaw = settings.wSmoothnessYaw;
            config.wObstacle = settings.wObstacle;
            config.wFacing = settings.wFacing;
            config.terminalMultiplier = settings.terminalMultiplier;
            config.obstacleThreshold = settings.obstacleThreshold;
            config.arrivalDistance = settings.arrivalDistance;
            config.arrivalVelScale = settings.arrivalVelScale;
            config.arrivalYawScale = settings.arrivalYawScale;
            config.facingTarget = facingOverride ? facingAngle * Mathf.Deg2Rad : float.NaN;
        }

        partial void StoreDebugObstacles(ObstacleScan scan);
    }
}
