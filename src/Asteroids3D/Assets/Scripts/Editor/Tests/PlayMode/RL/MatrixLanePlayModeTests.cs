#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Combat.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Substrate.Services;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;
using RL.Arena;
using RL.Episodes;
using RL.Hosts;
using RL.Hosts.Lanes;
using RL.Probes;

namespace Tests.PlayMode
{
    /// <summary>One Lasers against Lasers episode through the host: the matrix row names a winner, both samplers stepped, and the sidecar holds that one pair.</summary>
    [Category("AI")]
    public class MatrixLanePlayModeTests
    {
        private GameObject arenaHost;
        private UnitService unitService;
        private ProjectileService projectiles;
        private HarnessAssets assets;
        private WeaponComponent lasers;
        private string outDir;

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            arenaHost = new GameObject("[MatrixArena]");
            unitService = arenaHost.AddComponent<UnitService>();
            projectiles = ShipServices.Compose(unitService, arenaHost.transform, presentationEnabled: false);
            assets = UnityEditor.AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            Assert.IsNotNull(assets, $"HarnessAssets missing at {HarnessAssets.AssetPath}");
            lasers = TrainingBootstrap.CatalogWeapons().FirstOrDefault(w => w.name == "Lasers");
            Assert.IsNotNull(lasers, "the item catalog must list the Lasers weapon prefab");
            PacingContract.Apply();
            Time.maximumDeltaTime = 1f;
            outDir = Path.Combine(Path.GetTempPath(), "matrix-lane-test-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            projectiles?.ReturnAllToPool();
            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            AudioListener.pause = false;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MatrixBlock_RunsHeadlessAndRecordsAWinnerAndBothTallies()
        {
            var spec = new HarnessSpec
            {
                lane = HarnessLane.Matrix,
                seeds = new[] { 3001 },
                tag = "matrix-test",
                episodesPerSeed = 1,
                probes = new[] { ProbeSpec.Named(MatrixProbe.ProbeName) },
                matrixWeapons = new[] { lasers },
                outDir = outDir,
            };
            var hostObject = new GameObject("[HarnessHost]");
            hostObject.transform.SetParent(arenaHost.transform, false);
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<HarnessHost>();
            host.Initialize(spec, assets, unitService, projectiles);

            var seedSpec = MatrixLane.MatrixSpec();
            seedSpec.runSeed = spec.seeds[0];
            var jsonlPath = EpisodeJsonl.NewRunPath(spec.tag, CheckpointEvaluator.ResultsFolder, spec.outDir);
            var composition = host.NewMatrixComposition(in seedSpec, lasers, lasers);
            yield return host.RunBlock(composition, MatrixLane.Block(lasers, lasers), spec.episodesPerSeed,
                seedSpec, jsonlPath, null);
            composition.Dispose();
            var artifacts = host.SummarizeProbes(jsonlPath);

            var probeLines = File.ReadAllLines(jsonlPath.Replace(".jsonl", "-matrix.jsonl"));
            Assert.AreEqual(spec.episodesPerSeed, probeLines.Length, "one matrix row per episode");
            var row = JsonUtility.FromJson<MatrixRow>(probeLines[0]);
            Assert.AreEqual(MatrixRow.SchemaId, row.schema);
            Assert.AreEqual("Lasers-Lasers", row.pair);
            Assert.That(row.winner, Is.EqualTo(MatrixProbe.SideA).Or.EqualTo(MatrixProbe.SideB)
                .Or.EqualTo(MatrixProbe.Draw));
            Assert.Greater(row.tallyA.steps, 0, "side A was sampled while both ships lived");
            Assert.Greater(row.tallyB.steps, 0, "side B was sampled while both ships lived");

            Assert.AreEqual(1, artifacts.Length);
            var sidecar = JsonUtility.FromJson<MatrixProbe.Sidecar>(File.ReadAllText(artifacts[0].summary));
            Assert.AreEqual(MatrixProbe.SummarySchemaId, sidecar.schema);
            Assert.AreEqual(1, sidecar.pairs.Length, "one entry per pair");
            Assert.AreEqual("Lasers", sidecar.pairs[0].weaponA);
            Assert.AreEqual("Lasers", sidecar.pairs[0].weaponB);
            StringAssert.Contains("mean vs others",
                File.ReadAllText(MatrixProbe.TablePath(artifacts[0].summary)));
            Assert.IsEmpty(Directory.GetFiles(outDir, "*-summary.json"),
                "eval_lane.py expects exactly one *-summary.json per run: the lane's, never a probe file");
        }
    }
}
#endif
