using System;
using AI;
using AI.Scanning;
using Movement.MPC;
using Ships;
using Ships.Command;
using Movement;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
namespace Movement.MPC
{
    [DefaultExecutionOrder(-60)]
    public partial class MpcNavigator : Navigator
    {
        [Header("Settings")]
        public MPC.Settings settings;

        [Header("Obstacle Avoidance")]
        public bool enableObstacleAvoidance = true;

        private Control[] bestSequence;
        private Control[] candidateSequence;
        private State[] predictedStates;
        private Config config;
#if UNITY_EDITOR
        public float lastBestCost;
#else
        private float lastBestCost;
#endif
        private Control lastControl;

        // Burst solver buffers
        private NativeArray<Control> burstWarmStart;
        private NativeArray<Control> burstCandidates;
        private NativeArray<float> burstCosts;
        private NativeArray<ObstacleData> burstObstacles;
        private NativeArray<Control> burstResult;
        private bool burstAllocated;

        public override void Initialize(Func<Ships.Command.State> stateProvider, Dynamics dynamics, Scout scout)
        {
            base.Initialize(stateProvider, dynamics, scout);

            var horizon = settings.Horizon;
            bestSequence = new Control[horizon];
            candidateSequence = new Control[horizon];
            predictedStates = new State[horizon];

            config = BuildConfig();
            AllocateBurstBuffers(horizon, settings.samples);
        }

        private Config BuildConfig()
        {
            return new Config
            {
                dt = settings.rolloutDt,
                invDt = settings.rolloutDt > 0f ? 1f / settings.rolloutDt : 0f,
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
                arrivalDistanceSq = settings.arrivalDistance * settings.arrivalDistance,
                arrivalVelScale = settings.arrivalVelScale,
                arrivalYawScale = settings.arrivalYawScale,
                facingTarget = float.NaN
            };
        }

        public override void GenerateNavCommands(Ships.Command.State state, ref Command cmd)
        {
            using var _ = EditorProfilingScope.Begin("MPC.MpcNavigator.GenerateNavCommands");
            if (!currentWaypoint.isValid || HasArrived(state.kinematics)) return;

            RefreshWeights();

            var mpcState = ToMpcState(state.kinematics);
            var scan = scout.ObstacleScan;
            StoreDebugObstacles(scan);

            ShiftWarmStart();

#if UNITY_EDITOR
            var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
            using (EditorProfilingScope.Begin("MPC.MpcNavigator.Solve"))
            {
                SolveBurst(mpcState, scan);
            }
#if UNITY_EDITOR
            sw.Stop();

            lastSolveTimeMs = (float)sw.Elapsed.TotalMilliseconds;
            lastCostBreakdown = Sampler.EvaluateTrajectoryBreakdown(mpcState, bestSequence,
                currentWaypoint.position, scan, config, dynamics, lastControl);
#endif

            using (EditorProfilingScope.Begin("MPC.MpcNavigator.UpdatePredictedStates"))
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

        private void ShiftWarmStart()
        {
            if (bestSequence.Length > 1)
            {
                System.Array.Copy(bestSequence, 1, bestSequence, 0, bestSequence.Length - 1);
            }
        }

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
            config.invDt = settings.rolloutDt > 0f ? 1f / settings.rolloutDt : 0f;
            var newHorizon = settings.Horizon;
            if (config.horizon != newHorizon)
            {
                bestSequence = new Control[newHorizon];
                candidateSequence = new Control[newHorizon];
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
            config.arrivalDistanceSq = settings.arrivalDistance * settings.arrivalDistance;
            config.arrivalVelScale = settings.arrivalVelScale;
            config.arrivalYawScale = settings.arrivalYawScale;
            config.facingTarget = facingOverride ? facingAngle * Mathf.Deg2Rad : float.NaN;
        }

        private void SolveBurst(State mpcState, ObstacleScan scan)
        {
            var horizon = config.horizon;
            var samples = settings.samples;

            EnsureBurstBuffers(horizon, samples);

            // Copy warm start into native buffer
            for (var i = 0; i < horizon; i++)
                burstWarmStart[i] = bestSequence[i];

            // Convert obstacles to blittable data
            var obsCount = (scan.count > 0 && enableObstacleAvoidance) ? scan.count : 0;
            for (var i = 0; i < obsCount; i++)
            {
                var obs = scan.buffer[i];
                burstObstacles[i] = new ObstacleData
                {
                    position = new float2(obs.position.x, obs.position.y),
                    radius = obs.radius
                };
            }

            lastBestCost = BurstSampler.Solve(
                mpcState, burstWarmStart, burstCandidates, burstCosts,
                new float2(currentWaypoint.position.x, currentWaypoint.position.y),
                burstObstacles, obsCount,
                config, dynamics, samples, settings.noiseStd, lastControl, burstResult);

            // Copy result back to managed array
            for (var i = 0; i < horizon; i++)
                bestSequence[i] = burstResult[i];
        }

        private void AllocateBurstBuffers(int horizon, int samples)
        {
            DisposeBurstBuffers();
            burstWarmStart = new NativeArray<Control>(horizon, Allocator.Persistent);
            burstCandidates = new NativeArray<Control>(samples * horizon, Allocator.Persistent);
            burstCosts = new NativeArray<float>(samples, Allocator.Persistent);
            burstObstacles = new NativeArray<ObstacleData>(64, Allocator.Persistent);
            burstResult = new NativeArray<Control>(horizon, Allocator.Persistent);
            burstAllocated = true;
        }

        private void EnsureBurstBuffers(int horizon, int samples)
        {
            if (burstAllocated && burstWarmStart.Length == horizon && burstCosts.Length == samples)
                return;
            AllocateBurstBuffers(horizon, samples);
        }

        private void DisposeBurstBuffers()
        {
            if (!burstAllocated) return;
            burstWarmStart.Dispose();
            burstCandidates.Dispose();
            burstCosts.Dispose();
            burstObstacles.Dispose();
            burstResult.Dispose();
            burstAllocated = false;
        }

        private void OnDestroy()
        {
            DisposeBurstBuffers();
        }

        partial void StoreDebugObstacles(ObstacleScan scan);
    }
}
