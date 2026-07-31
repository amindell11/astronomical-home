using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The eval lane client: sequences the host's primitives over the frozen eval protocol — a fresh composition per seed so every RNG stream replays from that seed, then one equal episode block per opponent (the roster's five archetypes by default, stratification being sequencing rather than a mixture draw) — and aggregates per-opponent W/L/D with the Wilson 95% lower bound on win-rate (draws are non-wins, deliberately NO blended aggregate). Writes per-episode JSONL plus a summary artifact under results/rl-eval/; the spec's probes write their own sidecars alongside.</summary>
    public static class CheckpointEvaluator
    {
        public const string ResultsFolder = "rl-eval";
        public const string SchemaId = "rl-eval-summary-v2";

        private static readonly OpponentArchetype[] EvalArchetypes =
        {
            OpponentArchetype.Aggressor,
            OpponentArchetype.Evader,
            OpponentArchetype.Orbiter,
            OpponentArchetype.Kiter,
            OpponentArchetype.Dummy,
        };

        [Serializable]
        public struct OpponentSummary
        {
            public string opponent;
            public int episodes;
            public int wins;
            public int losses;
            public int draws;
            public float winRate;
            public float wilsonLowerBound95;
        }

        [Serializable]
        public struct Summary
        {
            public string schema;
            public string checkpoint;
            /// <summary>The file RL_HARNESS_ONNX named — provenance the imported asset path erases.</summary>
            public string checkpointSource;
            /// <summary>Slot-2 provenance; empty unless the opponent is a checkpoint.</summary>
            public string opponentCheckpoint;
            public string opponentCheckpointSource;
            public int[] seeds;
            public int episodesPerSeed;
            public bool useAsteroidField;
            public float fieldDensityScale;
            public OpponentSummary[] opponents;
            public string episodesJsonl;
            public ProbeArtifacts[] probes;
        }

        /// <summary>The host's lane entry: the canonical eval environment (training's terminal lesson), then the protocol.</summary>
        public static IEnumerator RunLane(HarnessSessionHost host, SessionSpec spec) =>
            Run(host, spec, EvalProtocol.EvalSpec(spec.fieldDensityScale));

        public static IEnumerator Run(HarnessSessionHost host, SessionSpec sessionSpec, RewardSpec baseSpec,
            Action<Summary> onDone = null)
        {
            var jsonlPath = EpisodeJsonl.NewRunPath(sessionSpec.tag, ResultsFolder, sessionSpec.outDir);
            var summary = new Summary
            {
                schema = SchemaId,
                checkpoint = sessionSpec.onnxAssetPath,
                checkpointSource = sessionSpec.onnxSourcePath,
                opponentCheckpoint = sessionSpec.opponentOnnxAssetPath,
                opponentCheckpointSource = sessionSpec.opponentOnnxSourcePath,
                seeds = (int[])sessionSpec.seeds.Clone(),
                episodesPerSeed = sessionSpec.episodesPerSeed,
                useAsteroidField = baseSpec.useAsteroidField,
                fieldDensityScale = baseSpec.fieldDensityScale,
                episodesJsonl = jsonlPath,
            };

            var blocks = Blocks(sessionSpec);
            var outcomes = new List<(string opponent, string outcome)>();
            var field = baseSpec.useAsteroidField
                ? HarnessField.Spawn(host.Arena, host.Assets, baseSpec.fieldDensityScale, presentationEnabled: false)
                : null;

            foreach (var seed in sessionSpec.seeds)
            {
                var spec = baseSpec;
                spec.runSeed = seed;
                var composition = host.NewComposition(in spec, sessionSpec.opponentKind, field);
                foreach (var block in blocks)
                {
                    var label = block.Label;
                    yield return host.RunBlock(composition, block, sessionSpec.episodesPerSeed, spec, jsonlPath,
                        result => outcomes.Add((label, result.outcome)));
                }

                composition.Dispose();
                host.Projectiles.ReturnAllToPool();
            }

            field?.Dispose();
            summary.opponents = Summarize(outcomes);
            summary.probes = host.SummarizeProbes(jsonlPath);

            var summaryPath = jsonlPath.Replace(".jsonl", "-summary.json");
            File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, prettyPrint: true));
            foreach (var o in summary.opponents)
                Debug.Log($"[CheckpointEvaluator] {sessionSpec.onnxAssetPath} vs {o.opponent}: episodes={o.episodes} "
                    + $"W/L/D={o.wins}/{o.losses}/{o.draws} "
                    + $"winRate={o.winRate:F3} wilsonLB95={o.wilsonLowerBound95:F3}");
            Debug.Log($"[CheckpointEvaluator] {sessionSpec.onnxAssetPath}: summary → {summaryPath}");
            onDone?.Invoke(summary);
        }

        /// <summary>The block sequence one seed's composition runs: the roster stratifies into equal per-archetype blocks; a pinned archetype, the mirror, or the checkpoint opponent is a single block.</summary>
        private static OpponentSpec[] Blocks(SessionSpec spec) => spec.opponentKind switch
        {
            OpponentKind.Roster => Array.ConvertAll(EvalArchetypes, OpponentSpec.Pinned),
            OpponentKind.Archetype => new[] { OpponentSpec.Pinned(spec.opponentArchetype) },
            OpponentKind.Mirror => new[] { OpponentSpec.Mirror },
            OpponentKind.Checkpoint => new[] { OpponentSpec.Checkpoint(spec.opponentLabel) },
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec.opponentKind, null),
        };

        /// <summary>Per-opponent aggregation in first-appearance order; each entry stands alone — no blended win rate exists.</summary>
        internal static OpponentSummary[] Summarize(IReadOnlyList<(string opponent, string outcome)> episodes)
        {
            var order = new List<string>();
            var tally = new Dictionary<string, OpponentSummary>();
            foreach (var (opponent, outcome) in episodes)
            {
                if (!tally.TryGetValue(opponent, out var entry))
                {
                    entry = new OpponentSummary { opponent = opponent };
                    order.Add(opponent);
                }
                entry.episodes++;
                if (outcome == EpisodeOutcome.Win.ToString()) entry.wins++;
                else if (outcome == EpisodeOutcome.Loss.ToString()) entry.losses++;
                else entry.draws++;
                tally[opponent] = entry;
            }

            var result = new OpponentSummary[order.Count];
            for (var i = 0; i < order.Count; i++)
            {
                var entry = tally[order[i]];
                entry.winRate = entry.episodes > 0 ? entry.wins / (float)entry.episodes : 0f;
                entry.wilsonLowerBound95 = EvalProtocol.WilsonLowerBound(entry.wins, entry.episodes);
                result[i] = entry;
            }
            return result;
        }
    }
}
