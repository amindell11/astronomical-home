#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Combat.Weapons;
using Damage;
using NUnit.Framework;
using Ships.Registry;
using UnityEngine;
using UnityEngine.TestTools;
using Substrate.Services;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;
using RL.Arena;
using RL.Episodes;
using RL.Hosts;
using RL.Hosts.Lanes;
using RL.Opponents;
using RL.Probes;

namespace Tests.PlayMode
{
    /// <summary>End-to-end proof for the duel lane's machinery: a Lasers shooter fights one block against the still Dummy headless through the host, and the marksmanship probe's row, summary sidecar and table all carry shots that were fired and hits that landed.</summary>
    [Category("AI")]
    public class DuelLanePlayModeTests
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
            arenaHost = new GameObject("[DuelArena]");
            unitService = arenaHost.AddComponent<UnitService>();
            projectiles = ShipServices.Compose(unitService, arenaHost.transform, presentationEnabled: false);
            assets = UnityEditor.AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            Assert.IsNotNull(assets, $"HarnessAssets missing at {HarnessAssets.AssetPath}");
            lasers = TrainingBootstrap.CatalogWeapons().FirstOrDefault(w => w.name == "Lasers");
            Assert.IsNotNull(lasers, "the item catalog must list the Lasers weapon prefab");
            PacingContract.Apply();
            Time.maximumDeltaTime = 1f;
            outDir = Path.Combine(Path.GetTempPath(), "duel-lane-test-" + Guid.NewGuid().ToString("N"));
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
        public IEnumerator DuelBlock_RunsHeadlessAndTheMarksmanshipProbeCountsShotsAndHits()
        {
            var spec = new HarnessSpec
            {
                lane = HarnessLane.Duel,
                seeds = new[] { 3001 },
                tag = "duel-test",
                episodesPerSeed = 1,
                probes = new[] { ProbeSpec.Named(MarksmanshipProbe.ProbeName) },
                duelWeapons = new[] { lasers },
                outDir = outDir,
            };
            var hostObject = new GameObject("[HarnessHost]");
            hostObject.transform.SetParent(arenaHost.transform, false);
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<HarnessHost>();
            host.Initialize(spec, assets, unitService, projectiles);

            var seedSpec = DuelLane.DuelSpec();
            seedSpec.runSeed = spec.seeds[0];
            var jsonlPath = EpisodeJsonl.NewRunPath(spec.tag, CheckpointEvaluator.ResultsFolder, spec.outDir);
            var composition = host.NewDuelComposition(in seedSpec, lasers);
            Assert.AreEqual(0, composition.Pair.Baseline.Weapons.Context.Slots.Count, "the target is unarmed");
            yield return host.RunBlock(composition, DuelLane.Block(lasers, OpponentArchetype.Dummy),
                spec.episodesPerSeed, seedSpec, jsonlPath, null);
            composition.Dispose();
            var artifacts = host.SummarizeProbes(jsonlPath);

            var probeLines = File.ReadAllLines(jsonlPath.Replace(".jsonl", "-marksmanship.jsonl"));
            Assert.AreEqual(spec.episodesPerSeed, probeLines.Length, "one marksmanship row per episode");
            var row = JsonUtility.FromJson<MarksmanshipRow>(probeLines[0]);
            Assert.AreEqual("Lasers-Dummy", row.block);
            Assert.AreEqual("Dummy", row.target);
            Assert.AreEqual("Lasers", row.tally.weapon);
            Assert.Greater(row.tally.fired, 0, "the scripted shooter fired its weapon");
            Assert.Greater(row.tally.hits, 0, "a still target inside the envelope gets hit");
            Assert.LessOrEqual(row.tally.hits, row.tally.fired, "one bolt lands at most once");
            Assert.Greater(row.tally.envelopeSteps, 0, "shots only leave inside the firing envelope");
            Assert.IsFalse(row.tally.hasLock || row.tally.hasCharge, "Lasers expose neither a lock nor a charge");

            Assert.AreEqual(1, artifacts.Length);
            var sidecar = JsonUtility.FromJson<MarksmanshipProbe.Sidecar>(File.ReadAllText(artifacts[0].summary));
            Assert.AreEqual(MarksmanshipProbe.SummarySchemaId, sidecar.schema);
            Assert.AreEqual(1, sidecar.blocks.Length, "one entry per block");
            Assert.AreEqual(row.tally.fired, sidecar.blocks[0].fired);
            Assert.AreEqual(row.tally.hits, sidecar.blocks[0].hits);
            StringAssert.Contains($"| Lasers | Dummy | 1 | ",
                File.ReadAllText(MarksmanshipProbe.TablePath(artifacts[0].summary)));
            Assert.IsEmpty(Directory.GetFiles(outDir, "*-summary.json"),
                "eval_lane.py expects exactly one *-summary.json per run: the lane's, never a probe file");
        }

        [Test]
        public void Sampler_CountsOnlyTheShootersNonCollisionDamageAsHits()
        {
            var spec = DuelLane.DuelSpec();
            using var pair = EpisodePair.SpawnArmedPair(unitService, Vector2.zero, null, projectiles, in spec,
                assets, lasers);
            using var sampler = new MarksmanshipSampler(pair.Agent, pair.Baseline);
            var target = pair.Baseline.Damage;

            target.TakeDamage(Hit(5f, DamageKind.Collision, pair.Agent.Id));
            target.TakeDamage(Hit(5f, DamageKind.Laser, pair.Baseline.Id));
            Assert.AreEqual(0, sampler.Tally.hits, "a ram and another attacker's bolt are not this weapon's hits");

            target.TakeDamage(Hit(5f, DamageKind.Laser, pair.Agent.Id));
            Assert.AreEqual(1, sampler.Tally.hits);
            Assert.AreEqual(5f, sampler.Tally.damageDealt);
            Assert.AreEqual(0, sampler.Tally.selfHits);
        }

        private static DamageInfo Hit(float amount, DamageKind kind, ShipId attacker) =>
            new(amount, kind, attacker, 1f, Vector3.zero, Vector3.zero);
    }
}
#endif
