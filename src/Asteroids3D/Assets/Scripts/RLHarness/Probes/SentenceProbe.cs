using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AI;
using Ships;
using UnityEngine;
using AI.Strategy;

namespace Game.RLHarness
{
    /// <summary>One sentence-probe episode's aggregates: per-slot referent switch rates, mean
    /// authority weights, and weight-entropy (§Stage C fork 8 — the failure branch's diagnosis
    /// surface and the live check on residual slot thrash). Zero-decision episodes zero-fill.</summary>
    [Serializable]
    public struct SentenceProbeRow
    {
        public string schema;
        public int seed;
        public int episodeIndex;
        public string opponent;
        public string outcome;
        public float simSeconds;
        public int decisions;
        public float aimReferentSwitchesPerDecision;
        public float posReferentSwitchesPerDecision;
        public float velReferentSwitchesPerDecision;
        public float rockBoundFraction;
        public float meanAimWeight;
        public float meanAbsPosWeight;
        public float meanVelWeight;
        public float meanAbsLaneWeight;
        public float meanFieldWeight;
        public float meanWeightEntropy;

        public const string SchemaId = "rl-sentence-probe-v1";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }

    /// <summary>One opponent label's pooled sentence samples across episodes.</summary>
    internal sealed class SentencePool
    {
        public int episodes;
        public int decisions;
        public int aimSwitches;
        public int posSwitches;
        public int velSwitches;
        public int rockBoundDecisions;
        public float aimWeightSum;
        public float absPosWeightSum;
        public float velWeightSum;
        public float absLaneWeightSum;
        public float fieldWeightSum;
        public float entropySum;
    }

    /// <summary>Per-opponent aggregate over a sentence-probe run.</summary>
    [Serializable]
    public struct SentenceProbeSummary
    {
        public string schema;
        public string opponent;
        public int episodes;
        public int decisions;
        public float aimReferentSwitchesPerDecision;
        public float posReferentSwitchesPerDecision;
        public float velReferentSwitchesPerDecision;
        public float rockBoundFraction;
        public float meanAimWeight;
        public float meanAbsPosWeight;
        public float meanVelWeight;
        public float meanAbsLaneWeight;
        public float meanFieldWeight;
        public float meanWeightEntropy;

        public const string SchemaId = "rl-sentence-probe-summary-v1";

        internal static SentenceProbeSummary Summarize(string opponent, SentencePool pool) => new()
        {
            schema = SchemaId,
            opponent = opponent,
            episodes = pool.episodes,
            decisions = pool.decisions,
            aimReferentSwitchesPerDecision = Rate(pool.aimSwitches, pool.decisions),
            posReferentSwitchesPerDecision = Rate(pool.posSwitches, pool.decisions),
            velReferentSwitchesPerDecision = Rate(pool.velSwitches, pool.decisions),
            rockBoundFraction = Rate(pool.rockBoundDecisions, pool.decisions),
            meanAimWeight = Rate(pool.aimWeightSum, pool.decisions),
            meanAbsPosWeight = Rate(pool.absPosWeightSum, pool.decisions),
            meanVelWeight = Rate(pool.velWeightSum, pool.decisions),
            meanAbsLaneWeight = Rate(pool.absLaneWeightSum, pool.decisions),
            meanFieldWeight = Rate(pool.fieldWeightSum, pool.decisions),
            meanWeightEntropy = Rate(pool.entropySum, pool.decisions),
        };

        private static float Rate(float sum, int count) => count > 0 ? sum / count : 0f;
    }

    /// <summary>Per-decision sentence sampler for one episode, reading the brain's
    /// <see cref="IPolicyReadout"/> — new decisions detected by the monotonic TotalDecisions.
    /// Referent switches compare slot CHOICES (0 = enemy, 1..6 = rock slots): sticky slot
    /// indices make an index change a real retarget, the thrash signal the probe exists for.</summary>
    public sealed class SentenceSampler
    {
        private readonly IPolicyReadout readout;
        private readonly Action<PolicyAction, float> onDecision;

        internal int Decisions { get; private set; }
        internal int AimSwitches { get; private set; }
        internal int PosSwitches { get; private set; }
        internal int VelSwitches { get; private set; }
        internal int RockBoundDecisions { get; private set; }
        internal float AimWeightSum { get; private set; }
        internal float AbsPosWeightSum { get; private set; }
        internal float VelWeightSum { get; private set; }
        internal float AbsLaneWeightSum { get; private set; }
        internal float FieldWeightSum { get; private set; }
        internal float EntropySum { get; private set; }

        private int lastTotalDecisions;
        private int prevAimReferent;
        private int prevPosReferent;
        private int prevVelReferent;

        public SentenceSampler(IPolicyReadout readout, Action<PolicyAction, float> onDecision = null)
        {
            this.readout = readout;
            this.onDecision = onDecision;
            lastTotalDecisions = readout.TotalDecisions;
        }

        public void Sample()
        {
            var total = readout.TotalDecisions;
            if (total <= lastTotalDecisions) return;
            lastTotalDecisions = total;

            var a = readout.ActionFromNewest(0);
            if (Decisions > 0)
            {
                if (a.aimReferent != prevAimReferent) AimSwitches++;
                if (a.posReferent != prevPosReferent) PosSwitches++;
                if (a.velReferent != prevVelReferent) VelSwitches++;
            }
            prevAimReferent = a.aimReferent;
            prevPosReferent = a.posReferent;
            prevVelReferent = a.velReferent;

            if (a.aimReferent != 0 || a.posReferent != 0 || a.velReferent != 0) RockBoundDecisions++;
            AimWeightSum += a.facingWeight;
            AbsPosWeightSum += Mathf.Abs(a.posWeight);
            VelWeightSum += a.velocityWeight;
            AbsLaneWeightSum += Mathf.Abs(a.laneWeight);
            FieldWeightSum += a.fieldWeight;
            var entropy = WeightEntropy(in a);
            EntropySum += entropy;
            Decisions++;

            onDecision?.Invoke(a, entropy);
        }

        /// <summary>Shannon entropy (nats) of the |weight| distribution across the five slots —
        /// the saturation check: 0 = all authority in one slot, ln 5 ≈ 1.61 = uniform.</summary>
        public static float WeightEntropy(in PolicyAction a)
        {
            Span<float> w = stackalloc float[]
            {
                Mathf.Abs(a.facingWeight), Mathf.Abs(a.posWeight), Mathf.Abs(a.velocityWeight),
                Mathf.Abs(a.laneWeight), Mathf.Abs(a.fieldWeight),
            };
            var sum = 0f;
            foreach (var v in w) sum += v;
            if (sum <= 0f) return 0f;

            var entropy = 0f;
            foreach (var v in w)
            {
                if (v <= 0f) continue;
                var p = v / sum;
                entropy -= p * Mathf.Log(p);
            }
            return entropy;
        }

        internal SentenceProbeRow ToRow(in EpisodeResult result, string opponent) => new()
        {
            schema = SentenceProbeRow.SchemaId,
            seed = result.spec.runSeed,
            episodeIndex = result.episodeIndex,
            opponent = opponent,
            outcome = result.outcome,
            simSeconds = result.simSeconds,
            decisions = Decisions,
            aimReferentSwitchesPerDecision = Rate(AimSwitches),
            posReferentSwitchesPerDecision = Rate(PosSwitches),
            velReferentSwitchesPerDecision = Rate(VelSwitches),
            rockBoundFraction = Rate(RockBoundDecisions),
            meanAimWeight = Rate(AimWeightSum),
            meanAbsPosWeight = Rate(AbsPosWeightSum),
            meanVelWeight = Rate(VelWeightSum),
            meanAbsLaneWeight = Rate(AbsLaneWeightSum),
            meanFieldWeight = Rate(FieldWeightSum),
            meanWeightEntropy = Rate(EntropySum),
        };

        internal void DrainInto(SentencePool pool)
        {
            pool.episodes++;
            pool.decisions += Decisions;
            pool.aimSwitches += AimSwitches;
            pool.posSwitches += PosSwitches;
            pool.velSwitches += VelSwitches;
            pool.rockBoundDecisions += RockBoundDecisions;
            pool.aimWeightSum += AimWeightSum;
            pool.absPosWeightSum += AbsPosWeightSum;
            pool.velWeightSum += VelWeightSum;
            pool.absLaneWeightSum += AbsLaneWeightSum;
            pool.fieldWeightSum += FieldWeightSum;
            pool.entropySum += EntropySum;
        }

        private float Rate(float sum) => Decisions > 0 ? sum / Decisions : 0f;
    }

    /// <summary>The intent-sentence instrument (§Stage C fork 8): per-episode JSONL aggregates plus
    /// a per-decision CSV sidecar (every weight, referent/frame choice, trigger branch, and the
    /// weight-entropy) written beside the summary — the raw stream fork 6's failure diagnosis and
    /// the rig's sentence replay read.</summary>
    public sealed class SentenceProbe : ISessionProbe
    {
        public const string ProbeName = "sentence";
        private const string DecisionsCsvSuffix = "-decisions.csv";

        private const string CsvHeader = "opponent,episodeIndex,decision,aimWeight,aimOffsetRad,aimReferent," +
            "posWeight,posOffsetR,posOffsetThetaRad,posSetpoint,posReferent,posFrame," +
            "velWeight,velRadialSpeed,velTangentialSpeed,velReferent,velFrame," +
            "laneWeight,fieldWeight,firePrimary,fireSecondary,boost,weightEntropy";

        [Serializable]
        private struct Sidecar
        {
            public string decisionsCsv;
            public SentenceProbeSummary[] opponents;
        }

        private readonly StringBuilder csv = new(CsvHeader + "\n");
        private readonly List<string> order = new();
        private readonly Dictionary<string, SentencePool> poolsByOpponent = new();
        private SentenceSampler sampler;
        private Ship agent;
        private IPolicyReadout readout;
        private string label;
        private int episodeIndex;
        private int decisionInEpisode;

        public string Name => ProbeName;

        public void Begin(in ProbeContext context)
        {
            label = context.opponentLabel;
            episodeIndex = context.episodeIndex;
            decisionInEpisode = 0;
            if (context.pair.Agent != agent)
            {
                agent = context.pair.Agent;
                var brain = agent.GetComponentInChildren<AICommander>().Brain;
                readout = brain as IPolicyReadout ?? throw new InvalidOperationException(
                    $"{ProbeName} probe requires an IPolicyReadout brain; got {(brain ? brain.GetType().Name : "null")}.");
            }
            sampler = new SentenceSampler(readout, AppendCsvRow);
        }

        public void Sample() => sampler?.Sample();

        public string End(in EpisodeResult result)
        {
            var row = sampler.ToRow(in result, label);
            if (!poolsByOpponent.TryGetValue(label, out var pool))
            {
                pool = new SentencePool();
                poolsByOpponent[label] = pool;
                order.Add(label);
            }
            sampler.DrainInto(pool);
            sampler = null;
            return row.ToJsonLine();
        }

        public void Summarize(string summaryPath)
        {
            var csvName = Path.GetFileNameWithoutExtension(summaryPath) + DecisionsCsvSuffix;
            var csvPath = Path.Combine(Path.GetDirectoryName(summaryPath) ?? "", csvName);
            File.WriteAllText(csvPath, csv.ToString());

            var sidecar = new Sidecar
            {
                decisionsCsv = csvName,
                opponents = new SentenceProbeSummary[order.Count],
            };
            for (var i = 0; i < order.Count; i++)
                sidecar.opponents[i] = SentenceProbeSummary.Summarize(order[i], poolsByOpponent[order[i]]);
            File.WriteAllText(summaryPath, JsonUtility.ToJson(sidecar, prettyPrint: true));
        }

        private void AppendCsvRow(PolicyAction a, float entropy)
        {
            csv.Append(label).Append(',')
                .Append(episodeIndex).Append(',')
                .Append(decisionInEpisode++).Append(',');
            AppendF(a.facingWeight);
            AppendF(a.facingOffsetRad);
            csv.Append(a.aimReferent).Append(',');
            AppendF(a.posWeight);
            AppendF(a.posOffsetR);
            AppendF(a.posOffsetThetaRad);
            AppendF(a.posSetpoint);
            csv.Append(a.posReferent).Append(',')
                .Append(a.posFrame).Append(',');
            AppendF(a.velocityWeight);
            AppendF(a.radialSpeed);
            AppendF(a.tangentialSpeed);
            csv.Append(a.velReferent).Append(',')
                .Append(a.velFrame).Append(',');
            AppendF(a.laneWeight);
            AppendF(a.fieldWeight);
            csv.Append(a.firePrimary ? 1 : 0).Append(',')
                .Append(a.fireSecondary ? 1 : 0).Append(',')
                .Append(a.boost ? 1 : 0).Append(',')
                .Append(entropy.ToString("G6", CultureInfo.InvariantCulture)).Append('\n');
        }

        private void AppendF(float value) =>
            csv.Append(value.ToString("G6", CultureInfo.InvariantCulture)).Append(',');

        public void Dispose() { }
    }
}
