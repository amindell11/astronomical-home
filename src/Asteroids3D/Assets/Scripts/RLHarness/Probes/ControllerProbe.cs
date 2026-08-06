using System;
using System.Collections.Generic;
using System.IO;
using AI;
using Movement;
using Movement.MPC;
using Ships;
using Ships.Command;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Nose/torque metrics for one obstacle-threat class; per-sec rates are over this class's sampled seconds.</summary>
    [Serializable]
    public struct ControllerBucketStats
    {
        public int steps;
        public float meanAbsYawTorque;
        public float torqueReversalsPerSec;
        public float torqueDeadbandReversalsPerSec;
        public float noseReversalsPerSec;
        public float noseDeadbandReversalsPerSec;
        public float meanAbsYawRateDegPerSec;
    }

    /// <summary>One controller-probe episode's applied-control signals: yaw torque and nose reversals (strict + deadband-hysteresis), the anchor's own angular velocity, and the obstacle-threat split. Zero-step episodes zero-fill.</summary>
    [Serializable]
    public struct ControllerRow
    {
        public string schema;
        public int seed;
        public int episodeIndex;
        public string opponent;
        public string outcome;
        public float simSeconds;
        public int steps;
        public float meanAbsYawTorque;
        public float torqueReversalsPerSec;
        public float torqueDeadbandReversalsPerSec;
        public float noseReversalsPerSec;
        public float noseDeadbandReversalsPerSec;
        public float meanAbsYawRateDegPerSec;
        public float meanAnchorRateDegPerSec;
        public float p90AnchorRateDegPerSec;
        public int anchorSamples;
        public float threatStepFraction;
        public ControllerBucketStats threat;
        public ControllerBucketStats clear;

        public const string SchemaId = "rl-controller-probe-v1";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }

    /// <summary>Raw per-obstacle-threat-class accumulator; reversal counts land in the bucket of the step that completed the flip.</summary>
    internal sealed class ControllerBucket
    {
        public int steps;
        public float seconds;
        public float absTorqueSum;
        public float absYawRateSum;
        public int torqueReversals;
        public int torqueDeadbandReversals;
        public int noseReversals;
        public int noseDeadbandReversals;

        public void Add(ControllerBucket other)
        {
            steps += other.steps;
            seconds += other.seconds;
            absTorqueSum += other.absTorqueSum;
            absYawRateSum += other.absYawRateSum;
            torqueReversals += other.torqueReversals;
            torqueDeadbandReversals += other.torqueDeadbandReversals;
            noseReversals += other.noseReversals;
            noseDeadbandReversals += other.noseDeadbandReversals;
        }

        public ControllerBucketStats ToStats() => new()
        {
            steps = steps,
            meanAbsYawTorque = steps > 0 ? absTorqueSum / steps : 0f,
            torqueReversalsPerSec = seconds > 0f ? torqueReversals / seconds : 0f,
            torqueDeadbandReversalsPerSec = seconds > 0f ? torqueDeadbandReversals / seconds : 0f,
            noseReversalsPerSec = seconds > 0f ? noseReversals / seconds : 0f,
            noseDeadbandReversalsPerSec = seconds > 0f ? noseDeadbandReversals / seconds : 0f,
            meanAbsYawRateDegPerSec = steps > 0 ? absYawRateSum / steps : 0f,
        };
    }

    /// <summary>One opponent label's raw samples pooled across episodes, so summary percentiles never average per-episode stats.</summary>
    internal sealed class ControllerPool
    {
        public readonly ControllerBucket threat = new();
        public readonly ControllerBucket clear = new();
        public readonly List<float> anchorRateDegPerSec = new();
        public int episodes;
        public float simSeconds;
    }

    /// <summary>Per-opponent aggregate over a controller-probe run, pooled across all episodes' samples.</summary>
    [Serializable]
    public struct ControllerSummary
    {
        public string schema;
        public string opponent;
        public int episodes;
        public int steps;
        public float meanEpisodeSeconds;
        public float meanAbsYawTorque;
        public float torqueReversalsPerSec;
        public float torqueDeadbandReversalsPerSec;
        public float noseReversalsPerSec;
        public float noseDeadbandReversalsPerSec;
        public float meanAbsYawRateDegPerSec;
        public float meanAnchorRateDegPerSec;
        public float p90AnchorRateDegPerSec;
        public int anchorSamples;
        public float threatStepFraction;
        public ControllerBucketStats threat;
        public ControllerBucketStats clear;

        public const string SchemaId = "rl-controller-probe-summary-v1";

        internal static ControllerSummary Summarize(string opponent, ControllerPool pool)
        {
            var overall = new ControllerBucket();
            overall.Add(pool.threat);
            overall.Add(pool.clear);
            return new ControllerSummary
            {
                schema = SchemaId,
                opponent = opponent,
                episodes = pool.episodes,
                steps = overall.steps,
                meanEpisodeSeconds = pool.episodes > 0 ? pool.simSeconds / pool.episodes : 0f,
                meanAbsYawTorque = overall.steps > 0 ? overall.absTorqueSum / overall.steps : 0f,
                torqueReversalsPerSec = pool.simSeconds > 0f ? overall.torqueReversals / pool.simSeconds : 0f,
                torqueDeadbandReversalsPerSec =
                    pool.simSeconds > 0f ? overall.torqueDeadbandReversals / pool.simSeconds : 0f,
                noseReversalsPerSec = pool.simSeconds > 0f ? overall.noseReversals / pool.simSeconds : 0f,
                noseDeadbandReversalsPerSec = pool.simSeconds > 0f ? overall.noseDeadbandReversals / pool.simSeconds : 0f,
                meanAbsYawRateDegPerSec = overall.steps > 0 ? overall.absYawRateSum / overall.steps : 0f,
                meanAnchorRateDegPerSec = FacingSummary.Mean(pool.anchorRateDegPerSec),
                p90AnchorRateDegPerSec = FacingSummary.Percentile(pool.anchorRateDegPerSec, 90),
                anchorSamples = pool.anchorRateDegPerSec.Count,
                threatStepFraction = overall.steps > 0 ? (float)pool.threat.steps / overall.steps : 0f,
                threat = pool.threat.ToStats(),
                clear = pool.clear.ToStats(),
            };
        }
    }

    /// <summary>Per-fixed-step sampler for one controller-probe episode, fed already-observed values (applied yaw torque, nose yaw rate, the recomputed anchor yaw, the obstacle-threat class) so the reversal/wrap/split math stays test-drivable without a live solver.</summary>
    public sealed class ControllerSampler
    {
        private readonly float torqueDeadband;
        private readonly float yawRateDeadbandDegPerSec;

        internal readonly ControllerBucket threat = new();
        internal readonly ControllerBucket clear = new();
        internal readonly List<float> anchorRateDegPerSec = new();

        private int torqueSign;
        private int torqueDeadbandSign;
        private int yawRateSign;
        private int yawRateDeadbandSign;
        private bool haveAnchor;
        private float prevAnchorYawRad;

        public ControllerSampler(float torqueDeadband, float yawRateDeadbandDegPerSec)
        {
            this.torqueDeadband = torqueDeadband;
            this.yawRateDeadbandDegPerSec = yawRateDeadbandDegPerSec;
        }

        public void Sample(float yawTorque, float yawRateDegPerSec, bool hasAnchor, float anchorYawRad,
            bool underThreat, float dt)
        {
            var bucket = underThreat ? threat : clear;
            bucket.steps++;
            bucket.seconds += dt;
            bucket.absTorqueSum += Mathf.Abs(yawTorque);
            bucket.absYawRateSum += Mathf.Abs(yawRateDegPerSec);
            if (FacingSampler.FlippedStrictly(yawTorque, ref torqueSign)) bucket.torqueReversals++;
            if (FlippedPastDeadband(yawTorque, torqueDeadband, ref torqueDeadbandSign))
                bucket.torqueDeadbandReversals++;
            if (FacingSampler.FlippedStrictly(yawRateDegPerSec, ref yawRateSign)) bucket.noseReversals++;
            if (FlippedPastDeadband(yawRateDegPerSec, yawRateDeadbandDegPerSec, ref yawRateDeadbandSign))
                bucket.noseDeadbandReversals++;

            if (hasAnchor)
            {
                if (haveAnchor && dt > 0f)
                    anchorRateDegPerSec.Add(
                        Mathf.Abs(Cost.WrapRadians(anchorYawRad - prevAnchorYawRad)) / dt * Mathf.Rad2Deg);
                prevAnchorYawRad = anchorYawRad;
            }
            // An anchorless tick breaks the pair: the next delta must not span the gap.
            haveAnchor = hasAnchor;
        }

        /// <summary>Hysteresis reversal: counts only when |value| clears the deadband opposite the last cleared sign; sub-deadband samples keep the armed sign. Deadband 0 degrades to the strict rule.</summary>
        internal static bool FlippedPastDeadband(float value, float deadband, ref int armedSign)
        {
            if (!(Mathf.Abs(value) > deadband)) return false;
            var next = value > 0f ? 1 : -1;
            var flipped = armedSign == -next;
            armedSign = next;
            return flipped;
        }

        public ControllerRow ToRow(in EpisodeResult result, string opponent)
        {
            var overall = new ControllerBucket();
            overall.Add(threat);
            overall.Add(clear);
            return new ControllerRow
            {
                schema = ControllerRow.SchemaId,
                seed = result.spec.runSeed,
                episodeIndex = result.episodeIndex,
                opponent = opponent,
                outcome = result.outcome,
                simSeconds = result.simSeconds,
                steps = overall.steps,
                meanAbsYawTorque = overall.steps > 0 ? overall.absTorqueSum / overall.steps : 0f,
                torqueReversalsPerSec = result.simSeconds > 0f ? overall.torqueReversals / result.simSeconds : 0f,
                torqueDeadbandReversalsPerSec =
                    result.simSeconds > 0f ? overall.torqueDeadbandReversals / result.simSeconds : 0f,
                noseReversalsPerSec = result.simSeconds > 0f ? overall.noseReversals / result.simSeconds : 0f,
                noseDeadbandReversalsPerSec =
                    result.simSeconds > 0f ? overall.noseDeadbandReversals / result.simSeconds : 0f,
                meanAbsYawRateDegPerSec = overall.steps > 0 ? overall.absYawRateSum / overall.steps : 0f,
                meanAnchorRateDegPerSec = FacingSummary.Mean(anchorRateDegPerSec),
                p90AnchorRateDegPerSec = FacingSummary.Percentile(anchorRateDegPerSec, 90),
                anchorSamples = anchorRateDegPerSec.Count,
                threatStepFraction = overall.steps > 0 ? (float)threat.steps / overall.steps : 0f,
                threat = threat.ToStats(),
                clear = clear.ToStats(),
            };
        }

        internal void DrainInto(ControllerPool pool, in EpisodeResult result)
        {
            pool.episodes++;
            pool.threat.Add(threat);
            pool.clear.Add(clear);
            pool.anchorRateDegPerSec.AddRange(anchorRateDegPerSec);
            pool.simSeconds += result.simSeconds;
        }
    }

    /// <summary>The MPC-retune controller instrument: per fixed step it reads the measured agent's applied control off the live solver (<see cref="Mpc.LastControl"/>), recomputes the anchor yaw from both ships' kinematics, classifies the step as obstacle threat or clear against the solver's live obstacle buffer, and feeds one <see cref="ControllerSampler"/> per episode; per-opponent pooled aggregates land in the summary sidecar.</summary>
    public sealed class ControllerProbe : ISessionProbe
    {
        public const string ProbeName = "controller";
        public const string YawRateDeadbandKey = "deadbandDegPerSec";
        public const string TorqueDeadbandKey = "torqueDeadband";
        internal const float DefaultYawRateDeadbandDegPerSec = 10f;
        internal const float DefaultTorqueDeadband = 0.1f;

        [Serializable]
        private struct Sidecar
        {
            public ControllerSummary[] opponents;
        }

        private readonly float yawRateDeadbandDegPerSec;
        private readonly float torqueDeadband;
        private readonly List<string> order = new();
        private readonly Dictionary<string, ControllerPool> poolsByOpponent = new();
        private ControllerSampler sampler;
        private Ship agent;
        private Ship enemy;
        private Navigator navigator;
        private float projectileSpeed;
        private string label;

        public ControllerProbe(IReadOnlyDictionary<string, float> parameters)
        {
            yawRateDeadbandDegPerSec = Param(parameters, YawRateDeadbandKey, DefaultYawRateDeadbandDegPerSec);
            torqueDeadband = Param(parameters, TorqueDeadbandKey, DefaultTorqueDeadband);
        }

        private static float Param(IReadOnlyDictionary<string, float> parameters, string key, float fallback)
        {
            var value = parameters != null && parameters.TryGetValue(key, out var given) ? given : fallback;
            if (!float.IsFinite(value) || value < 0f)
                throw new ArgumentException($"{ProbeName} probe: {key}={value} must be a finite value >= 0.");
            return value;
        }

        public string Name => ProbeName;

        public void Begin(in ProbeContext context)
        {
            label = context.opponentLabel;
            // Navigator identity survives respawns; its Mpc does not (ResetState rebuilds it), so Sample re-reads it.
            if (context.pair.Agent != agent)
            {
                agent = context.pair.Agent;
                enemy = context.pair.Baseline;
                navigator = ((AICommander)agent.Commander).Navigator;
                projectileSpeed = agent.Weapons.Context.ProjectileSpeed(WeaponSlot.Primary);
            }
            sampler = new ControllerSampler(torqueDeadband, yawRateDeadbandDegPerSec);
        }

        public void Sample()
        {
            if (!agent) return;
            var mpc = navigator.mpc;
            var control = mpc.LastControl;
            var cfg = mpc.Config;
            var solver = mpc.Solver;
            var kin = agent.Kinematics;
            var pos = new float2(kin.pos.x, kin.pos.y);

            // The obstacle buffer is only valid until the next Solve — classify in the tick it was written.
            var hullRadius = cfg.shipRadius * Cost.BankProfileScale(control.strafe, cfg) + cfg.collisionSafetyMargin;
            var underThreat = ObstacleThreat(pos, new float2(kin.vel.x, kin.vel.y),
                solver.Obstacles, solver.ObstacleCount, hullRadius, cfg.maxLatAccel);

            var hasAnchor = (bool)enemy;
            var anchorYawRad = 0f;
            if (hasAnchor)
            {
                var enemyKin = enemy.Kinematics;
                anchorYawRad = Cost.AnchorYaw(pos, new float2(enemyKin.pos.x, enemyKin.pos.y),
                    new float2(enemyKin.vel.x, enemyKin.vel.y), projectileSpeed);
            }

            sampler.Sample(control.yawTorque, kin.yawRate, hasAnchor, anchorYawRad, underThreat,
                Time.fixedDeltaTime);
        }

        /// <summary>Obstacle threat: the MPC's obstacle handling fires this step — hull overlap or collision-course turn-away, mirroring <see cref="Cost.ObstacleCosts"/>' two mutually exclusive branches.</summary>
        internal static bool ObstacleThreat(float2 pos, float2 vel, NativeArray<ObstacleData> obstacles,
            int count, float hullRadius, float maxLatAccel)
            => count > 0 && (Cost.Collides(pos, obstacles, count, hullRadius)
                || Cost.TurnAwayCost(pos, vel, obstacles, count, hullRadius, maxLatAccel) > 0f);

        public string End(in EpisodeResult result)
        {
            var row = sampler.ToRow(in result, label);
            if (!poolsByOpponent.TryGetValue(label, out var pool))
            {
                pool = new ControllerPool();
                poolsByOpponent[label] = pool;
                order.Add(label);
            }
            sampler.DrainInto(pool, in result);
            sampler = null;
            return row.ToJsonLine();
        }

        public void Summarize(string summaryPath)
        {
            var sidecar = new Sidecar { opponents = new ControllerSummary[order.Count] };
            for (var i = 0; i < order.Count; i++)
                sidecar.opponents[i] = ControllerSummary.Summarize(order[i], poolsByOpponent[order[i]]);
            File.WriteAllText(summaryPath, JsonUtility.ToJson(sidecar, prettyPrint: true));
        }

        public void Dispose() { }
    }
}
