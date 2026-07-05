#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Game;
using Movement.MPC;
using UnityEngine;

namespace Tests.PlayMode.ChaseBenchmark
{
    /// <summary>
    /// Drives the chase-benchmark sweep: for each config it pins the MPC sampler seed,
    /// builds a fresh two-AI scenario, simulates a fixed number of physics steps, records
    /// the per-ship metrics, and tears everything down before the next run. Metrics are
    /// reported as a list of per-run rows; aggregation (mean ± spread) is the diff script's
    /// job, matching the statistical evaluation model (no reliance on bit-reproducibility).
    /// </summary>
    public static class ChaseBenchmarkRunner
    {
        /// <summary>The default sweep: a few start-offsets × distinct seed biases.</summary>
        public static List<ChaseRunConfig> DefaultSweep(int ticks = 800)
        {
            var offsets = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(600f, 0f),
                new Vector2(0f, 600f),
            };
            var configs = new List<ChaseRunConfig>(offsets.Length);
            for (var i = 0; i < offsets.Length; i++)
            {
                var c = ChaseRunConfig.Default(i);
                c.label = $"offset{i}";
                c.startOffset = offsets[i];
                c.ticks = ticks;
                configs.Add(c);
            }
            return configs;
        }

        /// <summary>
        /// Runs every config in order, appending a <see cref="ChaseRunResult"/> per run to
        /// <paramref name="results"/> and (optionally) writing JSONL to <paramref name="logDir"/>.
        /// Yields fixed-update steps, so drive it from a <c>[UnityTest]</c> coroutine.
        /// </summary>
        public static IEnumerator RunSweep(
            IReadOnlyList<ChaseRunConfig> configs,
            List<ChaseRunResult> results,
            string logDir = null)
        {
            var logger = logDir != null ? new ChaseBenchmarkLogger(logDir) : null;
            try
            {
                foreach (var cfg in configs)
                {
                    SolverBuffers.SeedBias = cfg.seedBias;
                    var registry = new ShipRegistry();
                    ChaseBenchmarkScenario scenario = null;
                    try
                    {
                        scenario = new ChaseBenchmarkScenario(cfg, registry);
                        for (var i = 0; i < cfg.ticks; i++)
                            yield return new WaitForFixedUpdate();

                        var row = scenario.BuildResult();
                        results.Add(row);
                        logger?.WriteRow(row);
                    }
                    finally
                    {
                        scenario?.Dispose();
                        registry.Dispose();
                    }
                }
            }
            finally
            {
                SolverBuffers.SeedBias = 0;
                logger?.Dispose();
            }
        }
    }
}
#endif
