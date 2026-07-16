using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Services;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Executable eval protocol: runs a checkpoint InferenceOnly over a pinned seed list (a fresh pair per seed so every RNG stream replays from that seed), aggregates W/L/D with the Wilson 95% lower bound on win-rate (draws are non-wins), and writes per-episode JSONL plus a summary artifact under results/rl-eval/.</summary>
    public static class CheckpointEvaluator
    {
        public const string ResultsFolder = "rl-eval";

        [Serializable]
        public struct Summary
        {
            public string checkpoint;
            public int[] seeds;
            public int episodesPerSeed;
            public int episodes;
            public int wins;
            public int losses;
            public int draws;
            public float winRate;
            public float wilsonLowerBound95;
            public string episodesJsonl;
        }

        public static IEnumerator Run(UnitService units, ArenaContext arena, IProjectileService projectiles,
            string onnxAssetPath, IReadOnlyList<int> seeds, int episodesPerSeed, RewardSpec baseSpec,
            string tag, Action<Summary> onDone)
        {
            var jsonlPath = EpisodeJsonl.NewRunPath(tag, ResultsFolder);
            var summary = new Summary
            {
                checkpoint = onnxAssetPath,
                seeds = new int[seeds.Count],
                episodesPerSeed = episodesPerSeed,
                episodesJsonl = jsonlPath,
            };
            for (var i = 0; i < seeds.Count; i++) summary.seeds[i] = seeds[i];

            foreach (var seed in seeds)
            {
                var spec = baseSpec;
                spec.runSeed = seed;
                var pair = EpisodePair.SpawnWithAgentChooser(units, arena, projectiles, in spec, out var chooser);
                var agent = ShipAgentFactory.ComposeInferenceOnly(pair, chooser, in spec, arena.Offset, onnxAssetPath);
                var driver = new EpisodeLoopDriver(pair, agent, arena.Offset);

                for (var episode = 0; episode < episodesPerSeed; episode++)
                {
                    yield return driver.RunEpisode(spec, episode);
                    var result = driver.Runner.Result;
                    EpisodeJsonl.Append(jsonlPath, in result);
                    Tally(ref summary, result.outcome);
                }

                UnityEngine.Object.DestroyImmediate(agent.gameObject);
                pair.Dispose();
                projectiles.ReturnAllToPool();
            }

            summary.episodes = summary.wins + summary.losses + summary.draws;
            summary.winRate = summary.episodes > 0 ? summary.wins / (float)summary.episodes : 0f;
            summary.wilsonLowerBound95 = EvalProtocol.WilsonLowerBound(summary.wins, summary.episodes);

            var summaryPath = jsonlPath.Replace(".jsonl", "-summary.json");
            File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, prettyPrint: true));
            Debug.Log($"[CheckpointEvaluator] {onnxAssetPath}: episodes={summary.episodes} "
                + $"W/L/D={summary.wins}/{summary.losses}/{summary.draws} "
                + $"winRate={summary.winRate:F3} wilsonLB95={summary.wilsonLowerBound95:F3} → {summaryPath}");
            onDone?.Invoke(summary);
        }

        private static void Tally(ref Summary summary, string outcome)
        {
            if (outcome == EpisodeOutcome.Win.ToString()) summary.wins++;
            else if (outcome == EpisodeOutcome.Loss.ToString()) summary.losses++;
            else summary.draws++;
        }
    }
}
