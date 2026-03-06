using System;
using AI;
using AI.Scanning;
using Movement.MPC;
using Ships;
using Ships.Command;
using Movement;
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
        private State[] predictedStates;
        private Config config;
#if UNITY_EDITOR
        public float lastBestCost;
#else
        private float lastBestCost;
#endif
        private Control lastControl;
        private SolverBuffers solver;

        public override void Initialize(Func<Ships.Command.State> stateProvider, Dynamics dynamics, Scout scout)
        {
            base.Initialize(stateProvider, dynamics, scout);

            config = settings.ToConfig();
            bestSequence = new Control[config.horizon];
            predictedStates = new State[config.horizon];
            solver = new SolverBuffers();
        }

        public override void GenerateNavCommands(Ships.Command.State state, ref Command cmd)
        {
            using var _ = EditorProfilingScope.Begin("MPC.MpcNavigator.GenerateNavCommands");
            if (!currentWaypoint.isValid || HasArrived(state.kinematics)) return;

            RefreshConfig();

            var mpcState = ToMpcState(state.kinematics);
            var scan = scout.ObstacleScan;
            StoreDebugObstacles(scan);
            ShiftWarmStart();

#if UNITY_EDITOR
            var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
            using (EditorProfilingScope.Begin("MPC.MpcNavigator.Solve"))
            {
                lastBestCost = solver.Solve(mpcState, bestSequence,
                    scan, enableObstacleAvoidance,
                    GoalPos(), config, dynamics,
                    settings.samples, settings.noiseStd, lastControl);
            }
#if UNITY_EDITOR
            sw.Stop();
            lastSolveTimeMs = (float)sw.Elapsed.TotalMilliseconds;
            lastCostBreakdown = EvaluateBreakdown(mpcState);
#endif

            UpdatePredictedStates(mpcState);
            lastControl = bestSequence[0];
            ApplyControl(ref cmd, bestSequence[0]);
        }

        private bool HasArrived(Kinematics kin)
        {
            var toGoal = currentWaypoint.position - kin.pos;
            var posArrived = toGoal.sqrMagnitude < arriveRadius * arriveRadius;
            var velStopped = kin.vel.sqrMagnitude < 0.1f;

            if (!posArrived || !velStopped) return false;

            if (!facingOverride) return true;
            var yawErr = Mathf.DeltaAngle(kin.yaw, facingAngle);
            return !(Mathf.Abs(yawErr) > 5f);
        }

        private static MPC.State ToMpcState(Kinematics kin) => new()
        {
            pos = new float2(kin.pos.x, kin.pos.y),
            vel = new float2(kin.vel.x, kin.vel.y),
            yaw = kin.yaw * Mathf.Deg2Rad,
            yawRate = kin.yawRate * Mathf.Deg2Rad
        };

        private float2 GoalPos() => new(currentWaypoint.position.x, currentWaypoint.position.y);

        private void ShiftWarmStart()
        {
            if (bestSequence.Length > 1)
                System.Array.Copy(bestSequence, 1, bestSequence, 0, bestSequence.Length - 1);
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

        private void RefreshConfig()
        {
            var facingRad = facingOverride ? facingAngle * Mathf.Deg2Rad : float.NaN;
            config = settings.ToConfig(facingRad);

            if (bestSequence.Length != config.horizon)
            {
                bestSequence = new Control[config.horizon];
                predictedStates = new State[config.horizon];
            }
        }

        private void OnDestroy()
        {
            solver?.Dispose();
        }

        partial void StoreDebugObstacles(ObstacleScan scan);
    }
}
