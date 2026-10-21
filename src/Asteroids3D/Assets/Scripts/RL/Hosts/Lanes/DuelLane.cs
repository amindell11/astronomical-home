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
    /// <summary>The duel lane client: for each seed, one harness composition per selected weapon — a scripted shooter carrying that weapon alone in its primary weapon slot — fought in an empty arena against an unarmed target, one block per target opponent archetype (Dummy still, Orbiter crossing). The marksmanship probe's sidecar is the instrument; this summary just fingerprints the run.</summary>
    public sealed class DuelLane : ILaneClient
    {
        IEnumerator ILaneClient.Run(HarnessHost host, HarnessSpec spec) => RunLane(host, spec);

        public const string SchemaId = "duel-summary-v1";
        /// <summary>An episode ends on a death or here: 60 sim-seconds.</summary>
        public const int TimeoutDecisions = 300;

        private static readonly OpponentArchetype[] Targets = { OpponentArchetype.Dummy, OpponentArchetype.Orbiter };

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

        /// <summary>The duel's episode spec: the default empty arena, episodes cut at <see cref="TimeoutDecisions"/>.</summary>
        public static RewardSpec DuelSpec()
        {
            var spec = RewardSpec.Default;
            spec.timeoutDecisions = TimeoutDecisions;
            return spec;
        }

        /// <summary>One weapon against one target; the label names both because every weapon meets the same targets.</summary>
        public static OpponentSpec Block(WeaponComponent weapon, OpponentArchetype target) => new()
        {
            kind = OpponentKind.Archetype,
            archetype = target,
            labelOverride = $"{weapon.name}-{target}",
        };

        public static IEnumerator RunLane(HarnessHost host, HarnessSpec spec)
        {
            var baseSpec = DuelSpec();
            var jsonlPath = EpisodeJsonl.NewRunPath(spec.tag, CheckpointEvaluator.ResultsFolder, spec.outDir);
            var weapons = spec.duelWeapons;
            var blocks = new BlockSummary[weapons.Length * Targets.Length];
            for (var i = 0; i < blocks.Length; i++)
                blocks[i].label = Block(weapons[i / Targets.Length], Targets[i % Targets.Length]).Label;

            foreach (var seed in spec.seeds)
            {
                var seedSpec = baseSpec;
                seedSpec.runSeed = seed;
                // One harness composition per weapon: the loadout binds at spawn, so a weapon change is a fresh pair.
                for (var w = 0; w < weapons.Length; w++)
                {
                    var composition = host.NewDuelComposition(in seedSpec, weapons[w]);
                    for (var t = 0; t < Targets.Length; t++)
                    {
                        var index = w * Targets.Length + t;
                        yield return host.RunBlock(composition, Block(weapons[w], Targets[t]), spec.episodesPerSeed,
                            seedSpec, jsonlPath, _ => blocks[index].episodes++);
                    }
                    composition.Dispose();
                    host.Projectiles.ReturnAllToPool();
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
                Debug.Log($"[DuelLane] {block.label}: episodes={block.episodes}");
            Debug.Log($"[DuelLane] summary → {summaryPath}");
        }
    }
}
