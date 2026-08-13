#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Game.RLHarness;
using Game.Services;
using NUnit.Framework;
using Tests.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>End-to-end proof for the sentence lane's machinery: a short drift-hold block runs headless through the host with the controller probe, ends every episode by rule, and the probe samples a live solver — the armed all-zero sentence solved instead of idling.</summary>
    [Category("AI")]
    public class RLSentencePlayModeTests
    {
        private GameObject arenaHost;
        private UnitService unitService;
        private ArenaContext arena;
        private ProjectileService projectiles;
        private HarnessAssets assets;
        private string outDir;

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            GameSettings.SetPresentationEnabled(false);
            arenaHost = new GameObject("[SentenceArena]");
            unitService = arenaHost.AddComponent<UnitService>();
            arena = TestArena.On(arenaHost, unitService.ActiveRegistry);
            unitService.SetArena(arena);
            projectiles = new ProjectileService(arenaHost.transform);
            unitService.SetProjectiles(projectiles);
            assets = UnityEditor.AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            Assert.IsNotNull(assets, $"HarnessAssets missing at {HarnessAssets.AssetPath}");
            PacingContract.Apply();
            Time.maximumDeltaTime = 1f;
            outDir = Path.Combine(Path.GetTempPath(), "rl-sentence-test-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            projectiles?.ReturnAllToPool();
            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            GameSettings.SetPresentationEnabled(true);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            AudioListener.pause = false;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator SentenceBlock_RunsHeadlessAndTheControllerProbeSamplesTheSolver()
        {
            var spec = new SessionSpec
            {
                lane = SessionLane.Sentence,
                seeds = new[] { EvalProtocol.HeldOutSeeds[0] },
                tag = "sentence-test",
                episodesPerSeed = 2,
                fieldDensityScale = EvalProtocol.CanonicalFieldDensityScale,
                probes = new[] { ProbeSpec.Named(ControllerProbe.ProbeName) },
                sentenceRows = new[] { SentenceRow.DriftHold },
                outDir = outDir,
            };
            var hostObject = new GameObject("[HarnessSessionHost]");
            hostObject.transform.SetParent(arenaHost.transform, false);
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<HarnessSessionHost>();
            host.Initialize(spec, assets, unitService, arena, projectiles);

            // Drift-hold never kills: the episode must end by the shortened timeout rule, not test patience.
            var seedSpec = RewardSpec.Default;
            seedSpec.runSeed = spec.seeds[0];
            seedSpec.timeoutDecisions = 25;
            var jsonlPath = EpisodeJsonl.NewRunPath(spec.tag, CheckpointEvaluator.ResultsFolder, spec.outDir);
            var composition = host.NewSentenceComposition(in seedSpec, null, SentenceRow.DriftHold);
            yield return host.RunBlock(composition, SentenceRows.Block(SentenceRow.DriftHold), spec.episodesPerSeed,
                seedSpec, jsonlPath, null);
            composition.Dispose();
            var artifacts = host.SummarizeProbes(jsonlPath);

            Assert.AreEqual(spec.episodesPerSeed, File.ReadAllLines(jsonlPath).Length, "one row per episode");
            var probeLines = File.ReadAllLines(jsonlPath.Replace(".jsonl", "-controller.jsonl"));
            Assert.AreEqual(spec.episodesPerSeed, probeLines.Length, "one controller row per episode");
            foreach (var line in probeLines)
            {
                var row = JsonUtility.FromJson<ControllerRow>(line);
                Assert.AreEqual("drift-hold", row.opponent, "blocks label by row token");
                Assert.Greater(row.steps, 0, "the probe sampled a live solver — the all-zero sentence solved");
            }
            Assert.AreEqual(1, artifacts.Length);
            Assert.IsTrue(File.Exists(artifacts[0].summary), "the probe summary sidecar landed");
        }
    }
}
#endif
