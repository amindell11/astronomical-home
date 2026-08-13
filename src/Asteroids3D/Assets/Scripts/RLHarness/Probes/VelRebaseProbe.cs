using System;
using System.Collections.Generic;
using System.IO;
using AI;
using Movement;
using Movement.MPC;
using Ships;
using Unity.Mathematics;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>One velrebase episode's kinematics-side velocity signals for the measured (baseline-slot) ship: churn from actual motion (heading-turn reversals, |lateral accel|), per-step velocity-tracking error against the arm's intended reference binned by range, and nose metrics as the cross-arm sanity column. Zero-decision or zero-step episodes zero-fill.</summary>
    [Serializable]
    public struct VelRebaseRow
    {
        public string schema;
        public int seed;
        public int episodeIndex;
        public string opponent;
        public string arm;
        public string outcome;
        public float simSeconds;
        public int decisions;
        public int steps;
        public float velHeadingReversalsPerSec;
        public float meanAbsLateralAccel;
        public float meanSpeed;
        public float noseReversalsPerSec;
        public float meanAbsYawRateDegPerSec;
        public float trackErrMeanUnder3;
        public float trackErrMedianUnder3;
        public float trackErrP90Under3;
        public int stepsUnder3;
        public float trackErrMeanAnnulus;
        public float trackErrMedianAnnulus;
        public float trackErrP90Annulus;
        public int stepsAnnulus;
        public float trackErrMeanBeyond8;
        public float trackErrMedianBeyond8;
        public float trackErrP90Beyond8;
        public int stepsBeyond8;

        public const string SchemaId = "rl-velrebase-probe-v1";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }

    /// <summary>One block label's raw samples pooled across episodes, so summary percentiles never average per-episode stats.</summary>
    internal sealed class VelRebasePool
    {
        public readonly List<float> trackErrUnder3 = new();
        public readonly List<float> trackErrAnnulus = new();
        public readonly List<float> trackErrBeyond8 = new();
        public int episodes;
        public int decisions;
        public int velHeadingReversals;
        public int noseReversals;
        public int steps;
        public int lateralAccelSamples;
        public float absLateralAccelSum;
        public float absYawRateSum;
        public float speedSum;
        public float simSeconds;
    }

    /// <summary>Per-block-label aggregate over a velrebase run, pooled across all episodes' samples; the paired legacy-vs-anchored delta reads across two labels of one archetype.</summary>
    [Serializable]
    public struct VelRebaseSummary
    {
        public string schema;
        public string opponent;
        public string arm;
        public int episodes;
        public int decisions;
        public float meanEpisodeSeconds;
        public float velHeadingReversalsPerSec;
        public float meanAbsLateralAccel;
        public float meanSpeed;
        public float noseReversalsPerSec;
        public float meanAbsYawRateDegPerSec;
        public float trackErrMeanUnder3;
        public float trackErrMedianUnder3;
        public float trackErrP90Under3;
        public int stepsUnder3;
        public float trackErrMeanAnnulus;
        public float trackErrMedianAnnulus;
        public float trackErrP90Annulus;
        public int stepsAnnulus;
        public float trackErrMeanBeyond8;
        public float trackErrMedianBeyond8;
        public float trackErrP90Beyond8;
        public int stepsBeyond8;

        public const string SchemaId = "rl-velrebase-probe-summary-v1";

        internal static VelRebaseSummary Summarize(string opponent, string arm, VelRebasePool pool) => new()
        {
            schema = SchemaId,
            opponent = opponent,
            arm = arm,
            episodes = pool.episodes,
            decisions = pool.decisions,
            meanEpisodeSeconds = pool.episodes > 0 ? pool.simSeconds / pool.episodes : 0f,
            velHeadingReversalsPerSec = pool.simSeconds > 0f ? pool.velHeadingReversals / pool.simSeconds : 0f,
            meanAbsLateralAccel = pool.lateralAccelSamples > 0
                ? pool.absLateralAccelSum / pool.lateralAccelSamples : 0f,
            meanSpeed = pool.steps > 0 ? pool.speedSum / pool.steps : 0f,
            noseReversalsPerSec = pool.simSeconds > 0f ? pool.noseReversals / pool.simSeconds : 0f,
            meanAbsYawRateDegPerSec = pool.steps > 0 ? pool.absYawRateSum / pool.steps : 0f,
            trackErrMeanUnder3 = FacingSummary.Mean(pool.trackErrUnder3),
            trackErrMedianUnder3 = FacingSummary.Percentile(pool.trackErrUnder3, 50),
            trackErrP90Under3 = FacingSummary.Percentile(pool.trackErrUnder3, 90),
            stepsUnder3 = pool.trackErrUnder3.Count,
            trackErrMeanAnnulus = FacingSummary.Mean(pool.trackErrAnnulus),
            trackErrMedianAnnulus = FacingSummary.Percentile(pool.trackErrAnnulus, 50),
            trackErrP90Annulus = FacingSummary.Percentile(pool.trackErrAnnulus, 90),
            stepsAnnulus = pool.trackErrAnnulus.Count,
            trackErrMeanBeyond8 = FacingSummary.Mean(pool.trackErrBeyond8),
            trackErrMedianBeyond8 = FacingSummary.Percentile(pool.trackErrBeyond8, 50),
            trackErrP90Beyond8 = FacingSummary.Percentile(pool.trackErrBeyond8, 90),
            stepsBeyond8 = pool.trackErrBeyond8.Count,
        };
    }

    /// <summary>Per-fixed-step sampler for one velrebase episode. Churn is measured from ship kinematics, never in the command's native space (legacy world vs anchored polar are incomparable, and resolving the polar at 5 Hz would falsely re-introduce churn — 50 Hz resolution is the point). Tracking error compares the ship's velocity to the arm's intended reference: the ZOH world command on the legacy arms, the per-step re-resolved <see cref="Cost.AnchoredVelocityRef"/> on the anchored arm.</summary>
    public sealed class VelRebaseSampler
    {
        /// <summary>The yaw wall: inside this range no interface tracks (v/r exceeds the slew authority).</summary>
        internal const float YawWallRange = 3f;
        /// <summary>Outer edge of the trackable annulus bin; beyond it pools separately.</summary>
        internal const float AnnulusOuterRange = 8f;
        /// <summary>Below this speed a velocity heading is noise, not direction — heading/lateral samples skip.</summary>
        internal const float HeadingSpeedFloor = 0.5f;

        private readonly IScriptedVelocityReadout readout;

        internal readonly List<float> trackErrUnder3 = new();
        internal readonly List<float> trackErrAnnulus = new();
        internal readonly List<float> trackErrBeyond8 = new();
        internal int Steps { get; private set; }
        internal int Decisions { get; private set; }
        internal int VelHeadingReversals { get; private set; }
        internal int NoseReversals { get; private set; }
        internal int LateralAccelSamples { get; private set; }
        internal float AbsLateralAccelSum { get; private set; }
        internal float AbsYawRateSum { get; private set; }
        internal float SpeedSum { get; private set; }

        private int lastTotalDecisions;
        private bool haveCommand;
        private ScriptedVelocityCommand command;
        private bool havePrevVel;
        private Vector2 prevVel;
        private bool havePrevHeading;
        private float prevHeadingDeg;
        private int headingDeltaSign;
        private int yawRateSign;

        public VelRebaseSampler(IScriptedVelocityReadout readout)
        {
            this.readout = readout;
            lastTotalDecisions = readout.TotalDecisions;
        }

        public void Sample(in Kinematics measured, in Kinematics enemy, float dt)
        {
            Steps++;
            var speed = measured.vel.magnitude;
            SpeedSum += speed;
            AbsYawRateSum += Mathf.Abs(measured.yawRate);
            if (FacingSampler.FlippedStrictly(measured.yawRate, ref yawRateSign)) NoseReversals++;

            if (speed >= HeadingSpeedFloor)
            {
                var headingDeg = Mathf.Atan2(measured.vel.y, measured.vel.x) * Mathf.Rad2Deg;
                if (havePrevHeading
                    && FacingSampler.FlippedStrictly(Mathf.DeltaAngle(prevHeadingDeg, headingDeg),
                        ref headingDeltaSign))
                    VelHeadingReversals++;
                prevHeadingDeg = headingDeg;
                havePrevHeading = true;
            }
            else
            {
                havePrevHeading = false;
                headingDeltaSign = 0;
            }

            if (havePrevVel && dt > 0f)
            {
                var prevSpeed = prevVel.magnitude;
                if (prevSpeed >= HeadingSpeedFloor)
                {
                    var accel = (measured.vel - prevVel) / dt;
                    var velHat = prevVel / prevSpeed;
                    AbsLateralAccelSum += Mathf.Abs(accel.x * velHat.y - accel.y * velHat.x);
                    LateralAccelSamples++;
                }
            }
            prevVel = measured.vel;
            havePrevVel = true;

            var total = readout.TotalDecisions;
            if (total > lastTotalDecisions)
            {
                Decisions += total - lastTotalDecisions;
                lastTotalDecisions = total;
                command = readout.LastCommand;
                haveCommand = true;
            }
            if (!haveCommand) return;

            var err = (measured.vel - IntendedReference(in measured, in enemy)).magnitude;
            var range = (enemy.pos - measured.pos).magnitude;
            var bin = range < YawWallRange ? trackErrUnder3
                : range <= AnnulusOuterRange ? trackErrAnnulus : trackErrBeyond8;
            bin.Add(err);
        }

        private Vector2 IntendedReference(in Kinematics measured, in Kinematics enemy)
        {
            if (readout.Drive != ArchetypeDrive.OpenLoopAnchored) return command.worldVelocity;
            var resolved = Cost.AnchoredVelocityRef(
                new float2(measured.pos.x, measured.pos.y),
                new float2(enemy.pos.x, enemy.pos.y),
                new float2(enemy.vel.x, enemy.vel.y),
                command.radialSpeed, command.tangentialSpeed);
            return new Vector2(resolved.x, resolved.y);
        }

        public VelRebaseRow ToRow(in EpisodeResult result, string opponent, string arm) => new()
        {
            schema = VelRebaseRow.SchemaId,
            seed = result.spec.runSeed,
            episodeIndex = result.episodeIndex,
            opponent = opponent,
            arm = arm,
            outcome = result.outcome,
            simSeconds = result.simSeconds,
            decisions = Decisions,
            steps = Steps,
            velHeadingReversalsPerSec = result.simSeconds > 0f ? VelHeadingReversals / result.simSeconds : 0f,
            meanAbsLateralAccel = LateralAccelSamples > 0 ? AbsLateralAccelSum / LateralAccelSamples : 0f,
            meanSpeed = Steps > 0 ? SpeedSum / Steps : 0f,
            noseReversalsPerSec = result.simSeconds > 0f ? NoseReversals / result.simSeconds : 0f,
            meanAbsYawRateDegPerSec = Steps > 0 ? AbsYawRateSum / Steps : 0f,
            trackErrMeanUnder3 = FacingSummary.Mean(trackErrUnder3),
            trackErrMedianUnder3 = FacingSummary.Percentile(trackErrUnder3, 50),
            trackErrP90Under3 = FacingSummary.Percentile(trackErrUnder3, 90),
            stepsUnder3 = trackErrUnder3.Count,
            trackErrMeanAnnulus = FacingSummary.Mean(trackErrAnnulus),
            trackErrMedianAnnulus = FacingSummary.Percentile(trackErrAnnulus, 50),
            trackErrP90Annulus = FacingSummary.Percentile(trackErrAnnulus, 90),
            stepsAnnulus = trackErrAnnulus.Count,
            trackErrMeanBeyond8 = FacingSummary.Mean(trackErrBeyond8),
            trackErrMedianBeyond8 = FacingSummary.Percentile(trackErrBeyond8, 50),
            trackErrP90Beyond8 = FacingSummary.Percentile(trackErrBeyond8, 90),
            stepsBeyond8 = trackErrBeyond8.Count,
        };

        internal void DrainInto(VelRebasePool pool, in EpisodeResult result)
        {
            pool.episodes++;
            pool.decisions += Decisions;
            pool.trackErrUnder3.AddRange(trackErrUnder3);
            pool.trackErrAnnulus.AddRange(trackErrAnnulus);
            pool.trackErrBeyond8.AddRange(trackErrBeyond8);
            pool.velHeadingReversals += VelHeadingReversals;
            pool.noseReversals += NoseReversals;
            pool.steps += Steps;
            pool.lateralAccelSamples += LateralAccelSamples;
            pool.absLateralAccelSum += AbsLateralAccelSum;
            pool.absYawRateSum += AbsYawRateSum;
            pool.speedSum += SpeedSum;
            pool.simSeconds += result.simSeconds;
        }
    }

    /// <summary>The K1-2 mechanical-rebase instrument: one <see cref="VelRebaseSampler"/> per episode on the measured (baseline-slot) ship's scripted-brain readout, against the agent-slot enemy's kinematics, with per-block-label pooled aggregates as the summary sidecar.</summary>
    public sealed class VelRebaseProbe : ISessionProbe
    {
        public const string ProbeName = "velrebase";

        [Serializable]
        private struct Sidecar
        {
            public VelRebaseSummary[] blocks;
        }

        private readonly List<string> order = new();
        private readonly Dictionary<string, VelRebasePool> poolsByLabel = new();
        private readonly Dictionary<string, string> armsByLabel = new();
        private VelRebaseSampler sampler;
        private Ship measured;
        private Ship enemy;
        private AICommander measuredCommander;
        private string label;
        private string arm;

        public string Name => ProbeName;

        public void Begin(in ProbeContext context)
        {
            label = context.opponentLabel;
            // Commander identity survives respawns; the brain does not — re-read it every episode.
            if (context.pair.Baseline != measured)
            {
                measured = context.pair.Baseline;
                enemy = context.pair.Agent;
                measuredCommander = measured.GetComponentInChildren<AICommander>();
            }
            var brain = measuredCommander.Brain;
            var readout = brain as IScriptedVelocityReadout ?? throw new InvalidOperationException(
                $"{ProbeName} probe requires an IScriptedVelocityReadout brain on the measured (baseline) ship; "
                + $"got {(brain ? brain.GetType().Name : "null")}.");
            arm = readout.Drive switch
            {
                ArchetypeDrive.OpenLoopAnchored => "anchored",
                ArchetypeDrive.OpenLoopLegacy => "legacy",
                _ => "production",
            };
            sampler = new VelRebaseSampler(readout);
        }

        public void Sample()
        {
            if (!measured || !enemy) return;
            sampler.Sample(measured.Kinematics, enemy.Kinematics, Time.fixedDeltaTime);
        }

        public string End(in EpisodeResult result)
        {
            var row = sampler.ToRow(in result, label, arm);
            if (!poolsByLabel.TryGetValue(label, out var pool))
            {
                pool = new VelRebasePool();
                poolsByLabel[label] = pool;
                armsByLabel[label] = arm;
                order.Add(label);
            }
            sampler.DrainInto(pool, in result);
            sampler = null;
            return row.ToJsonLine();
        }

        public void Summarize(string summaryPath)
        {
            var sidecar = new Sidecar { blocks = new VelRebaseSummary[order.Count] };
            for (var i = 0; i < order.Count; i++)
                sidecar.blocks[i] = VelRebaseSummary.Summarize(order[i], armsByLabel[order[i]],
                    poolsByLabel[order[i]]);
            File.WriteAllText(summaryPath, JsonUtility.ToJson(sidecar, prettyPrint: true));
        }

        public void Dispose() { }
    }
}
