using System;
using System.Collections.Generic;
using Movement;
using Movement.MPC;
using Unity.Mathematics;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Bench-comparable churn metrics over one rig episode; rates cover the post-warmup window.</summary>
    [Serializable]
    public struct RigResult
    {
        public float simSeconds;
        public int steps;
        public float meanAbsYawTorque;
        public float torqueReversalsPerSec;
        public float torqueDeadbandReversalsPerSec;
        public float noseReversalsPerSec;
        public float noseDeadbandReversalsPerSec;
        public float meanAbsYawRateDegPerSec;
        public float meanFacingErrorDeg;
        public float p90FacingErrorDeg;
        public float finalRange;
        public float incumbentWinFraction;
        public float meanIncumbentRank;
        public float meanAbsEmitYawDeltaFromIncumbent;
    }

    /// <summary>Closed-loop solver rig: steps <see cref="Mpc.Plan"/> against the solver's own <see cref="Model"/> plant at the production 50 Hz cadence — no ship, scene, physics, or policy — so controller experiments read on the bench's churn metrics deterministically, in seconds.</summary>
    public static class MpcSolverRig
    {
        public static RigResult Run(MpcSettings settings, Dynamics dynamics, in RigScenario scenario, uint seed,
            List<RigTraceRow> trace = null)
        {
            using var mpc = new Mpc(settings, dynamics, seed);

            // The plant integrates at sim rate; the solver's own config keeps rolloutDt.
            var plantConfig = settings.ToConfig();
            plantConfig.ApplyDynamics(in dynamics);
            plantConfig.dt = scenario.simDt;
            plantConfig.invDt = 1f / scenario.simDt;

            var state = new State { pos = scenario.startPos, yaw = scenario.startYawRad };
            var sampler = new ControllerSampler(ControllerProbe.DefaultTorqueDeadband,
                ControllerProbe.DefaultYawRateDeadbandDegPerSec);
            var facingErrorDeg = new List<float>();
            var incumbentWins = 0;
            var rankSum = 0L;
            var absEmitYawDeltaSum = 0f;

            var warmupSteps = Mathf.RoundToInt(scenario.warmupSeconds / scenario.simDt);
            var totalSteps = warmupSteps + Mathf.RoundToInt(scenario.durationSeconds / scenario.simDt);

            for (var i = 0; i < totalSteps; i++)
            {
                var inputs = new MpcInputs
                {
                    kinematics = ToKinematics(state),
                    dt = scenario.simDt,
                    // Sentence slots carry the command, mirroring Navigator.ApplyObjective for anchored objectives.
                    velocityReference = default,
                    facingRad = float.NaN,
                    enemyPos = scenario.enemyPos,
                    enemyVel = default,
                    enemyYaw = scenario.enemyYawRad,
                    enemyYawRate = 0f,
                    enemyDynamics = dynamics,
                    projectileSpeed = scenario.projectileSpeed,
                    sentence = scenario.intent,
                    obstacleScan = default,
                    enableObstacleAvoidance = false,
                };
                var result = mpc.Plan(in inputs);

                // Selection internals: candidate 0 is the noise-free incumbent (shifted warm start); rank 0 = it won argmin.
                var costs = mpc.Solver.Costs;
                var incumbentCost = costs[0];
                var incumbentRank = 0;
                for (var c = 1; c < mpc.Solver.LastSampleCount; c++)
                    if (costs[c] < incumbentCost) incumbentRank++;
                var emitYawDelta = result.yawTorque - mpc.Solver.Candidates[0].yawTorque;

                var anchorYawRad = Cost.AnchorYaw(state.pos, scenario.enemyPos, default, scenario.projectileSpeed);
                var errorDeg = Mathf.Abs(Mathf.DeltaAngle(
                    (anchorYawRad + scenario.intent.aim.offsetRad) * Mathf.Rad2Deg,
                    state.yaw * Mathf.Rad2Deg));

                if (i >= warmupSteps)
                {
                    sampler.Sample(result.yawTorque, state.yawRate * Mathf.Rad2Deg, hasAnchor: true, anchorYawRad,
                        underThreat: false, scenario.simDt);
                    facingErrorDeg.Add(errorDeg);
                    if (incumbentRank == 0) incumbentWins++;
                    rankSum += incumbentRank;
                    absEmitYawDeltaSum += Mathf.Abs(emitYawDelta);
                }

                trace?.Add(new RigTraceRow
                {
                    t = i * scenario.simDt,
                    posX = state.pos.x,
                    posY = state.pos.y,
                    velX = state.vel.x,
                    velY = state.vel.y,
                    yawDeg = state.yaw * Mathf.Rad2Deg,
                    yawRateDegPerSec = state.yawRate * Mathf.Rad2Deg,
                    thrust = result.thrust,
                    strafe = result.strafe,
                    yawTorque = result.yawTorque,
                    anchorYawDeg = anchorYawRad * Mathf.Rad2Deg,
                    facingErrorDeg = errorDeg,
                    solveCost = result.cost,
                    incumbentRank = incumbentRank,
                    incumbentCost = incumbentCost,
                    emitYawDeltaFromIncumbent = emitYawDelta,
                });

                var control = new Control { thrust = result.thrust, strafe = result.strafe, yawTorque = result.yawTorque };
                state = Model.Step(state, control, plantConfig, dynamics);
            }

            var overall = new ControllerBucket();
            overall.Add(sampler.threat);
            overall.Add(sampler.clear);
            var stats = overall.ToStats();
            return new RigResult
            {
                simSeconds = overall.seconds,
                steps = overall.steps,
                meanAbsYawTorque = stats.meanAbsYawTorque,
                torqueReversalsPerSec = stats.torqueReversalsPerSec,
                torqueDeadbandReversalsPerSec = stats.torqueDeadbandReversalsPerSec,
                noseReversalsPerSec = stats.noseReversalsPerSec,
                noseDeadbandReversalsPerSec = stats.noseDeadbandReversalsPerSec,
                meanAbsYawRateDegPerSec = stats.meanAbsYawRateDegPerSec,
                meanFacingErrorDeg = FacingSummary.Mean(facingErrorDeg),
                p90FacingErrorDeg = FacingSummary.Percentile(facingErrorDeg, 90),
                finalRange = math.distance(state.pos, scenario.enemyPos),
                incumbentWinFraction = overall.steps > 0 ? incumbentWins / (float)overall.steps : 0f,
                meanIncumbentRank = overall.steps > 0 ? rankSum / (float)overall.steps : 0f,
                meanAbsEmitYawDeltaFromIncumbent = overall.steps > 0 ? absEmitYawDeltaSum / overall.steps : 0f,
            };
        }

        private static Kinematics ToKinematics(in State state) => new(
            new Vector2(state.pos.x, state.pos.y),
            new Vector2(state.vel.x, state.vel.y),
            state.yaw * Mathf.Rad2Deg,
            state.yawRate * Mathf.Rad2Deg,
            bank: 0f);
    }
}
