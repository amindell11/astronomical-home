using System;
using System.Collections;
using System.IO;
using Combat.Weapons;
using UnityEngine;
using RL.Episodes;
using RL.Hosts;
using RL.Opponents;
using RL.Probes;
using RL.Reward;

namespace RL.Hosts.Lanes
{
    /// <summary>The matrix lane client: for each seed, one harness composition per unordered weapon pair, a weapon against itself included. Both ships are scripted. The matrix probe's sidecar is the instrument; this summary fingerprints the run.</summary>
    public sealed class MatrixLane : ILaneClient
    {
        IEnumerator ILaneClient.Run(HarnessHost host, HarnessSpec spec) => RunLane(host, spec);

        public const string SchemaId = "matrix-lane-v1";
        /// <summary>An episode ends on a death or here: 90 sim-seconds.</summary>
        public const int TimeoutDecisions = 450;

        public static bool SidesSwapped(int episodeIndex) => (episodeIndex & 1) == 1;

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
            public int timeoutDecisions;
            public BlockSummary[] blocks;
            public string episodesJsonl;
            public ProbeArtifacts[] probes;
        }

        /// <summary>The matrix episode spec: the default empty arena, episodes cut at <see cref="TimeoutDecisions"/>.</summary>
        public static RewardSpec MatrixSpec()
        {
            var spec = RewardSpec.Default;
            spec.timeoutDecisions = TimeoutDecisions;
            return spec;
        }

        public static string PairLabel(WeaponComponent weaponA, WeaponComponent weaponB) =>
            $"{weaponA.name}-{weaponB.name}";

        public static OpponentSpec Block(WeaponComponent weaponA, WeaponComponent weaponB) => new()
        {
            kind = OpponentKind.Archetype,
            archetype = OpponentArchetype.Aggressor,
            labelOverride = PairLabel(weaponA, weaponB),
        };

        public static IEnumerator RunLane(HarnessHost host, HarnessSpec spec)
        {
            var baseSpec = MatrixSpec();
            var jsonlPath = EpisodeJsonl.NewRunPath(spec.tag, CheckpointEvaluator.ResultsFolder, spec.outDir);
            var weapons = spec.matrixWeapons;
            var blocks = new BlockSummary[weapons.Length * (weapons.Length + 1) / 2];
            var cursor = 0;
            for (var i = 0; i < weapons.Length; i++)
                for (var j = i; j < weapons.Length; j++)
                    blocks[cursor++].label = Block(weapons[i], weapons[j]).Label;

            foreach (var seed in spec.seeds)
            {
                var seedSpec = baseSpec;
                seedSpec.runSeed = seed;
                // One composition per pair: the loadout binds at spawn, so a weapon change is a fresh pair.
                var index = 0;
                for (var i = 0; i < weapons.Length; i++)
                {
                    for (var j = i; j < weapons.Length; j++)
                    {
                        var blockIndex = index++;
                        var composition = host.NewMatrixComposition(in seedSpec, weapons[i], weapons[j]);
                        yield return host.RunBlock(composition, Block(weapons[i], weapons[j]), spec.episodesPerSeed,
                            seedSpec, jsonlPath, _ => blocks[blockIndex].episodes++);
                        composition.Dispose();
                        host.Projectiles.ReturnAllToPool();
                    }
                }
            }

            var summary = new Summary
            {
                schema = SchemaId,
                seeds = (int[])spec.seeds.Clone(),
                episodesPerSeed = spec.episodesPerSeed,
                timeoutDecisions = baseSpec.timeoutDecisions,
                blocks = blocks,
                episodesJsonl = jsonlPath,
                probes = host.SummarizeProbes(jsonlPath),
            };

            var summaryPath = jsonlPath.Replace(".jsonl", "-summary.json");
            File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, prettyPrint: true));
            foreach (var block in summary.blocks)
                Debug.Log($"[MatrixLane] {block.label}: episodes={block.episodes}");
            Debug.Log($"[MatrixLane] summary → {summaryPath}");
        }
    }
}
