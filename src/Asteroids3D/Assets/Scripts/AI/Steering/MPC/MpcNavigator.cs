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
        // Snapshot of the ship's MPC state at the moment of the last Solve(), used by the
        // editor to roll out individual candidate trajectories from the same starting point.
        public State lastInitialState;
#else
        private float lastBestCost;
#endif
        private Control lastControl;
        private float previousDt;
        private Control[] resampleBuffer;
        internal SolverBuffers solver;

        public override void Initialize(Func<Ships.Command.State> stateProvider, Dynamics dynamics, Scout scout)
        {
            base.Initialize(stateProvider, dynamics, scout);

            config = settings.ToConfig();
            config.maxBankAngleRad = dynamics.maxBankAngleRad;
            config.maxSpeedSq = dynamics.maxSpeed * dynamics.maxSpeed;
            config.maxYawRateSq = dynamics.maxYawRate * dynamics.maxYawRate;
            bestSequence = new Control[config.horizon];
            predictedStates = new State[config.horizon];
            solver = new SolverBuffers();
        }

        public override void GenerateNavCommands(Ships.Command.State state, ref Command cmd)
        {
            using var _ = EditorProfilingScope.Begin("MPC.MpcNavigator.GenerateNavCommands");
            if (!currentWaypoint.isValid || HasArrived(state.kinematics)) return;

            var mpcState = ToMpcState(state.kinematics);
            RefreshConfig(mpcState);
#if UNITY_EDITOR
            lastInitialState = mpcState;
#endif
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
                // Scale noise inversely with dt so state-space exploration stays constant
                var noiseStd = settings.noiseStd * settings.rolloutDt / config.dt;
                lastBestCost = solver.Solve(mpcState, bestSequence,
                    scan, enableObstacleAvoidance,
                    GoalPos(), GoalVel(), enemyYaw, enemyYawRate,
                    projectileSpeed, config, dynamics,
                    settings.samples, noiseStd, lastControl,
                    enemyDynamics,
                    boostCooldown, boostProb,
                    settings.eliteFraction);
            }
#if UNITY_EDITOR
            sw.Stop();
            lastSolveTimeMs = (float)sw.Elapsed.TotalMilliseconds;
            lastCostBreakdown = EvaluateBreakdown(mpcState);
            LogSolverPerformanceIfNeeded();
#endif

            UpdatePredictedStates(mpcState);
            RunComparisonRollouts(mpcState, scan);
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

        private void ShiftWarmStart()
        {
            var newDt = config.dt;
            var dtChanged = previousDt > 0f && math.abs(newDt - previousDt) > 1e-6f;
            previousDt = newDt;

            if (dtChanged)
                ResampleWarmStart(newDt);
            else
                ShiftSequenceForward();
        }

        private void ShiftSequenceForward()
        {
            if (bestSequence.Length > 1)
                System.Array.Copy(bestSequence, 1, bestSequence, 0, bestSequence.Length - 1);
        }

        private void ResampleWarmStart(float newDt)
        {
            var horizon = bestSequence.Length;
            if (resampleBuffer == null || resampleBuffer.Length != horizon)
                resampleBuffer = new Control[horizon];

            for (var i = 0; i < horizon; i++)
            {
                var fIdx = i * newDt / previousDt + 1f; // +1 accounts for the one-step shift
                resampleBuffer[i] = SampleSequenceAt(fIdx, horizon);
            }

            System.Array.Copy(resampleBuffer, bestSequence, horizon);
        }

        private Control SampleSequenceAt(float index, int horizon)
        {
            var lo = (int)index;
            if (lo >= horizon - 1) return bestSequence[horizon - 1];

            var a = bestSequence[lo];
            var b = bestSequence[math.min(lo + 1, horizon - 1)];
            var frac = index - lo;
            return new Control
            {
                thrust = math.lerp(a.thrust, b.thrust, frac),
                strafe = math.lerp(a.strafe, b.strafe, frac),
                yawTorque = math.lerp(a.yawTorque, b.yawTorque, frac),
                boost = frac < 0.5f ? a.boost : b.boost,
            };
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

        private void RefreshConfig(State mpcState)
        {
            var facingRad = facingOverride ? facingAngle * Mathf.Deg2Rad : float.NaN;
            config = settings.ToConfig(facingRad, goalMode, goalDesiredRange, goalRangeTolerance);
            config.maxBankAngleRad = dynamics.maxBankAngleRad;
            config.maxSpeedSq = dynamics.maxSpeed * dynamics.maxSpeed;
            config.maxYawRateSq = dynamics.maxYawRate * dynamics.maxYawRate;
            weightMultipliers.Apply(ref config);

            if (settings.adaptiveDtScale > 0f)
            {
                float t;
                if (goalMode == GoalMode.Flee)
                {
                    // Flee: scale by closing speed (how fast the gap is changing)
                    var toGoal = GoalPos() - mpcState.pos;
                    var dist = math.length(toGoal);
                    var dir = dist > 1e-4f ? toGoal / dist : float2.zero;
                    var relVel = mpcState.vel - GoalVel();
                    var closingSpeed = math.abs(math.dot(relVel, dir));
                    t = math.saturate(closingSpeed / dynamics.maxSpeed);
                }
                else
                {
                    // Other modes: scale by distance to goal
                    var dist = math.length(GoalPos() - mpcState.pos);
                    t = math.saturate(dist / settings.adaptiveDtRefDistance);
                }

                var rawScale = 1f + t * settings.adaptiveDtScale;
                // Bin to 0.25 increments so dt changes infrequently
                var scale = math.round(rawScale * 4f) / 4f;
                config.dt *= scale;
                config.invDt = config.dt > 0f ? 1f / config.dt : 0f;
            }

            if (bestSequence.Length != config.horizon)
            {
                bestSequence = new Control[config.horizon];
                predictedStates = new State[config.horizon];
            }
        }

        partial void RunComparisonRollouts(State mpcState, AI.Scanning.ObstacleScan scan);

        private void OnDestroy()
        {
            solver?.Dispose();
        }

        partial void StoreDebugObstacles(ObstacleScan scan);
        partial void LogSolverPerformanceIfNeeded();
    }
}
