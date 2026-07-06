using System;
using AI.Navigation.Field;
using AI.Scanning;
using Movement;
using Unity.Mathematics;
using UnityEngine;

namespace Movement.MPC
{
    /// <summary>Everything the MPC solver needs for one tick, assembled by the Navigator.</summary>
    public struct MpcInputs
    {
        public Kinematics kinematics;
        public float boostCooldown;
        public float2 goalPos;
        public float2 goalVel;
        public GoalMode goalMode;
        public float goalDesiredRange;
        public float goalRangeTolerance;
        public float facingRad;            // NaN = no facing override
        public float2 enemyPos;
        public float2 enemyVel;
        public float enemyYaw;
        public float enemyYawRate;
        public Dynamics enemyDynamics;
        public float projectileSpeed;
        public WeightOverride[] weightOverrides;
        public ObstacleScan obstacleScan;
        public bool enableObstacleAvoidance;
        public TerminalNavFieldSnapshot terminalField;
    }

    /// <summary>The control output of a single MPC solve.</summary>
    public struct MpcResult
    {
        public float thrust;
        public float strafe;
        public float yawTorque;
        public float boost;
        public float cost;
    }

    /// <summary>
    /// Model-predictive control solver. Owns the rollout buffers, warm-start, adaptive
    /// config, and control smoothing. The <see cref="Navigator"/> drives it via
    /// <see cref="Plan"/> — it knows nothing of waypoints, intents, or the component graph.
    /// </summary>
    public class Mpc : IDisposable
    {
        private readonly MpcSettings settings;
        private readonly Dynamics dynamics;
        private readonly SolverBuffers solver;

        private Config config;
        private Control[] bestSequence;
        private State[] predictedStates;
        private Control smoothedControl;
        private Control lastControl;
        private float previousDt;
        private Control[] resampleBuffer;
        private float lastBestCost;
        private State lastInitialState;

        public Mpc(MpcSettings settings, Dynamics dynamics)
        {
            this.settings = settings;
            this.dynamics = dynamics;

            config = settings.ToConfig();
            config.maxBankAngleRad = dynamics.maxBankAngleRad;
            config.maxSpeedSq = dynamics.maxSpeed * dynamics.maxSpeed;
            config.maxYawRateSq = dynamics.maxYawRate * dynamics.maxYawRate;
            bestSequence = new Control[config.horizon];
            predictedStates = new State[config.horizon];
            solver = new SolverBuffers();
        }

        /// <summary>Solve for one step and return the (smoothed, urgency-scaled) control.</summary>
        public MpcResult Plan(in MpcInputs inputs)
        {
            var mpcState = ToMpcState(inputs.kinematics);
            RefreshConfig(mpcState, in inputs);
            lastInitialState = mpcState;
            ShiftWarmStart();

            var boostCooldown = inputs.boostCooldown;
            // If cooldown exceeds the entire horizon, skip boost sampling to save candidate quality.
            var boostProb = boostCooldown > settings.horizonSeconds ? 0f : settings.boostSampleProbability;

            using (EditorProfilingScope.Begin("MPC.Mpc.Solve"))
            {
                // Scale noise inversely with dt so state-space exploration stays constant.
                var noiseStd = settings.noiseStd * settings.rolloutDt / config.dt;
                lastBestCost = solver.Solve(mpcState, bestSequence,
                    config, dynamics,
                    inputs.obstacleScan, inputs.enableObstacleAvoidance,
                    inputs.goalPos, inputs.goalVel,
                    inputs.enemyPos, inputs.enemyVel, inputs.enemyYaw, inputs.enemyYawRate,
                    inputs.enemyDynamics, inputs.projectileSpeed,
                    inputs.terminalField,
                    settings.samples, noiseStd, lastControl,
                    boostCooldown, boostProb,
                    settings.eliteFraction);
            }

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

            // Scale controls by urgency — below relaxMin = coast, above relaxMax = full authority.
            if (settings.relaxMax > settings.relaxMin)
            {
                var normalizedCost = lastBestCost / config.horizon;
                var t = math.saturate((normalizedCost - settings.relaxMin) / (settings.relaxMax - settings.relaxMin));
                var urgency = math.pow(t, settings.relaxCurve);
                smoothedControl.thrust *= urgency;
                smoothedControl.strafe *= urgency;
                smoothedControl.yawTorque *= urgency;
            }

            return new MpcResult
            {
                thrust = smoothedControl.thrust,
                strafe = smoothedControl.strafe,
                yawTorque = smoothedControl.yawTorque,
                boost = raw.boost,
                cost = lastBestCost,
            };
        }

        private static State ToMpcState(Kinematics kin) => new()
        {
            pos = new float2(kin.pos.x, kin.pos.y),
            vel = new float2(kin.vel.x, kin.vel.y),
            yaw = kin.yaw * Mathf.Deg2Rad,
            yawRate = kin.yawRate * Mathf.Deg2Rad
        };

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
                Array.Copy(bestSequence, 1, bestSequence, 0, bestSequence.Length - 1);
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

            Array.Copy(resampleBuffer, bestSequence, horizon);
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

        private void RefreshConfig(State mpcState, in MpcInputs inputs)
        {
            config = settings.ToConfig(inputs.facingRad, inputs.goalMode, inputs.goalDesiredRange, inputs.goalRangeTolerance);
            config.maxBankAngleRad = dynamics.maxBankAngleRad;
            config.maxSpeedSq = dynamics.maxSpeed * dynamics.maxSpeed;
            config.maxYawRateSq = dynamics.maxYawRate * dynamics.maxYawRate;
            inputs.weightOverrides.Apply(ref config);

            if (settings.adaptiveDtScale > 0f)
            {
                float t;
                if (inputs.goalMode == GoalMode.Flee)
                {
                    // Flee: scale by closing speed (how fast the gap is changing)
                    var toGoal = inputs.goalPos - mpcState.pos;
                    var dist = math.length(toGoal);
                    var dir = dist > 1e-4f ? toGoal / dist : float2.zero;
                    var relVel = mpcState.vel - inputs.goalVel;
                    var closingSpeed = math.abs(math.dot(relVel, dir));
                    t = math.saturate(closingSpeed / dynamics.maxSpeed);
                }
                else
                {
                    // Other modes: scale by distance to goal
                    var dist = math.length(inputs.goalPos - mpcState.pos);
                    t = math.saturate(dist / settings.adaptiveDtRefDistance);
                }

                var rawScale = 1f + t * settings.adaptiveDtScale;
                // Bin to 0.25 increments so dt changes infrequently
                var scale = math.round(rawScale * 4f) / 4f;
                config.dt *= scale;
                config.invDt = config.dt > 0f ? 1f / config.dt : 0f;
            }

            if (bestSequence.Length == config.horizon) return;
            bestSequence = new Control[config.horizon];
            predictedStates = new State[config.horizon];
        }

        public void Dispose() => solver?.Dispose();

        // ── Editor/debug accessors (read-only views of solver runtime state) ──
        internal MpcSettings Settings => settings;
        internal Dynamics Dynamics => dynamics;
        internal SolverBuffers Solver => solver;
        internal Config Config => config;
        internal Control[] BestSequence => bestSequence;
        internal State[] PredictedStates => predictedStates;
        internal Control LastControl => lastControl;
        internal Control SmoothedControl => smoothedControl;
        internal State LastInitialState => lastInitialState;
        internal float LastBestCost => lastBestCost;
    }
}
