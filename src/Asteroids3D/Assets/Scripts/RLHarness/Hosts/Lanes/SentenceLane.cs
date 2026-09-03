using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The Stage A3 sentence lane client: for each seed, one composition per selected session bingo row — the measured ship plays the row's fixed hand-authored sentence against the row's staged opponent under the canonical eval arena. The controller probe's sidecar is the instrument; this summary just fingerprints the run.</summary>
    public sealed class SentenceLane : ISessionClient
    {
        IEnumerator ISessionClient.Run(HarnessSessionHost host, SessionSpec spec) => RunLane(host, spec);

        public const string SchemaId = "rl-sentence-summary-v1";

        [Serializable]
        public struct BlockSummary
        {
            public string label;
            public int episodes;
        }

        [Serializable]
        public struct Summary
        {
            public string schema;
            public int[] seeds;
            public int episodesPerSeed;
            public bool useAsteroidField;
            public float fieldDensityScale;
            public BlockSummary[] blocks;
            public string episodesJsonl;
            public ProbeArtifacts[] probes;
        }

        public static IEnumerator RunLane(HarnessSessionHost host, SessionSpec spec)
        {
            var baseSpec = EvalProtocol.EvalSpec(spec.fieldDensityScale);

            var jsonlPath = EpisodeJsonl.NewRunPath(spec.tag, CheckpointEvaluator.ResultsFolder, spec.outDir);
            var rows = spec.sentenceRows;
            var episodeCounts = new int[rows.Length];
            var field = baseSpec.useAsteroidField
                ? HarnessField.Spawn(host.Offset, host.Assets, baseSpec.fieldDensityScale, presentationEnabled: false)
                : null;

            foreach (var seed in spec.seeds)
            {
                var seedSpec = baseSpec;
                seedSpec.runSeed = seed;
                // One composition per row: the sentence binds at spawn, so a row change is a fresh pair.
                for (var i = 0; i < rows.Length; i++)
                {
                    var index = i;
                    var composition = host.NewSentenceComposition(in seedSpec, field, rows[i]);
                    yield return host.RunBlock(composition, SentenceRows.Block(rows[i]), spec.episodesPerSeed,
                        seedSpec, jsonlPath, _ => episodeCounts[index]++);
                    composition.Dispose();
                    host.Projectiles.ReturnAllToPool();
                }
            }

            field?.Dispose();
            var summary = new Summary
            {
                schema = SchemaId,
                seeds = (int[])spec.seeds.Clone(),
                episodesPerSeed = spec.episodesPerSeed,
                useAsteroidField = baseSpec.useAsteroidField,
                fieldDensityScale = baseSpec.fieldDensityScale,
                blocks = new BlockSummary[rows.Length],
                episodesJsonl = jsonlPath,
                probes = host.SummarizeProbes(jsonlPath),
            };
            for (var i = 0; i < rows.Length; i++)
                summary.blocks[i] = new BlockSummary { label = SentenceRows.Token(rows[i]), episodes = episodeCounts[i] };

            var summaryPath = jsonlPath.Replace(".jsonl", "-summary.json");
            File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, prettyPrint: true));
            foreach (var block in summary.blocks)
                Debug.Log($"[SentenceLane] {block.label}: episodes={block.episodes}");
            Debug.Log($"[SentenceLane] summary → {summaryPath}");
        }
    }
}
