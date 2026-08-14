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
        public float dt;               // sim seconds since the previous solve; drives the Fractional plan shift
        public float2 velocityReference;   // commanded planar velocity
        public float facingRad;            // NaN = no facing override
        public float2 enemyPos;
        public float2 enemyVel;
        public float enemyYaw;
        public float enemyYawRate;
        public Dynamics enemyDynamics;
        public float projectileSpeed;
        public IntentSentence sentence;
        public ReferentSnapshot referent1;
        public ReferentSnapshot referent2;
        public ObstacleScan obstacleScan;
        public bool enableObstacleAvoidance;
    }

    /// <summary>The control output of a single MPC solve.</summary>
    public struct MpcResult
    {
        public float thrust;
        public float strafe;
        public float yawTorque;
        public float cost;
    }

    /// <summary>Model-predictive control solver owning the rollout buffers, warm-start, and per-tick config refresh. The <see cref="Navigator"/> drives it via <see cref="Plan"/>; it knows nothing of intents or the component graph.</summary>
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

        public Mpc(MpcSettings settings, Dynamics dynamics, uint seed)
        {
            this.settings = settings;
            this.dynamics = dynamics;

            config = settings.ToConfig();
            config.ApplyDynamics(in dynamics);
            bestSequence = new Control[config.horizon];
            predictedStates = new State[config.horizon];
            solver = new SolverBuffers(seed);
        }

        /// <summary>Solve for one step and return the first control of the best sequence.</summary>
        public MpcResult Plan(in MpcInputs inputs)
        {
            var mpcState = ToMpcState(inputs.kinematics);
            RefreshConfig(in inputs);
            ApplyErrorRelativePosWidth(in inputs, mpcState);
            lastInitialState = mpcState;

            // Slide the warm start's time origin forward by dt so its plan clock tracks sim
            // time at solve rate: whole slots first, then the ZOH-faithful fractional resample.
            var remaining = inputs.dt;
            while (remaining >= settings.rolloutDt)
            {
                ShiftSequenceForward();
                remaining -= settings.rolloutDt;
            }
            FractionalShift(remaining / settings.rolloutDt);

            using (EditorProfilingScope.Begin("MPC.Mpc.Solve"))
            {
                lastBestCost = solver.Solve(mpcState, bestSequence,
                    config, dynamics,
                    inputs.obstacleScan, inputs.enableObstacleAvoidance, settings.multiSphereObstacles,
                    inputs.velocityReference,
                    inputs.enemyPos, inputs.enemyVel, inputs.enemyYaw, inputs.enemyYawRate,
                    inputs.enemyDynamics, inputs.projectileSpeed, inputs.sentence,
                    inputs.referent1, inputs.referent2,
                    settings.samples, settings.noiseStd, settings.noiseKnots, lastControl,
                    settings.eliteFraction);
            }

            UpdatePredictedStates(mpcState);
            lastControl = bestSequence[0];

            var raw = bestSequence[0];
            return new MpcResult
            {
                thrust = raw.thrust,
                strafe = raw.strafe,
                yawTorque = raw.yawTorque,
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

        // A dt-shifted slot spans (1−α) of old slot i plus α of slot i+1; the time-weighted
        // average IS the exact resample of a zero-order-hold plan. Tail slot holds. Repeated
        // application diffuses plan features (binomial blend, mean phase exact); measured
        // immaterial vs a diffusion-free shift on the rig (2026-08-11, PR #389 P1 experiment).
        private void FractionalShift(float alpha)
        {
            if (alpha <= 0f) return;
            for (var i = 0; i < bestSequence.Length - 1; i++)
            {
                var a = bestSequence[i];
                var b = bestSequence[i + 1];
                bestSequence[i] = new Control
                {
                    thrust = math.lerp(a.thrust, b.thrust, alpha),
                    strafe = math.lerp(a.strafe, b.strafe, alpha),
                    yawTorque = math.lerp(a.yawTorque, b.yawTorque, alpha),
                };
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

        // POS width is error-relative per solve (Intent_Grammar.md §Stage C, fork 3): widened
        // from the initial ring error so reach keeps a gradient; posWidth stays the settle floor.
        private void ApplyErrorRelativePosWidth(in MpcInputs inputs, State state)
        {
            var probe = new CostInput
            {
                enemyPos = inputs.enemyPos,
                enemyVel = inputs.enemyVel,
                enemyYaw = inputs.enemyYaw,
                enemyYawRate = inputs.enemyYawRate,
                sentence = inputs.sentence,
                referent1 = inputs.referent1,
                referent2 = inputs.referent2,
            };
            config.posWidth = Cost.EffectivePosWidth(state, in probe, in config, settings.posWidthSlope);
        }

        private void RefreshConfig(in MpcInputs inputs)
        {
            config = settings.ToConfig(inputs.facingRad);
            config.ApplyDynamics(in dynamics);

            if (bestSequence.Length == config.horizon) return;
            bestSequence = new Control[config.horizon];
            predictedStates = new State[config.horizon];
        }

        public void Dispose() => solver?.Dispose();

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
