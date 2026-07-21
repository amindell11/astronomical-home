using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Services;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Executable eval protocol: runs a checkpoint InferenceOnly over a pinned seed list (a fresh pair per seed so every RNG stream replays from that seed), stratified per opponent archetype — equal episode blocks through the pinned roster install, per-archetype W/L/D with the Wilson 95% lower bound on win-rate (draws are non-wins), deliberately NO blended aggregate — and writes per-episode JSONL plus a summary artifact under results/rl-eval/.</summary>
    public static class CheckpointEvaluator
    {
        public const string ResultsFolder = "rl-eval";

        private static readonly OpponentArchetype[] EvalArchetypes =
        {
            OpponentArchetype.Aggressor,
            OpponentArchetype.Evader,
            OpponentArchetype.Orbiter,
            OpponentArchetype.Kiter,
            OpponentArchetype.Dummy,
        };

        [Serializable]
        public struct ArchetypeSummary
        {
            public string archetype;
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
            public string checkpoint;
            public int[] seeds;
            public int episodesPerSeed;
            public bool useAsteroidField;
            public float fieldDensityScale;
            public ArchetypeSummary[] archetypes;
            // The per-archetype tuning signal a W/L/D tally hides (archetypes[] holds the tally).
            public ArchetypeGateSummary[] behavior;
            public string episodesJsonl;
            public string behaviorJsonl;
        }

        public static IEnumerator Run(UnitService units, ArenaContext arena, IProjectileService projectiles,
            HarnessAssets assets, string onnxAssetPath, IReadOnlyList<int> seeds, int episodesPerSeed,
            RewardSpec baseSpec, string tag, Action<Summary> onDone)
        {
            var jsonlPath = EpisodeJsonl.NewRunPath(tag, ResultsFolder);
            var behaviorPath = jsonlPath.Replace(".jsonl", "-behavior.jsonl");
            var summary = new Summary
            {
                checkpoint = onnxAssetPath,
                seeds = new int[seeds.Count],
                episodesPerSeed = episodesPerSeed,
                useAsteroidField = baseSpec.useAsteroidField,
                fieldDensityScale = baseSpec.fieldDensityScale,
                episodesJsonl = jsonlPath,
                behaviorJsonl = behaviorPath,
            };
            for (var i = 0; i < seeds.Count; i++) summary.seeds[i] = seeds[i];

            var outcomes = new List<(string archetype, string outcome)>();
            var behaviorRows = new Dictionary<OpponentArchetype, List<ArchetypeGateRow>>();
            foreach (var a in EvalArchetypes) behaviorRows[a] = new List<ArchetypeGateRow>();
            var field = baseSpec.useAsteroidField ? HarnessField.Spawn(arena, assets, baseSpec.fieldDensityScale) : null;

            foreach (var seed in seeds)
            {
                var spec = baseSpec;
                spec.runSeed = seed;
                var pair = EpisodePair.SpawnWithAgentChooser(units, arena, projectiles, in spec, assets, out var chooser);
                var roster = new OpponentRoster(pair.Baseline, pair.Agent);
                var agent = ShipAgentFactory.ComposeInferenceOnly(pair, chooser, in spec, arena.Offset, onnxAssetPath);
                var driver = new EpisodeLoopDriver(pair, agent, arena.Offset, field);

                foreach (var archetype in EvalArchetypes)
                {
                    for (var episode = 0; episode < episodesPerSeed; episode++)
                    {
                        // Pinned install before RunEpisode's pair-reset (the respawn re-inits the chooser).
                        var draw = roster.Install(archetype, in spec, episode, arena.Offset);
                        ArchetypeGateProbe probe = null;
                        yield return driver.RunEpisode(spec, episode,
                            onBegin: () => probe = new ArchetypeGateProbe(pair.Baseline, pair.Agent,
                                arena.Offset, spec.arenaRadius, in draw),
                            onFixedStep: () => probe.Sample());
                        driver.Runner.RecordOpponent(in draw);
                        var result = driver.Runner.Result;
                        EpisodeJsonl.Append(jsonlPath, in result);
                        outcomes.Add((archetype.ToString(), result.outcome));
                        var row = probe.ToRow(in result);
                        behaviorRows[archetype].Add(row);
                        File.AppendAllText(behaviorPath, row.ToJsonLine() + "\n");
                        probe.Dispose();
                    }
                }

                roster.Dispose();
                UnityEngine.Object.DestroyImmediate(agent.gameObject);
                pair.Dispose();
                projectiles.ReturnAllToPool();
            }

            field?.Dispose();
            summary.archetypes = Summarize(outcomes);
            summary.behavior = new ArchetypeGateSummary[EvalArchetypes.Length];
            for (var i = 0; i < EvalArchetypes.Length; i++)
                summary.behavior[i] = ArchetypeGateSummary.Summarize(
                    EvalArchetypes[i].ToString(), behaviorRows[EvalArchetypes[i]]);

            var summaryPath = jsonlPath.Replace(".jsonl", "-summary.json");
            File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, prettyPrint: true));
            foreach (var a in summary.archetypes)
                Debug.Log($"[CheckpointEvaluator] {onnxAssetPath} vs {a.archetype}: episodes={a.episodes} "
                    + $"W/L/D={a.wins}/{a.losses}/{a.draws} "
                    + $"winRate={a.winRate:F3} wilsonLB95={a.wilsonLowerBound95:F3}");
            // Teacher scorecard: the challenge lever is agentPoolLost (damage the archetype deals); borderHug/displacement flag a degenerate cheese.
            foreach (var b in summary.behavior)
                Debug.Log($"[CheckpointEvaluator] {b.archetype} teacher: agentHPlost={b.meanAgentPoolLostPct:P0} "
                    + $"oppHPlost={b.meanOpponentPoolLostPct:P0} oppShots={b.meanShotsFired:F1} "
                    + $"meanRange={b.meanRange:F1} borderHug={b.meanBorderHugFraction:P0} "
                    + $"maxDisp={b.maxDisplacement:F0} survived={b.survived}/{b.episodes}");
            Debug.Log($"[CheckpointEvaluator] {onnxAssetPath}: summary → {summaryPath}");
            onDone?.Invoke(summary);
        }

        /// <summary>Per-archetype aggregation in first-appearance order; each entry stands alone — no blended win rate exists.</summary>
        internal static ArchetypeSummary[] Summarize(IReadOnlyList<(string archetype, string outcome)> episodes)
        {
            var order = new List<string>();
            var tally = new Dictionary<string, ArchetypeSummary>();
            foreach (var (archetype, outcome) in episodes)
            {
                if (!tally.TryGetValue(archetype, out var entry))
                {
                    entry = new ArchetypeSummary { archetype = archetype };
                    order.Add(archetype);
                }
                entry.episodes++;
                if (outcome == EpisodeOutcome.Win.ToString()) entry.wins++;
                else if (outcome == EpisodeOutcome.Loss.ToString()) entry.losses++;
                else entry.draws++;
                tally[archetype] = entry;
            }

            var result = new ArchetypeSummary[order.Count];
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
