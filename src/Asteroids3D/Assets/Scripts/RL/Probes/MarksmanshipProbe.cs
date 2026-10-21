using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using RL.Episodes;

namespace RL.Probes
{
    [Serializable]
    public struct MarksmanshipRow
    {
        public string schema;
        public int seed;
        public int episodeIndex;
        public string block;
        public string target;
        public string outcome;
        public string endKind;
        public float simSeconds;
        public MarksmanshipTally tally;

        public const string SchemaId = "marksmanship-v1";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }

    /// <summary>One block's episodes pooled: counts are sums, ratios are taken over the pooled counts, and the time to kill is a mean over the episodes the weapon killed in.</summary>
    [Serializable]
    public struct MarksmanshipBlock
    {
        public string block;
        public string weapon;
        public string target;
        public int episodes;
        public int kills;
        public float meanTimeToKill;
        public float simSeconds;
        public int fired;
        public int hits;
        public float hitRate;
        public float damageDealt;
        public float envelopeShare;
        public float meanCrossingSpeed;
        public int selfHits;
        public bool hasLock;
        public int guidedLaunches;
        public int unguidedLaunches;
        public bool hasCharge;
        public int fullChargeShots;
        public int earlyShots;
        public float meanEarlyCharge;
        public int droppedCharges;

        public static MarksmanshipBlock Pool(string block, IReadOnlyList<MarksmanshipRow> rows)
        {
            var pooled = new MarksmanshipBlock { block = block, episodes = rows.Count };
            var steps = 0;
            var envelopeSteps = 0;
            var killSeconds = 0f;
            foreach (var row in rows)
            {
                var tally = row.tally;
                pooled.weapon = tally.weapon;
                pooled.target = row.target;
                pooled.simSeconds += row.simSeconds;
                if (tally.kill)
                {
                    pooled.kills++;
                    killSeconds += row.simSeconds;
                }
                steps += tally.steps;
                envelopeSteps += tally.envelopeSteps;
                pooled.meanCrossingSpeed += tally.meanCrossingSpeed * tally.steps;
                pooled.fired += tally.fired;
                pooled.hits += tally.hits;
                pooled.damageDealt += tally.damageDealt;
                pooled.selfHits += tally.selfHits;
                pooled.hasLock |= tally.hasLock;
                pooled.guidedLaunches += tally.guidedLaunches;
                pooled.hasCharge |= tally.hasCharge;
                pooled.fullChargeShots += tally.fullChargeShots;
                pooled.earlyShots += tally.earlyShots;
                pooled.meanEarlyCharge += tally.meanEarlyCharge * tally.earlyShots;
                pooled.droppedCharges += tally.droppedCharges;
            }
            if (pooled.kills > 0) pooled.meanTimeToKill = killSeconds / pooled.kills;
            if (pooled.fired > 0) pooled.hitRate = (float)pooled.hits / pooled.fired;
            if (steps > 0)
            {
                pooled.envelopeShare = (float)envelopeSteps / steps;
                pooled.meanCrossingSpeed /= steps;
            }
            if (pooled.hasLock) pooled.unguidedLaunches = pooled.fired - pooled.guidedLaunches;
            if (pooled.earlyShots > 0) pooled.meanEarlyCharge /= pooled.earlyShots;
            return pooled;
        }
    }

    /// <summary>The marksmanship instrument as a harness probe: one <see cref="MarksmanshipSampler"/> per episode on the pair's agent-slot ship against its baseline-slot ship, rows pooled by block label. Its summary sidecar is the machine contract, one entry per block; the markdown table written beside it is the same blocks for pasting into a PR description.</summary>
    public sealed class MarksmanshipProbe : IHarnessProbe
    {
        public const string ProbeName = "marksmanship";
        public const string SummarySchemaId = "marksmanship-summary-v1";

        [Serializable]
        public struct Sidecar
        {
            public string schema;
            public MarksmanshipBlock[] blocks;
        }

        private readonly List<string> order = new();
        private readonly Dictionary<string, List<MarksmanshipRow>> rowsByBlock = new();
        private MarksmanshipSampler sampler;
        private string label;
        private string target;
        private int seed;
        private int episodeIndex;

        public string Name => ProbeName;

        /// <summary>The table rides beside the summary sidecar, under a name no summary glob matches.</summary>
        public static string TablePath(string summaryPath) => Path.ChangeExtension(summaryPath, ".md");

        public void Begin(in ProbeContext context)
        {
            label = context.opponentLabel;
            target = context.draw.archetype;
            seed = context.spec.runSeed;
            episodeIndex = context.episodeIndex;
            sampler = new MarksmanshipSampler(context.pair.Agent, context.pair.Baseline);
        }

        public void Sample() => sampler.Sample();

        public string End(in EpisodeResult result)
        {
            var row = new MarksmanshipRow
            {
                schema = MarksmanshipRow.SchemaId,
                seed = seed,
                episodeIndex = episodeIndex,
                block = label,
                target = target,
                outcome = result.outcome,
                endKind = result.endKind,
                simSeconds = result.simSeconds,
                tally = sampler.Tally,
            };
            if (!rowsByBlock.TryGetValue(label, out var rows))
            {
                rows = new List<MarksmanshipRow>();
                rowsByBlock[label] = rows;
                order.Add(label);
            }
            rows.Add(row);
            sampler.Dispose();
            sampler = null;
            return row.ToJsonLine();
        }

        public void Summarize(string summaryPath)
        {
            var sidecar = new Sidecar { schema = SummarySchemaId, blocks = new MarksmanshipBlock[order.Count] };
            for (var i = 0; i < order.Count; i++)
                sidecar.blocks[i] = MarksmanshipBlock.Pool(order[i], rowsByBlock[order[i]]);
            File.WriteAllText(summaryPath, JsonUtility.ToJson(sidecar, prettyPrint: true));
            File.WriteAllText(TablePath(summaryPath), MarkdownTable(sidecar.blocks));
        }

        public void Dispose() => sampler?.Dispose();

        /// <summary>One row per block; a cell is blank where its column does not apply to the weapon or has no sample.</summary>
        internal static string MarkdownTable(IReadOnlyList<MarksmanshipBlock> blocks)
        {
            var table = new StringBuilder();
            table.Append("| Weapon | Target | Episodes | Kills | TTK (s) | Sim (s) | Fired | Hits | Hit % | Damage "
                + "| Envelope % | Crossing (u/s) | Guided | Unguided | Full charge | Early | Early charge "
                + "| Dropped | Self-hits |\n");
            table.Append("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|\n");
            foreach (var b in blocks)
            {
                var cells = new[]
                {
                    b.weapon,
                    b.target,
                    Count(b.episodes),
                    Count(b.kills),
                    b.kills > 0 ? Number(b.meanTimeToKill, "0.0") : "",
                    Number(b.simSeconds, "0.0"),
                    Count(b.fired),
                    Count(b.hits),
                    b.fired > 0 ? Number(100f * b.hitRate, "0.0") : "",
                    Number(b.damageDealt, "0.#"),
                    Number(100f * b.envelopeShare, "0.0"),
                    Number(b.meanCrossingSpeed, "0.0"),
                    b.hasLock ? Count(b.guidedLaunches) : "",
                    b.hasLock ? Count(b.unguidedLaunches) : "",
                    b.hasCharge ? Count(b.fullChargeShots) : "",
                    b.hasCharge ? Count(b.earlyShots) : "",
                    b.earlyShots > 0 ? Number(b.meanEarlyCharge, "0.00") : "",
                    b.hasCharge ? Count(b.droppedCharges) : "",
                    Count(b.selfHits),
                };
                table.Append("| ").Append(string.Join(" | ", cells)).Append(" |\n");
            }
            return table.ToString();
        }

        private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Number(float value, string format) =>
            value.ToString(format, CultureInfo.InvariantCulture);
    }
}
