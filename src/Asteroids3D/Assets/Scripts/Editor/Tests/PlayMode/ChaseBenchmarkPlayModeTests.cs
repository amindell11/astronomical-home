#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game;
using Game.Benchmark;
using Game.Sectors;
using Game.Services;
using Movement.MPC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// Headless entry point for the two-AI chase benchmark (Track B1). Drives the same
    /// ChaseBenchmarkSector prefab a human can watch in-editor, sweeps (field seed x start
    /// offset) episodes, and writes one JSONL row per episode to
    /// <c>results/chase-benchmark/</c> at the repo root.
    ///
    /// The always-on smoke test runs one short episode so the harness stays verified by the
    /// default Workspace suite. The full sweep is opt-in (it takes minutes):
    /// set CHASE_BENCH=1 (optionally CHASE_BENCH_TAG / CHASE_BENCH_SEEDS /
    /// CHASE_BENCH_OFFSETS / CHASE_BENCH_DURATION) and run
    /// <c>scripts/unity_test_agent.ps1 -Mode PlayMode -TestCategory ChaseBenchmark</c>.
    /// </summary>
    [TestFixture]
    [Category("ChaseBenchmark")]
    public class ChaseBenchmarkPlayModeTests
    {
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/ChaseBenchmarkSector.prefab";

        private UnitService unitService;
        private GameServices services;
        private SectorSettings sectorConfig;
        private readonly List<GameObject> created = new();
        private float savedTimeScale;
        private float savedMaxDelta;

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            if (GamePlane.IsConfigured) GamePlane.Reset();
            // Match the shipped game convention (InitScene uses PlaneAxis.Z: the XY plane).
            GamePlane.Configure(PlaneAxis.Z);

            var unitServiceGO = Track(new GameObject("UnitService"));
            unitService = unitServiceGO.AddComponent<UnitService>();
            var objectiveServiceGO = Track(new GameObject("ObjectiveService"));
            var objectiveService = objectiveServiceGO.AddComponent<ObjectiveService>();

            // Mirror MainGameManager.ComposeSession: the environment service owns the active
            // obstacle field (registered by the sector's AsteroidFieldSpawner) and the unit
            // service forwards it to each AI ship it wires.
            var environmentService = new EnvironmentService();
            unitService.BindObstacleFieldProvider(environmentService);
            services = new GameServices(
                unitService, environmentService, objectiveService,
                new CameraService(), new UIService());

            sectorConfig = ScriptableObject.CreateInstance<SectorSettings>();

            savedTimeScale = Time.timeScale;
            savedMaxDelta = Time.maximumDeltaTime;
            Time.timeScale = 20f;
            Time.maximumDeltaTime = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = savedTimeScale;
            Time.maximumDeltaTime = savedMaxDelta;
            SolverBuffers.SamplerSeedOverride = null;
            ChaseBenchmarkModule.PendingConfig = null;

            unitService?.Clear();
            foreach (var go in created)
                if (go) UnityEngine.Object.DestroyImmediate(go);
            created.Clear();

            if (sectorConfig) UnityEngine.Object.DestroyImmediate(sectorConfig);
            GamePlane.Reset();
            AudioListener.pause = false;
        }

        private GameObject Track(GameObject go)
        {
            created.Add(go);
            return go;
        }

        // ── Episode driver ──────────────────────────────────────────────────────

        private IEnumerator RunEpisode(ChaseRunConfig cfg, List<ChaseRunResult> results)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectorPrefabPath);
            Assert.IsNotNull(prefab, $"Missing benchmark sector prefab at {SectorPrefabPath}");

            ChaseBenchmarkModule.PendingConfig = cfg;

            // Mirror MainGameManager.LoadSector: instantiate under an inactive holder so content
            // does not Awake before adoption wires it.
            var holder = new GameObject("SectorLoad") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);
            var sectorGO = UnityEngine.Object.Instantiate(prefab, holder.transform);
            var sector = sectorGO.GetComponent<Sector>();
            Assert.IsNotNull(sector, "Benchmark prefab has no Sector component");

            sector.Initialize(services, sectorConfig, null);
            yield return sector.Setup();
            sectorGO.transform.SetParent(null, true);
            UnityEngine.Object.Destroy(holder);

            var module = sectorGO.GetComponent<ChaseBenchmarkModule>();
            Assert.IsNotNull(module, "Benchmark prefab has no ChaseBenchmarkModule");

            // Wall-clock guard: sim runs time-scaled, MPC solve is the real cost.
            var deadline = Time.realtimeSinceStartup + 60f + cfg.duration * 10f;
            while (!module.EpisodeComplete && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(module.EpisodeComplete,
                $"Episode (seed {cfg.fieldSeed}, offset {cfg.fieldOffset}) did not finish before the wall-clock guard.");
            results.Add(module.LastResult);

            yield return sector.Teardown();
            UnityEngine.Object.Destroy(sectorGO);
            yield return null;
        }

        private static string ResultsDir()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            var dir = Path.Combine(repoRoot, "results", "chase-benchmark");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string WriteJsonl(string tag, List<ChaseRunResult> results)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(ResultsDir(), $"{stamp}-{tag}.jsonl");
            using (var w = new StreamWriter(path, false))
                foreach (var r in results)
                    w.WriteLine(r.ToJsonLine());
            Debug.Log($"[ChaseBenchmark] wrote {results.Count} episode rows to {path}");
            return path;
        }

        // ── Tests ───────────────────────────────────────────────────────────────

        /// <summary>Always-on harness check: one short episode must complete with sane metrics.</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Smoke_SingleShortEpisode_ProducesMetrics()
        {
            var results = new List<ChaseRunResult>();
            yield return RunEpisode(new ChaseRunConfig
            {
                fieldSeed = 101,
                fieldOffset = Vector2.zero,
                duration = 6f,
                pinSamplerSeed = false,
                tag = "smoke",
            }, results);

            Assert.AreEqual(1, results.Count);
            var r = results[0];
            Assert.AreEqual(ChaseRunResult.SchemaId, r.schema);
            Assert.Greater(r.duration, 5.9f, "Episode did not run its configured sim duration");
            Assert.Greater(r.pursuer.meanSpeed, 0.1f, "Pursuer never moved — AI wiring broken?");
            Assert.Greater(r.evader.meanSpeed, 0.1f, "Evader never moved — AI wiring broken?");
            Assert.Greater(r.meanDistance, 0f);
            Assert.IsNull(SolverBuffers.SamplerSeedOverride,
                "Sampler seed override must be cleared by module teardown");
        }

        /// <summary>
        /// Full statistical sweep (opt-in via CHASE_BENCH=1). Field seeds x start offsets,
        /// one JSONL row per episode. Aggregate/compare offline with
        /// scripts/chase-benchmark/compare_chase_benchmark.py.
        /// </summary>
        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator Benchmark_SeedSweep_WritesJsonl()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CHASE_BENCH")))
                Assert.Ignore("Set CHASE_BENCH=1 to run the full chase benchmark sweep.");

            var tag = Environment.GetEnvironmentVariable("CHASE_BENCH_TAG") ?? "run";
            var seedCount = EnvInt("CHASE_BENCH_SEEDS", 5);
            var offsetCount = EnvInt("CHASE_BENCH_OFFSETS", 2);
            var duration = EnvFloat("CHASE_BENCH_DURATION", 40f);

            // Non-chunk-aligned plane translations of the field: same seed, different local layout
            // under the (fixed) ship starts.
            var offsets = new[]
            {
                Vector2.zero,
                new Vector2(137.3f, 91.7f),
                new Vector2(-211.9f, 53.1f),
                new Vector2(64.7f, -173.9f),
            };

            var results = new List<ChaseRunResult>();
            for (var s = 0; s < seedCount; s++)
            for (var o = 0; o < Mathf.Min(offsetCount, offsets.Length); o++)
            {
                yield return RunEpisode(new ChaseRunConfig
                {
                    fieldSeed = 101 + s * 17,
                    fieldOffset = offsets[o],
                    duration = duration,
                    pinSamplerSeed = false,
                    tag = tag,
                }, results);
            }

            Assert.AreEqual(seedCount * Mathf.Min(offsetCount, offsets.Length), results.Count);
            WriteJsonl(tag, results);
        }

        private static int EnvInt(string name, int fallback)
        {
            var v = Environment.GetEnvironmentVariable(name);
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
        }

        private static float EnvFloat(string name, float fallback)
        {
            var v = Environment.GetEnvironmentVariable(name);
            return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;
        }
    }
}
#endif
