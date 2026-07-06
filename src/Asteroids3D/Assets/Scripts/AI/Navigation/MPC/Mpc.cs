using System;
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
        /// <summary>Scripted candidate sequences injected into the sample set (flattened,
        /// horizon-length each) — e.g. bank-through-gap primitives. Null/0 = none.</summary>
        public Control[] injectedControls;
        public int injectedCount;
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
    /// Model-predictive control solver. Owns the rollout buffers, warm-start, and per-tick
    /// config refresh. The <see cref="Navigator"/> drives it via
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
        private Control lastControl;
        private float lastBestCost;
        private State lastInitialState;

        public Mpc(MpcSettings settings, Dynamics dynamics)
        {
            this.settings = settings;
            this.dynamics = dynamics;

            config = settings.ToConfig();
            config.ApplyDynamics(in dynamics);
            bestSequence = new Control[config.horizon];
            predictedStates = new State[config.horizon];
            solver = new SolverBuffers();
        }

        /// <summary>Solve for one step and return the first control of the best sequence.</summary>
        public MpcResult Plan(in MpcInputs inputs)
        {
            var mpcState = ToMpcState(inputs.kinematics);
            RefreshConfig(in inputs);
            lastInitialState = mpcState;
            ShiftSequenceForward();

            var boostCooldown = inputs.boostCooldown;
            // If cooldown exceeds the entire horizon, skip boost sampling to save candidate quality.
            var boostProb = boostCooldown > settings.horizonSeconds ? 0f : settings.boostSampleProbability;

            using (EditorProfilingScope.Begin("MPC.Mpc.Solve"))
            {
                lastBestCost = solver.Solve(mpcState, bestSequence,
                    config, dynamics,
                    inputs.obstacleScan, inputs.enableObstacleAvoidance,
                    inputs.goalPos, inputs.goalVel,
                    inputs.enemyPos, inputs.enemyVel, inputs.enemyYaw, inputs.enemyYawRate,
                    inputs.enemyDynamics, inputs.projectileSpeed,
                    settings.samples, settings.noiseStd, settings.noiseKnots, lastControl,
                    boostCooldown, boostProb,
                    settings.eliteFraction,
                    inputs.injectedControls, inputs.injectedCount);
            }

            UpdatePredictedStates(mpcState);
            lastControl = bestSequence[0];

            var raw = bestSequence[0];
            return new MpcResult
            {
                thrust = raw.thrust,
                strafe = raw.strafe,
                yawTorque = raw.yawTorque,
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

        private void ShiftSequenceForward()
        {
            if (bestSequence.Length > 1)
                Array.Copy(bestSequence, 1, bestSequence, 0, bestSequence.Length - 1);
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

        private void RefreshConfig(in MpcInputs inputs)
        {
            config = settings.ToConfig(inputs.facingRad, inputs.goalMode, inputs.goalDesiredRange, inputs.goalRangeTolerance);
            config.ApplyDynamics(in dynamics);
            inputs.weightOverrides.Apply(ref config);

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
        internal State LastInitialState => lastInitialState;
        internal float LastBestCost => lastBestCost;
    }
}
