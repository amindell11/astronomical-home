using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The K1-2 velrebase lane client: for each seed, one open-loop composition, then a legacy block and an anchored block per measured archetype — episode indices restart per block, so the two arms replay identical poses, jitter draws, juke sequences, and enemy circuits, and the paired delta isolates the drive frame. No W/L/D aggregation exists here (nothing fires); the velrebase probe's sidecar is the instrument, this summary just fingerprints the run.</summary>
    public sealed class VelRebaseLane : ISessionClient
    {
        IEnumerator ISessionClient.Run(HarnessSessionHost host, SessionSpec spec) => RunLane(host, spec);

        public const string SchemaId = "rl-velrebase-summary-v1";
        // The bounds rule must never end an episode: border steer is dropped on both arms and the
        // Evader flees freely for the full timeout (120 s at ~25 u/s stays far inside).
        internal const float OpenLoopArenaRadius = 5000f;

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
            baseSpec.arenaRadius = OpenLoopArenaRadius;

            var jsonlPath = EpisodeJsonl.NewRunPath(spec.tag, CheckpointEvaluator.ResultsFolder, spec.outDir);
            var blocks = Blocks(spec);
            var episodeCounts = new int[blocks.Length];
            var field = baseSpec.useAsteroidField
                ? HarnessField.Spawn(host.Arena, host.Assets, baseSpec.fieldDensityScale, presentationEnabled: false)
                : null;

            foreach (var seed in spec.seeds)
            {
                var seedSpec = baseSpec;
                seedSpec.runSeed = seed;
                var composition = host.NewOpenLoopComposition(in seedSpec, field);
                for (var i = 0; i < blocks.Length; i++)
                {
                    var index = i;
                    yield return host.RunBlock(composition, blocks[i], spec.episodesPerSeed, seedSpec, jsonlPath,
                        _ => episodeCounts[index]++);
                }

                composition.Dispose();
                host.Projectiles.ReturnAllToPool();
            }

            field?.Dispose();
            var summary = new Summary
            {
                schema = SchemaId,
                seeds = (int[])spec.seeds.Clone(),
                episodesPerSeed = spec.episodesPerSeed,
                useAsteroidField = baseSpec.useAsteroidField,
                fieldDensityScale = baseSpec.fieldDensityScale,
                blocks = new BlockSummary[blocks.Length],
                episodesJsonl = jsonlPath,
                probes = host.SummarizeProbes(jsonlPath),
            };
            for (var i = 0; i < blocks.Length; i++)
                summary.blocks[i] = new BlockSummary { label = blocks[i].Label, episodes = episodeCounts[i] };

            var summaryPath = jsonlPath.Replace(".jsonl", "-summary.json");
            File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, prettyPrint: true));
            foreach (var block in summary.blocks)
                Debug.Log($"[VelRebaseLane] {block.label}: episodes={block.episodes}");
            Debug.Log($"[VelRebaseLane] summary → {summaryPath}");
        }

        /// <summary>Per archetype, the legacy arm immediately followed by its anchored pair.</summary>
        private static OpponentSpec[] Blocks(SessionSpec spec)
        {
            var blocks = new OpponentSpec[spec.openLoopArchetypes.Length * 2];
            for (var i = 0; i < spec.openLoopArchetypes.Length; i++)
            {
                blocks[2 * i] = OpponentSpec.OpenLoop(spec.openLoopArchetypes[i], ArchetypeDrive.OpenLoopLegacy);
                blocks[2 * i + 1] = OpponentSpec.OpenLoop(spec.openLoopArchetypes[i], ArchetypeDrive.OpenLoopAnchored);
            }
            return blocks;
        }
    }
}
