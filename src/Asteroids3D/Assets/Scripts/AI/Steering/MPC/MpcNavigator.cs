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
        private Control smoothedControl;
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
            var boostCooldown = state.boostCooldownRemaining;
            // If cooldown exceeds entire horizon, skip boost sampling to save candidate quality
            var boostProb = boostCooldown > settings.horizonSeconds
                ? 0f : settings.boostSampleProbability;

            using (EditorProfilingScope.Begin("MPC.MpcNavigator.Solve"))
            {
                lastBestCost = solver.Solve(mpcState, bestSequence,
                    scan, enableObstacleAvoidance,
                    GoalPos(), GoalVel(), enemyYaw, enemyYawRate,
                    projectileSpeed, config, dynamics,
                    settings.samples, settings.noiseStd, lastControl,
                    enemyDynamics,
                    boostCooldown, boostProb,
                    settings.eliteFraction,
                    NavigationTargetForSolver());
            }
#if UNITY_EDITOR
            sw.Stop();
            lastSolveTimeMs = (float)sw.Elapsed.TotalMilliseconds;
            lastCostBreakdown = EvaluateBreakdown(mpcState);
            LogSolverPerformanceIfNeeded();
#endif

            UpdatePredictedStates(mpcState);
            lastControl = bestSequence[0];

            var raw = bestSequence[0];
            var a = settings.controlSmoothing;
            smoothedControl = new Control
            {
                thrust = math.lerp(raw.thrust, smoothedControl.thrust, a),
                strafe = math.lerp(raw.strafe, smoothedControl.strafe, a),
                yawTorque = math.lerp(raw.yawTorque, smoothedControl.yawTorque, a),
            };

            // Scale controls by urgency — below relaxMin = coast, above relaxMax = full authority
            if (settings.relaxMax > settings.relaxMin)
            {
                var normalizedCost = lastBestCost / config.horizon;
                var t = math.saturate((normalizedCost - settings.relaxMin) / (settings.relaxMax - settings.relaxMin));
                var urgency = math.pow(t, settings.relaxCurve);
                smoothedControl.thrust *= urgency;
                smoothedControl.strafe *= urgency;
                smoothedControl.yawTorque *= urgency;
            }

            ApplyControl(ref cmd, smoothedControl, raw.boost);
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
        private float2 GoalVel() => new(currentWaypoint.velocity.x, currentWaypoint.velocity.y);

        private float2? NavigationTargetForSolver()
        {
            return navigationTarget.HasValue
                ? new float2(navigationTarget.Value.x, navigationTarget.Value.y)
                : (float2?)null;
        }

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

        private static void ApplyControl(ref Command cmd, Control u, float boost)
        {
            cmd.thrust = u.thrust;
            cmd.strafe = u.strafe;
            cmd.yawTorque = u.yawTorque;
            cmd.boost = boost;
        }

        private void RefreshConfig()
        {
            var facingRad = facingOverride ? facingAngle * Mathf.Deg2Rad : float.NaN;
            config = settings.ToConfig(facingRad, goalMode, goalDesiredRange, goalRangeTolerance);
            weightMultipliers.Apply(ref config);

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
        partial void LogSolverPerformanceIfNeeded();
    }
}
