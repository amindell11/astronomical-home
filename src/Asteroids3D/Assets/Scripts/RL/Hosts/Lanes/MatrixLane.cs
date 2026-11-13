using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Combat.Weapons;
using UnityEngine;
using RL.Episodes;
using RL.Episodes.Compositions;
using RL.Hosts;
using RL.Opponents;
using RL.Probes;
using RL.Reward;

namespace RL.Hosts.Lanes
{
    /// <summary>The matrix lane client. For each seed, one block per unordered weapon pair, or one block per roster ship against a fixed reference. Both ships are scripted. The matrix probe's sidecar is the instrument; this summary fingerprints the run.</summary>
    public sealed class MatrixLane : ILaneClient
    {
        IEnumerator ILaneClient.Run(HarnessHost host, HarnessSpec spec) => RunLane(host, spec);

        public const string SchemaId = "matrix-lane-v1";
        /// <summary>An episode ends on a death or here: 90 sim-seconds.</summary>
        public const int TimeoutDecisions = 450;

        public static bool SidesSwapped(int episodeIndex) => (episodeIndex & 1) == 1;

        /// <summary>Episodes 0 and 1 share this sample, then 2 and 3. The odd episode only swaps sides.</summary>
        public static int PoseIndex(int episodeIndex) => episodeIndex >> 1;

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

        // Authored pilots steer off their own border, so only a death or the timeout ends a roster fight.
        public static RewardSpec RosterSpec()
        {
            var spec = MatrixSpec();
            spec.arenaRadius = float.MaxValue;
            return spec;
        }

        private static bool IsRoster(HarnessSpec spec) => spec.matrixRoster is { Length: > 0 };

        public static string PairLabel(WeaponComponent weaponA, WeaponComponent weaponB) =>
            $"{weaponA.name}-{weaponB.name}";

        public static OpponentSpec Block(WeaponComponent weaponA, WeaponComponent weaponB) => new()
        {
            kind = OpponentKind.Archetype,
            archetype = OpponentArchetype.Aggressor,
            labelOverride = PairLabel(weaponA, weaponB),
        };

        public static string RosterBlockLabel(MatrixSide reference, MatrixSide roster) =>
            reference.Label + "_vs_" + roster.Label;

        public static OpponentSpec Block(MatrixSide reference, MatrixSide roster) => new()
        {
            labelOverride = RosterBlockLabel(reference, roster),
        };

        public static IEnumerator RunLane(HarnessHost host, HarnessSpec spec)
        {
            var baseSpec = IsRoster(spec) ? RosterSpec() : MatrixSpec();
            var jsonlPath = EpisodeJsonl.NewRunPath(spec.tag, CheckpointEvaluator.ResultsFolder, spec.outDir);
            var matchups = Matchups(host, spec);
            var blocks = new BlockSummary[matchups.Count];
            for (var i = 0; i < matchups.Count; i++)
                blocks[i].label = matchups[i].block.Label;

            foreach (var seed in spec.seeds)
            {
                var seedSpec = baseSpec;
                seedSpec.runSeed = seed;
                // One composition per block: the loadout binds at spawn, so a side change is a fresh pair.
                for (var i = 0; i < matchups.Count; i++)
                {
                    var blockIndex = i;
                    var composition = matchups[i].spawn(seedSpec);
                    yield return host.RunBlock(composition, matchups[i].block, spec.episodesPerSeed,
                        seedSpec, jsonlPath, _ => blocks[blockIndex].episodes++);
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
                Debug.Log($"[MatrixLane] {block.label}: episodes={block.episodes}");
            Debug.Log($"[MatrixLane] summary → {summaryPath}");
        }

        private static List<(OpponentSpec block, Func<RewardSpec, IHarnessComposition> spawn)> Matchups(
            HarnessHost host, HarnessSpec spec)
        {
            var matchups = new List<(OpponentSpec, Func<RewardSpec, IHarnessComposition>)>();
            if (IsRoster(spec))
            {
                var reference = spec.matrixReference;
                foreach (var roster in spec.matrixRoster)
                    matchups.Add((Block(reference, roster),
                        seedSpec => host.NewMatrixComposition(in seedSpec, reference, roster)));
                return matchups;
            }

            var weapons = spec.matrixWeapons;
            for (var i = 0; i < weapons.Length; i++)
            {
                for (var j = i; j < weapons.Length; j++)
                {
                    var weaponA = weapons[i];
                    var weaponB = weapons[j];
                    matchups.Add((Block(weaponA, weaponB),
                        seedSpec => host.NewMatrixComposition(in seedSpec, weaponA, weaponB)));
                }
            }
            return matchups;
        }
    }
}
