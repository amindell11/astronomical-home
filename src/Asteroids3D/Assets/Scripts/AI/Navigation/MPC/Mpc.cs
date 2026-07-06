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

        /// <summary>Seeded gap-threading primitives, flat [p*horizon + step]. Injected into the
        /// CEM candidate set (iteration 0). Null / count 0 = pure CEM.</summary>
        public Control[] primitives;
        public int primitiveCount;
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
    /// Model-predictive control solver. Owns the rollout buffers, warm-start, and config.
    /// The <see cref="Navigator"/> drives it via <see cref="Plan"/> — it knows nothing of
    /// waypoints, intents, or the component graph.
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
            ApplyDynamics(ref config);
            bestSequence = new Control[config.horizon];
            predictedStates = new State[config.horizon];
            solver = new SolverBuffers();
        }

        /// <summary>Solve for one step and return the applied control (bestSequence[0]).</summary>
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
                // dt is constant (== rolloutDt), so noise needs no dt rescaling.
                lastBestCost = solver.Solve(mpcState, bestSequence,
                    config, dynamics,
                    inputs.obstacleScan, inputs.enableObstacleAvoidance,
                    inputs.goalPos, inputs.goalVel,
                    inputs.enemyPos, inputs.enemyVel, inputs.enemyYaw, inputs.enemyYawRate,
                    inputs.enemyDynamics, inputs.projectileSpeed,
                    settings.samples, settings.noiseStd, lastControl,
                    boostCooldown, boostProb,
                    settings.eliteFraction, settings.noiseKnots,
                    settings.cemIterations, settings.strafeSigmaFloor, settings.sigmaFloor,
                    settings.meanMomentum, inputs.primitives, inputs.primitiveCount);
            }

            UpdatePredictedStates(mpcState);
            lastControl = bestSequence[0];

            var applied = bestSequence[0];
            return new MpcResult
            {
                thrust = applied.thrust,
                strafe = applied.strafe,
                yawTorque = applied.yawTorque,
                boost = applied.boost,
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

        // dt is constant, so warm-starting is always a one-step forward shift.
        private void ShiftWarmStart() => ShiftSequenceForward();

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

        private void RefreshConfig(State mpcState, in MpcInputs inputs)
        {
            config = settings.ToConfig(inputs.facingRad, inputs.goalMode, inputs.goalDesiredRange, inputs.goalRangeTolerance);
            ApplyDynamics(ref config);
            inputs.weightOverrides.Apply(ref config);

            if (bestSequence.Length == config.horizon) return;
            bestSequence = new Control[config.horizon];
            predictedStates = new State[config.horizon];
        }

        // Copies ship geometry/dynamics constants onto the config. maxLatAccel is the best-case
        // lateral (strafe) acceleration (drag ignored = optimistic; the hard collision term is the
        // real safety net) used by the turn-away admissibility cost.
        private void ApplyDynamics(ref Config cfg) => ApplyDynamicsTo(ref cfg, dynamics);

        /// <summary>Copies ship geometry/dynamics constants onto a config. Shared with the gap
        /// primitive synthesizer so its forward-simulated rollouts use the same model constants.</summary>
        internal static void ApplyDynamicsTo(ref Config cfg, in Dynamics dynamics)
        {
            cfg.maxBankAngleRad = dynamics.maxBankAngleRad;
            cfg.maxSpeedSq = dynamics.maxSpeed * dynamics.maxSpeed;
            cfg.maxYawRateSq = dynamics.maxYawRate * dynamics.maxYawRate;
            cfg.shipRadius = dynamics.shipRadius;
            cfg.maxLatAccel = dynamics.mass > 0f ? dynamics.maxStrafeAcc / dynamics.mass : dynamics.maxStrafeAcc;
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
