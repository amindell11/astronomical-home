using System;
using System.Collections.Generic;
using System.IO;
using AI;
using Movement;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>One facing-probe episode's command-vs-nose signals, all angles in degrees; zero-decision or zero-step episodes zero-fill.</summary>
    [Serializable]
    public struct FacingRow
    {
        public string schema;
        public int seed;
        public int episodeIndex;
        public string opponent;
        public string outcome;
        public float simSeconds;
        public int decisions;
        public float meanFacingErrorDeg;
        public float medianFacingErrorDeg;
        public float p90FacingErrorDeg;
        public float meanCmdDeltaDeg;
        public float medianCmdDeltaDeg;
        public float p90CmdDeltaDeg;
        public float noseReversalsPerSec;
        public float cmdReversalsPerSec;
        public float meanAbsYawRateDegPerSec;
        public float meanFacingWeight;
        public float p10FacingWeight;
        public float medianFacingWeight;
        public float p90FacingWeight;
        public float lowAuthorityFraction;
        public float authorityScale;

        public const string SchemaId = "rl-facing-probe-v1";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }

    /// <summary>One opponent label's raw samples pooled across episodes, so summary percentiles never average per-episode stats.</summary>
    internal sealed class FacingPool
    {
        public readonly List<float> facingErrorDeg = new();
        public readonly List<float> cmdDeltaDeg = new();
        public readonly List<float> weights = new();
        public int episodes;
        public int noseReversals;
        public int cmdReversals;
        public int steps;
        public float absYawRateSum;
        public float simSeconds;
    }

    /// <summary>Per-opponent aggregate over a facing sweep, pooled across all episodes' samples.</summary>
    [Serializable]
    public struct FacingSummary
    {
        public string schema;
        public string opponent;
        public int episodes;
        public int decisions;
        public float meanEpisodeSeconds;
        public float authorityScale;
        public float meanFacingErrorDeg;
        public float medianFacingErrorDeg;
        public float p90FacingErrorDeg;
        public float meanCmdDeltaDeg;
        public float medianCmdDeltaDeg;
        public float p90CmdDeltaDeg;
        public float noseReversalsPerSec;
        public float cmdReversalsPerSec;
        public float meanAbsYawRateDegPerSec;
        public float meanFacingWeight;
        public float p10FacingWeight;
        public float medianFacingWeight;
        public float p90FacingWeight;
        public float lowAuthorityFraction;

        public const string SchemaId = "rl-facing-probe-summary-v1";

        internal static FacingSummary Summarize(string opponent, FacingPool pool, float authorityScale) => new()
        {
            schema = SchemaId,
            opponent = opponent,
            episodes = pool.episodes,
            decisions = pool.weights.Count,
            meanEpisodeSeconds = pool.episodes > 0 ? pool.simSeconds / pool.episodes : 0f,
            authorityScale = authorityScale,
            meanFacingErrorDeg = Mean(pool.facingErrorDeg),
            medianFacingErrorDeg = Percentile(pool.facingErrorDeg, 50),
            p90FacingErrorDeg = Percentile(pool.facingErrorDeg, 90),
            meanCmdDeltaDeg = Mean(pool.cmdDeltaDeg),
            medianCmdDeltaDeg = Percentile(pool.cmdDeltaDeg, 50),
            p90CmdDeltaDeg = Percentile(pool.cmdDeltaDeg, 90),
            noseReversalsPerSec = pool.simSeconds > 0f ? pool.noseReversals / pool.simSeconds : 0f,
            cmdReversalsPerSec = pool.simSeconds > 0f ? pool.cmdReversals / pool.simSeconds : 0f,
            meanAbsYawRateDegPerSec = pool.steps > 0 ? pool.absYawRateSum / pool.steps : 0f,
            meanFacingWeight = Mean(pool.weights),
            p10FacingWeight = Percentile(pool.weights, 10),
            medianFacingWeight = Percentile(pool.weights, 50),
            p90FacingWeight = Percentile(pool.weights, 90),
            lowAuthorityFraction = LowAuthorityFraction(pool.weights),
        };

        internal static float Mean(List<float> samples)
        {
            if (samples.Count == 0) return 0f;
            var sum = 0f;
            foreach (var sample in samples) sum += sample;
            return sum / samples.Count;
        }

        // Ceil-rank convention, no interpolation; median = p50.
        internal static float Percentile(List<float> samples, int p)
        {
            if (samples.Count == 0) return 0f;
            var sorted = new List<float>(samples);
            sorted.Sort();
            return sorted[Mathf.Clamp(Mathf.CeilToInt(p / 100f * sorted.Count) - 1, 0, sorted.Count - 1)];
        }

        internal static float LowAuthorityFraction(List<float> weights)
        {
            if (weights.Count == 0) return 0f;
            var low = 0;
            foreach (var weight in weights)
                if (weight < FacingProbe.LowAuthorityWeight) low++;
            return (float)low / weights.Count;
        }
    }

    /// <summary>Per-fixed-step facing sampler for one episode: nose motion (|yawRate|, strict sign-flip reversals), commanded-vs-actual facing error once a decision exists, and per-decision command deltas/weights read off the chooser's <see cref="IPolicyReadout"/> — new decisions detected by the monotonic TotalDecisions.</summary>
    public sealed class FacingSampler
    {
        private readonly IPolicyReadout readout;

        internal readonly List<float> facingErrorDeg = new();
        internal readonly List<float> cmdDeltaDeg = new();
        internal readonly List<float> weights = new();
        internal int NoseReversals { get; private set; }
        internal int CmdReversals { get; private set; }
        internal int Steps { get; private set; }
        internal float AbsYawRateSum { get; private set; }

        private int lastTotalDecisions;
        private int yawRateSign;
        private int cmdDeltaSign;
        private float prevCmdDeg;

        public FacingSampler(IPolicyReadout readout)
        {
            this.readout = readout;
            lastTotalDecisions = readout.TotalDecisions;
        }

        public void Sample(in Kinematics kin)
        {
            Steps++;
            AbsYawRateSum += Mathf.Abs(kin.yawRate);
            if (FlippedStrictly(kin.yawRate, ref yawRateSign)) NoseReversals++;

            var total = readout.TotalDecisions;
            if (total > lastTotalDecisions)
            {
                lastTotalDecisions = total;
                var newest = readout.ActionFromNewest(0);
                var cmdDeg = newest.facingOffsetRad * Mathf.Rad2Deg;
                weights.Add(newest.facingWeight);
                if (weights.Count > 1)
                {
                    var delta = Mathf.DeltaAngle(prevCmdDeg, cmdDeg);
                    cmdDeltaDeg.Add(Mathf.Abs(delta));
                    if (FlippedStrictly(delta, ref cmdDeltaSign)) CmdReversals++;
                }
                prevCmdDeg = cmdDeg;
            }
            if (weights.Count > 0)
                facingErrorDeg.Add(Mathf.Abs(Mathf.DeltaAngle(prevCmdDeg, kin.yaw)));
        }

        public FacingRow ToRow(in EpisodeResult result, string opponent, float authorityScale) => new()
        {
            schema = FacingRow.SchemaId,
            seed = result.spec.runSeed,
            episodeIndex = result.episodeIndex,
            opponent = opponent,
            outcome = result.outcome,
            simSeconds = result.simSeconds,
            decisions = weights.Count,
            meanFacingErrorDeg = FacingSummary.Mean(facingErrorDeg),
            medianFacingErrorDeg = FacingSummary.Percentile(facingErrorDeg, 50),
            p90FacingErrorDeg = FacingSummary.Percentile(facingErrorDeg, 90),
            meanCmdDeltaDeg = FacingSummary.Mean(cmdDeltaDeg),
            medianCmdDeltaDeg = FacingSummary.Percentile(cmdDeltaDeg, 50),
            p90CmdDeltaDeg = FacingSummary.Percentile(cmdDeltaDeg, 90),
            noseReversalsPerSec = result.simSeconds > 0f ? NoseReversals / result.simSeconds : 0f,
            cmdReversalsPerSec = result.simSeconds > 0f ? CmdReversals / result.simSeconds : 0f,
            meanAbsYawRateDegPerSec = Steps > 0 ? AbsYawRateSum / Steps : 0f,
            meanFacingWeight = FacingSummary.Mean(weights),
            p10FacingWeight = FacingSummary.Percentile(weights, 10),
            medianFacingWeight = FacingSummary.Percentile(weights, 50),
            p90FacingWeight = FacingSummary.Percentile(weights, 90),
            lowAuthorityFraction = FacingSummary.LowAuthorityFraction(weights),
            authorityScale = authorityScale,
        };

        internal void DrainInto(FacingPool pool, in EpisodeResult result)
        {
            pool.episodes++;
            pool.facingErrorDeg.AddRange(facingErrorDeg);
            pool.cmdDeltaDeg.AddRange(cmdDeltaDeg);
            pool.weights.AddRange(weights);
            pool.noseReversals += NoseReversals;
            pool.cmdReversals += CmdReversals;
            pool.steps += Steps;
            pool.absYawRateSum += AbsYawRateSum;
            pool.simSeconds += result.simSeconds;
        }

        // Zero keeps the previous sign — only strict positive↔negative flips count. Shared with the velrebase sampler.
        internal static bool FlippedStrictly(float value, ref int sign)
        {
            var next = value > 0f ? 1 : value < 0f ? -1 : 0;
            if (next == 0) return false;
            var flipped = sign == -next;
            sign = next;
            return flipped;
        }
    }

    /// <summary>The manual-aim facing instrument: one <see cref="FacingSampler"/> per episode on the measured agent's <see cref="IPolicyReadout"/> chooser, an optional facing-authority sweep (wFacing scales the <see cref="AgentChooser"/> override, measured agent only), and per-opponent pooled aggregates as the summary sidecar.</summary>
    public sealed class FacingProbe : ISessionProbe
    {
        public const string ProbeName = "facing";
        public const string AuthorityScaleKey = "wFacing";
        /// <summary>Below this facing weight a decision counts as low-authority (#219's near-zero regime).</summary>
        internal const float LowAuthorityWeight = 0.2f;

        [Serializable]
        private struct Sidecar
        {
            public FacingSummary[] opponents;
        }

        private readonly float authorityScale;
        private readonly List<string> order = new();
        private readonly Dictionary<string, FacingPool> poolsByOpponent = new();
        private FacingSampler sampler;
        private Ship agent;
        private IPolicyReadout readout;
        private AgentChooser scaledChooser;
        private string label;

        public FacingProbe(IReadOnlyDictionary<string, float> parameters)
        {
            authorityScale = parameters != null && parameters.TryGetValue(AuthorityScaleKey, out var value)
                ? value
                : 1f;
            if (!float.IsFinite(authorityScale) || authorityScale < 0f)
                throw new ArgumentException(
                    $"{ProbeName} probe: {AuthorityScaleKey}={authorityScale} must be a finite value >= 0.");
        }

        public string Name => ProbeName;

        public void Begin(in ProbeContext context)
        {
            label = context.opponentLabel;
            // Chooser identity survives respawns; re-resolve only when a new composition's pair arrives.
            if (context.pair.Agent != agent)
            {
                agent = context.pair.Agent;
                var chooser = agent.GetComponentInChildren<AICommander>().Brain.Chooser;
                readout = chooser as IPolicyReadout ?? throw new InvalidOperationException(
                    $"{ProbeName} probe requires an IPolicyReadout chooser; got {chooser?.GetType().Name ?? "null"}.");
                scaledChooser = authorityScale != 1f
                    ? chooser as AgentChooser ?? throw new InvalidOperationException(
                        $"{ProbeName} probe: {AuthorityScaleKey} needs an AgentChooser; got {chooser.GetType().Name}.")
                    : null;
            }
            if (scaledChooser != null) scaledChooser.FacingAuthorityScale = authorityScale;
            sampler = new FacingSampler(readout);
        }

        public void Sample()
        {
            if (!agent) return;
            sampler.Sample(agent.Kinematics);
        }

        public string End(in EpisodeResult result)
        {
            var row = sampler.ToRow(in result, label, authorityScale);
            if (!poolsByOpponent.TryGetValue(label, out var pool))
            {
                pool = new FacingPool();
                poolsByOpponent[label] = pool;
                order.Add(label);
            }
            sampler.DrainInto(pool, in result);
            sampler = null;
            return row.ToJsonLine();
        }

        public void Summarize(string summaryPath)
        {
            var sidecar = new Sidecar { opponents = new FacingSummary[order.Count] };
            for (var i = 0; i < order.Count; i++)
                sidecar.opponents[i] = FacingSummary.Summarize(order[i], poolsByOpponent[order[i]], authorityScale);
            File.WriteAllText(summaryPath, JsonUtility.ToJson(sidecar, prettyPrint: true));
        }

        public void Dispose()
        {
            if (scaledChooser != null) scaledChooser.FacingAuthorityScale = 1f;
        }
    }
}
