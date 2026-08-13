using System;
using System.Collections.Generic;
using AI.Scanning;
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
        public float threatStepFraction;
        public float incumbentWinFraction;
        public float meanIncumbentRank;
        public float meanAbsEmitYawDeltaFromIncumbent;
    }

    /// <summary>Closed-loop solver rig: steps <see cref="Mpc.Plan"/> against the solver's own <see cref="Model"/> plant at the production 50 Hz cadence — no ship, scene, physics, or policy — so controller experiments read on the bench's churn metrics deterministically, in seconds. Referents move on scripted <see cref="RigLaw"/>s; obstacles enter through the production ConvertObstacles path.</summary>
    public static class MpcSolverRig
    {
        public static RigResult Run(MpcSettings settings, Dynamics dynamics, in RigScenario scenario, uint seed,
            List<RigTraceRow> trace = null)
        {
            if (!(scenario.posWidthOverride > 0f))
                return RunInner(settings, dynamics, in scenario, seed, trace);

            // Per-row posWidth runs on an in-memory clone; the asset file is never written (brief §blindsiders).
            var clone = UnityEngine.Object.Instantiate(settings);
            try
            {
                clone.posWidth = scenario.posWidthOverride;
                return RunInner(clone, dynamics, in scenario, seed, trace);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        private static RigResult RunInner(MpcSettings settings, Dynamics dynamics, in RigScenario scenario, uint seed,
            List<RigTraceRow> trace)
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
            var threatSteps = 0;

            var scan = BuildScan(scenario.obstacles);
            var enemyRunner = new LawRunner(scenario.enemyLaw);
            var referent1Runner = new LawRunner(scenario.referent1Law);
            var referent2Runner = new LawRunner(scenario.referent2Law);

            var warmupSteps = Mathf.RoundToInt(scenario.warmupSeconds / scenario.simDt);
            var totalSteps = warmupSteps + Mathf.RoundToInt(scenario.durationSeconds / scenario.simDt);
            // The sentence carries every command; a world-frame velocity reference is never issued.
            var noVelocityReference = new float2(float.NaN, 0f);
            var lastRange = float.NaN;

            for (var i = 0; i < totalSteps; i++)
            {
                var enemy = enemyRunner.Step(scenario.simDt, state.pos);
                var referent1 = referent1Runner.Step(scenario.simDt, state.pos);
                var referent2 = referent2Runner.Step(scenario.simDt, state.pos);

                var inputs = new MpcInputs
                {
                    kinematics = ToKinematics(state),
                    dt = scenario.simDt,
                    velocityReference = noVelocityReference,
                    facingRad = float.NaN,
                    enemyPos = enemy.pos,
                    enemyVel = enemy.vel,
                    enemyYaw = enemy.valid ? enemy.yaw : float.NaN,
                    enemyYawRate = 0f,
                    enemyDynamics = enemy.valid ? dynamics : default,
                    projectileSpeed = scenario.projectileSpeed,
                    sentence = scenario.intent,
                    referent1 = referent1,
                    referent2 = referent2,
                    obstacleScan = scan,
                    enableObstacleAvoidance = scan.count > 0,
                };
                var prevControl = mpc.LastControl;
                var result = mpc.Plan(in inputs);
                var applied = new Control { thrust = result.thrust, strafe = result.strafe, yawTorque = result.yawTorque };

                // Selection internals: candidate 0 is the noise-free incumbent (shifted warm start); rank 0 = it won argmin.
                var costs = mpc.Solver.Costs;
                var incumbentCost = costs[0];
                var incumbentRank = 0;
                for (var c = 1; c < mpc.Solver.LastSampleCount; c++)
                    if (costs[c] < incumbentCost) incumbentRank++;
                var emitYawDelta = result.yawTorque - mpc.Solver.Candidates[0].yawTorque;

                var cfg = mpc.Config;
                // The obstacle buffer is only valid until the next Solve — classify in the tick it was written (probe protocol).
                var hullRadius = cfg.shipRadius * Cost.BankProfileScale(applied.strafe, cfg) + cfg.collisionSafetyMargin;
                var underThreat = ControllerProbe.ObstacleThreat(state.pos, state.vel,
                    mpc.Solver.Obstacles, mpc.Solver.ObstacleCount, hullRadius, cfg.maxLatAccel);

                // The facing metric follows the AIM slot's referent; rows without an armed live AIM sample no facing error.
                var aimReferent = Resolve(scenario.intent.aim.referent, enemy, referent1, referent2);
                var hasAnchor = scenario.intent.aim.armed && aimReferent.valid;
                var anchorYawRad = hasAnchor
                    ? Cost.AnchorYaw(state.pos, aimReferent.pos, aimReferent.vel, scenario.projectileSpeed)
                    : float.NaN;
                var errorDeg = hasAnchor
                    ? Mathf.Abs(Mathf.DeltaAngle(
                        (anchorYawRad + scenario.intent.aim.offsetRad) * Mathf.Rad2Deg,
                        state.yaw * Mathf.Rad2Deg))
                    : float.NaN;

                lastRange = enemy.valid ? math.distance(state.pos, enemy.pos)
                    : referent1.valid ? math.distance(state.pos, referent1.pos)
                    : float.NaN;

                if (i >= warmupSteps)
                {
                    sampler.Sample(result.yawTorque, state.yawRate * Mathf.Rad2Deg, hasAnchor,
                        hasAnchor ? anchorYawRad : 0f, underThreat, scenario.simDt);
                    if (hasAnchor) facingErrorDeg.Add(errorDeg);
                    if (underThreat) threatSteps++;
                    if (incumbentRank == 0) incumbentWins++;
                    rankSum += incumbentRank;
                    absEmitYawDeltaSum += Mathf.Abs(emitYawDelta);
                }

                if (trace != null)
                {
                    // Applied-control step-0 breakdown against the exact CostInput this solve saw.
                    var costInput = mpc.Solver.BuildCostInput(noVelocityReference, enemy.pos, enemy.vel,
                        enemy.valid ? enemy.yaw : float.NaN, 0f, scenario.projectileSpeed,
                        state.vel, scenario.intent, referent1, referent2);
                    var breakdown = Cost.EvaluateBreakdown(state, applied, prevControl, costInput, cfg);
                    trace.Add(new RigTraceRow
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
                        range = lastRange,
                        underThreat = underThreat ? 1 : 0,
                        solveCost = result.cost,
                        incumbentRank = incumbentRank,
                        incumbentCost = incumbentCost,
                        emitYawDeltaFromIncumbent = emitYawDelta,
                        costVelocityTrack = breakdown.velocityTrack,
                        costFacing = breakdown.facing,
                        costFacingPrior = breakdown.facingPrior,
                        costPos = breakdown.pos,
                        costYawRate = breakdown.yawRate,
                        costObstacle = breakdown.obstacle,
                        costCollision = breakdown.collision,
                        costMomentum = breakdown.momentum,
                        costEffort = breakdown.effort,
                        costSmoothness = breakdown.smoothness,
                        costTotal = breakdown.total,
                    });
                }

                state = Model.Step(state, applied, plantConfig, dynamics);
            }

            // One more law sample lands the referents on the episode endpoint the final Model.Step reached.
            var finalEnemy = enemyRunner.Step(scenario.simDt, state.pos);
            var finalReferent1 = referent1Runner.Step(scenario.simDt, state.pos);
            lastRange = finalEnemy.valid ? math.distance(state.pos, finalEnemy.pos)
                : finalReferent1.valid ? math.distance(state.pos, finalReferent1.pos)
                : float.NaN;

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
                finalRange = lastRange,
                threatStepFraction = overall.steps > 0 ? threatSteps / (float)overall.steps : 0f,
                incumbentWinFraction = overall.steps > 0 ? incumbentWins / (float)overall.steps : 0f,
                meanIncumbentRank = overall.steps > 0 ? rankSum / (float)overall.steps : 0f,
                meanAbsEmitYawDeltaFromIncumbent = overall.steps > 0 ? absEmitYawDeltaSum / overall.steps : 0f,
            };
        }

        /// <summary>Synthetic circles become colliderless <see cref="DetectedObstacle"/>s so the solver ingests them through the production ConvertObstacles path — never hand-written solver-side rows.</summary>
        private static ObstacleScan BuildScan(RigCircle[] circles)
        {
            if (circles == null || circles.Length == 0) return default;
            var buffer = new DetectedObstacle[circles.Length];
            for (var i = 0; i < circles.Length; i++)
                buffer[i] = new DetectedObstacle(
                    GamePlane.PlanePointToWorld(new Vector2(circles[i].center.x, circles[i].center.y)),
                    circles[i].radius, collider: null);
            return new ObstacleScan(buffer, buffer.Length);
        }

        private static ReferentSnapshot Resolve(int referent, in ReferentSnapshot enemy,
            in ReferentSnapshot referent1, in ReferentSnapshot referent2)
            => referent == 1 ? referent1 : referent == 2 ? referent2 : enemy;

        /// <summary>Advances one scripted law per sim step. Closed-form kinds ignore the ship; Pursue integrates toward the ship's live position, deterministic inside the closed loop.</summary>
        private struct LawRunner
        {
            private readonly RigLaw law;
            private float t;
            private float2 pursuePos;
            private float2 pursueVel;
            private bool started;

            public LawRunner(RigLaw law)
            {
                this.law = law;
                t = 0f;
                pursuePos = law.p0;
                pursueVel = default;
                started = false;
            }

            public ReferentSnapshot Step(float dt, float2 shipPos)
            {
                switch (law.kind)
                {
                    case RigLawKind.Static:
                        return Snap(law.p0, default, law.yaw);
                    case RigLawKind.ConstantVelocity:
                    {
                        var snap = Snap(law.p0 + law.v0 * t, law.v0, HeadingOf(law.v0, law.yaw));
                        t += dt;
                        return snap;
                    }
                    case RigLawKind.Orbit:
                    {
                        var angle = law.phase + law.angularRate * t;
                        var pos = law.p0 + law.radius * new float2(math.cos(angle), math.sin(angle));
                        var vel = law.radius * law.angularRate * new float2(-math.sin(angle), math.cos(angle));
                        t += dt;
                        return Snap(pos, vel, HeadingOf(vel, law.yaw));
                    }
                    case RigLawKind.Pursue:
                    {
                        if (started)
                        {
                            var toShip = shipPos - pursuePos;
                            var dist = math.length(toShip);
                            pursueVel = dist > 1e-4f ? toShip / dist * law.v0.x : default;
                            pursuePos += pursueVel * dt;
                        }
                        started = true;
                        return Snap(pursuePos, pursueVel, HeadingOf(pursueVel, law.yaw));
                    }
                    default:
                        return default;
                }
            }

            private static ReferentSnapshot Snap(float2 pos, float2 vel, float yaw) =>
                new() { valid = true, pos = pos, vel = vel, yaw = yaw };

            /// <summary>Moving laws face their velocity (MPC convention, fwd = (-sin, cos)); at rest they hold the authored yaw.</summary>
            private static float HeadingOf(float2 vel, float restYaw) =>
                math.lengthsq(vel) > 1e-6f ? math.atan2(-vel.x, vel.y) : restYaw;
        }

        private static Kinematics ToKinematics(in State state) => new(
            new Vector2(state.pos.x, state.pos.y),
            new Vector2(state.vel.x, state.vel.y),
            state.yaw * Mathf.Rad2Deg,
            state.yawRate * Mathf.Rad2Deg,
            bank: 0f);
    }
}
