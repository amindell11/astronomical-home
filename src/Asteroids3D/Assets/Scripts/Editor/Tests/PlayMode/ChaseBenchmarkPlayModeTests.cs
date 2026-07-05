#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Tests.PlayMode.ChaseBenchmark;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// B1 chase eval harness — the measuring stick both chase-nav tracks report against.
    /// A two-AI scenario (pursuer MaintainRange vs evader Flee) in a deterministic BigField;
    /// headline metrics are per-ship (collisions, impact impulse, mean speed, control chatter,
    /// solve time), aggregated over a small seed sweep. Determinism is statistical, not
    /// bit-exact (the sampler seed still folds in per-tick ship position), matching the plan.
    /// Run headless: <c>pwsh scripts/unity_test_agent.ps1 -Mode PlayMode -TestCategory ChaseBenchmark</c>.
    /// </summary>
    [Category("ChaseBenchmark")]
    [Category("Slow")]
    public class ChaseBenchmarkPlayModeTests : PlayModeWorldFixture
    {
        [UnityTest]
        public IEnumerator SmokeRun_ShipsMoveAndSolverRuns()
        {
            var cfg = ChaseRunConfig.Default(0);
            cfg.ticks = 120;
            var results = new List<ChaseRunResult>();
            yield return ChaseBenchmarkRunner.RunSweep(new[] { cfg }, results, logDir: null);

            Assert.AreEqual(1, results.Count, "sweep did not produce a row");
            var r = results[0];
            Assert.Greater(r.pursuer.meanSpeed, 0f, "pursuer never moved");
            Assert.Greater(r.evader.meanSpeed, 0f, "evader never moved");
            Assert.Greater(r.pursuer.meanSolveMs, 0f, "MPC solver did not run (editor timing)");
            Assert.IsFalse(float.IsNaN(r.pursuer.chatterPerSec), "chatter NaN");
            Assert.IsFalse(float.IsInfinity(r.minDistance), "minDistance never updated");
        }

        [UnityTest]
        public IEnumerator Baseline_Sweep_WritesJsonl()
        {
            var configs = ChaseBenchmarkRunner.DefaultSweep(ticks: 300);
            var results = new List<ChaseRunResult>();
            var dir = ChaseBenchmarkLogger.DefaultDir();

            yield return ChaseBenchmarkRunner.RunSweep(configs, results, dir);

            Assert.AreEqual(configs.Count, results.Count, "missing run rows");
            foreach (var r in results)
            {
                AssertFinite(r.pursuer.meanSpeed, "pursuer.meanSpeed");
                AssertFinite(r.evader.meanSpeed, "evader.meanSpeed");
                AssertFinite(r.pursuer.chatterPerSec, "pursuer.chatter");
                Assert.GreaterOrEqual(r.pursuer.collisions, 0);
                Assert.IsFalse(float.IsInfinity(r.minDistance), "minDistance never updated");
            }
            Assert.IsTrue(File.Exists(Path.Combine(dir, "latest_runs.jsonl")), "JSONL not written");
            Debug.Log($"[ChaseBenchmark] baseline: {results.Count} rows -> {dir}");
        }

        [UnityTest]
        public IEnumerator Determinism_SameSeeds_StableDistributions()
        {
            var configs = ChaseBenchmarkRunner.DefaultSweep(ticks: 220).GetRange(0, 2);
            var a = new List<ChaseRunResult>();
            var b = new List<ChaseRunResult>();
            yield return ChaseBenchmarkRunner.RunSweep(configs, a, null);
            yield return ChaseBenchmarkRunner.RunSweep(configs, b, null);

            // Statistical stability (not bit-exact): identical seed biases + field should
            // reproduce the per-metric distribution to within a modest tolerance.
            AssertClose(Mean(a, r => r.pursuer.meanSpeed), Mean(b, r => r.pursuer.meanSpeed),
                0.20f, 0.5f, "pursuer meanSpeed");
            AssertClose(Mean(a, r => r.pursuer.chatterPerSec), Mean(b, r => r.pursuer.chatterPerSec),
                0.30f, 1.0f, "pursuer chatter");
            AssertClose(Mean(a, r => r.meanDistanceBehind), Mean(b, r => r.meanDistanceBehind),
                0.25f, 2.0f, "meanDistanceBehind");
        }

        private static void AssertFinite(float v, string what) =>
            Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), $"{what} not finite: {v}");

        private static float Mean(List<ChaseRunResult> rs, Func<ChaseRunResult, float> sel)
        {
            double s = 0;
            foreach (var r in rs) s += sel(r);
            return (float)(s / Mathf.Max(1, rs.Count));
        }

        private static void AssertClose(float x, float y, float relTol, float absFloor, string what)
        {
            var tol = Mathf.Max(absFloor, relTol * 0.5f * (Mathf.Abs(x) + Mathf.Abs(y)));
            Assert.LessOrEqual(Mathf.Abs(x - y), tol,
                $"{what} not stable across identical sweeps: {x:F3} vs {y:F3} (tol {tol:F3})");
        }
    }
}
#endif
