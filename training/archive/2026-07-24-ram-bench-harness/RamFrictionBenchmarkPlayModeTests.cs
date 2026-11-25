#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.RLHarness;
using Game.Services;
using NUnit.Framework;
using Ships;
using Tests.Common;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>Permanent ram-pin regression benchmark (balance program B6/B7). Reproduces the exploit with the REAL
    /// exploiter — mirror self-play of the frozen ramming checkpoint (<c>ShipCombat-999950</c>, selfplay2 best-on-record)
    /// — and runs an A/B between the pre-fix ship-ship physics and the layer-split fix, in one pass over the same seeds:
    /// <list type="bullet">
    /// <item><b>baseline</b> — the old behavior: hull↔hull (layer Ship) collision ON at the unchanged BouncyShip friction
    /// 0.6, bumper spheres disabled. This is the ram-pin the exploit rides.</item>
    /// <item><b>layer-split</b> — the shipped fix: hull↔hull ship-ship OFF, a frictionless <c>ShipBody</c> bumper sphere
    /// (ShipBody↔ShipBody ON) carries ship-ship contact instead. Hull↔asteroid + hull↔projectile untouched.</item>
    /// </list>
    /// Primary metric = sustained contact duration (longest contiguous in-contact run + in-contact fraction, measured at
    /// the bumper-sphere radius); the fix must COLLAPSE it. B3 honesty bound: a frozen policy is SCREENING (does the same
    /// learned ramming still pin?), NOT proof ramming is balanced post-fix (stage-ii retrain). Merge gate is a human
    /// playtest, not this benchmark. Opt-in gated (RL_RAM_BENCH env / results/rl-ram-bench/watch.flag).</summary>
    [TestFixture]
    [Category("AI")]
    public class RamFrictionBenchmarkPlayModeTests
    {
        private const string CheckpointPath = "Assets/Tests/Fixtures/ShipCombat-selfplay2-999950.onnx";
        private const string ShipLayerName = "Ship";
        private const string ShipBodyLayerName = "ShipBody";
        private const float ContactEpsilon = 0.5f; // tight tolerance on the summed bumper radii — actual touch, not orbit
        private const int EpisodesPerCondition = 8;

        // Self-validation guard: the baseline (old hull-friction pin) must actually ram, or the premise is wrong (wrong
        // checkpoint / pin not reproducing) and there is nothing to measure. Deliberately low — any real sustained contact.
        private const float SelfValidationMinSustainedSec = 0.4f;

        // Pass margins pinned from the measured baseline-vs-layer-split numbers (see PR body): the layer-split must COLLAPSE
        // sustained ram-contact. Multiplicative ceiling on mean longest-contact plus an additive floor on the fraction drop.
        private const float SustainedCollapseFactor = 0.5f;
        private const float ContactFractionDropMin = 0.05f;

        private GameObject arenaHost;
        private UnitService unitService;
        private ArenaContext arena;
        private ProjectileService projectiles;
        private HarnessAssets assets;
        private float savedTimeScale;
        private float savedMaxDelta;
        private float savedCaptureDelta;
        private bool savedShipShipIgnore;

        private EpisodePair pair;
        private ShipAgent agentA;
        private ShipAgent agentB;
        private bool verified;
        private float benchContactRange;

        [Serializable]
        private struct EpisodeMetrics
        {
            public float longestContactSec;
            public float contactFraction;
            public float minRange;
            public float ttk;
            public bool mutualKill;
            public float agentHpLostPct;
            public float oppHpLostPct;
            public float meanTumbleInContact;
            public string outcome;
        }

        [Serializable]
        private struct ConditionAgg
        {
            public string label;
            public int episodes;
            public float meanLongestContactSec;
            public float maxLongestContactSec;
            public float meanContactFraction;
            public float meanMinRange;
            public float meanTtk;
            public float mutualKillRate;
            public float meanAgentHpLostPct;
            public float meanOppHpLostPct;
            public float meanTumbleInContact;
            public EpisodeMetrics[] perEpisode;
        }

        [Serializable]
        private struct RamBenchDelta
        {
            public string schema;
            public string checkpoint;
            public float contactRange;
            public ConditionAgg baseline;
            public ConditionAgg layerSplit;
            public float sustainedContactSplitOverBaseline;
            public const string SchemaId = "rl-ram-bench-layersplit-v1";
        }

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            arenaHost = new GameObject("[RamBenchArena]");
            unitService = arenaHost.AddComponent<UnitService>();
            arena = TestArena.On(arenaHost, unitService.ActiveRegistry);
            unitService.SetArena(arena);
            projectiles = new ProjectileService(arenaHost.transform);
            unitService.SetProjectiles(projectiles);
            assets = UnityEditor.AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            Assert.IsNotNull(assets, $"HarnessAssets missing at {HarnessAssets.AssetPath}");

            savedTimeScale = Time.timeScale;
            savedMaxDelta = Time.maximumDeltaTime;
            savedCaptureDelta = Time.captureDeltaTime;
            savedShipShipIgnore = Physics.GetIgnoreLayerCollision(ShipLayer, ShipLayer);
            Time.maximumDeltaTime = 1f;
            PacingContract.Apply();
            if (Academy.IsInitialized)
                Academy.Instance.AutomaticSteppingEnabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = savedTimeScale;
            Time.maximumDeltaTime = savedMaxDelta;
            Time.captureDeltaTime = savedCaptureDelta;
            Physics.IgnoreLayerCollision(ShipLayer, ShipLayer, savedShipShipIgnore);

            DisposeComposition();
            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            arena = null;
            projectiles = null;

            if (Academy.IsInitialized)
                Academy.Instance.AutomaticSteppingEnabled = true;
            AudioListener.pause = false;
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator MirrorSelfPlay_LayerSplit_CollapsesSustainedRamContact()
        {
            if (SkipUnlessOptedIn(out var reason)) { Assert.Ignore(reason); yield break; }

            AssertCollisionMatrix();
            var spec = MakeSpec();

            // Baseline (old hull-friction pin) first — this pass is also the self-validation guard.
            ConditionAgg baseline = default;
            yield return RunCondition("baseline", layerSplit: false, spec, a => baseline = a);
            LogCondition(baseline);
            Assert.Greater(baseline.maxLongestContactSec, SelfValidationMinSustainedSec,
                $"SELF-VALIDATION FAILED: mirror self-play of {CheckpointPath} at the old hull-friction pin did not " +
                $"reproduce sustained ram-contact (max {baseline.maxLongestContactSec:F2}s over {baseline.episodes} eps). " +
                "Wrong checkpoint or the pin does not reproduce — STOP and report, do not measure a non-ramming policy.");

            ConditionAgg split = default;
            yield return RunCondition("layer-split", layerSplit: true, spec, a => split = a);
            LogCondition(split);

            var delta = WriteDelta(in baseline, in split);
            Debug.Log($"[RamBench] COLLAPSE meanLongestContact baseline={baseline.meanLongestContactSec:F2}s " +
                      $"layer-split={split.meanLongestContactSec:F2}s (ratio={delta.sustainedContactSplitOverBaseline:F2}); " +
                      $"contactFraction baseline={baseline.meanContactFraction:P1} split={split.meanContactFraction:P1}");

            Assert.Less(split.meanLongestContactSec, baseline.meanLongestContactSec * SustainedCollapseFactor,
                $"layer-split must collapse sustained ram-contact: baseline={baseline.meanLongestContactSec:F2}s " +
                $"layer-split={split.meanLongestContactSec:F2}s (need < {SustainedCollapseFactor:P0} of baseline)");
            Assert.Less(split.meanContactFraction, baseline.meanContactFraction - ContactFractionDropMin,
                $"layer-split must reduce in-contact fraction: baseline={baseline.meanContactFraction:P1} " +
                $"layer-split={split.meanContactFraction:P1} (need a drop of ≥ {ContactFractionDropMin:P0})");
        }

        private IEnumerator RunCondition(string label, bool layerSplit, RewardSpec spec, Action<ConditionAgg> onDone)
        {
            pair = EpisodePair.SpawnSelfPlayPair(unitService, arena, projectiles, in spec, assets,
                out var chooserA, out var chooserB);
            if (!verified)
            {
                AssertShipComposition(pair.Agent);
                AssertShipComposition(pair.Baseline);
                benchContactRange = 2f * BumperWorldRadius(pair.Agent) + ContactEpsilon;
                verified = true;
            }
            (agentA, agentB) = ShipAgentFactory.ComposeSelfPlayInference(
                pair, chooserA, chooserB, in spec, arena.Offset, CheckpointPath);

            ApplyCondition(layerSplit);

            var driver = new EpisodeLoopDriver(pair, agentA, arena.Offset, opponentAgent: agentB);
            var episodes = new List<EpisodeMetrics>();
            for (var i = 0; i < EpisodesPerCondition; i++)
            {
                var sampler = new ContactSampler(pair, benchContactRange);
                yield return driver.RunEpisode(spec, i, onFixedStep: sampler.Sample);
                episodes.Add(sampler.ToMetrics(driver.Runner.Result));
            }

            onDone(Aggregate(label, episodes));
            DisposeComposition();
        }

        /// <summary>Old behavior (baseline): hull↔hull ship-ship ON at friction 0.6, bumper spheres off. Shipped fix
        /// (layer-split): hull↔hull ship-ship OFF (the committed matrix), frictionless bumper spheres carry contact.</summary>
        private void ApplyCondition(bool layerSplit)
        {
            Physics.IgnoreLayerCollision(ShipLayer, ShipLayer, layerSplit);
            SetBumpersEnabled(pair.Agent, layerSplit);
            SetBumpersEnabled(pair.Baseline, layerSplit);
        }

        private static void SetBumpersEnabled(Ship ship, bool enabled)
        {
            foreach (var sphere in ship.GetComponentsInChildren<SphereCollider>())
                sphere.enabled = enabled;
        }

        private static ConditionAgg Aggregate(string label, List<EpisodeMetrics> eps)
        {
            var agg = new ConditionAgg { label = label, episodes = eps.Count, perEpisode = eps.ToArray() };
            foreach (var e in eps)
            {
                agg.meanLongestContactSec += e.longestContactSec;
                agg.maxLongestContactSec = Mathf.Max(agg.maxLongestContactSec, e.longestContactSec);
                agg.meanContactFraction += e.contactFraction;
                agg.meanMinRange += e.minRange;
                agg.meanTtk += e.ttk;
                if (e.mutualKill) agg.mutualKillRate += 1f;
                agg.meanAgentHpLostPct += e.agentHpLostPct;
                agg.meanOppHpLostPct += e.oppHpLostPct;
                agg.meanTumbleInContact += e.meanTumbleInContact;
            }
            if (eps.Count > 0)
            {
                var n = eps.Count;
                agg.meanLongestContactSec /= n;
                agg.meanContactFraction /= n;
                agg.meanMinRange /= n;
                agg.meanTtk /= n;
                agg.mutualKillRate /= n;
                agg.meanAgentHpLostPct /= n;
                agg.meanOppHpLostPct /= n;
                agg.meanTumbleInContact /= n;
            }
            return agg;
        }

        private static void LogCondition(ConditionAgg a) => Debug.Log(
            $"[RamBench] {a.label} eps={a.episodes} meanLongestContact={a.meanLongestContactSec:F2}s " +
            $"maxLongestContact={a.maxLongestContactSec:F2}s contactFraction={a.meanContactFraction:P1} " +
            $"minRange={a.meanMinRange:F2} " +
            $"meanTTK={a.meanTtk:F1}s mutualKillRate={a.mutualKillRate:P0} tumbleInContact={a.meanTumbleInContact:F1}deg/s " +
            $"agentHPlost={a.meanAgentHpLostPct:P0} oppHPlost={a.meanOppHpLostPct:P0}");

        private static bool SkipUnlessOptedIn(out string reason)
        {
            var watchFlag = File.Exists(Path.Combine(ResultsDir(), "watch.flag"));
            if (watchFlag || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RL_RAM_BENCH")))
            {
                reason = null;
                return false;
            }
            reason = "Set RL_RAM_BENCH=1 (or create results/rl-ram-bench/watch.flag) to run the ram-pin mirror benchmark.";
            return true;
        }

        private static RewardSpec MakeSpec()
        {
            var spec = RewardSpec.Default;
            spec.useAsteroidField = false; // empty arena isolates the ship-ship contact mechanic
            spec.timeoutDecisions = 150;   // bounds a ranged stalemate; a ram resolves well inside this
            return spec;
        }

        private static int ShipLayer => LayerMask.NameToLayer(ShipLayerName);
        private static int ShipBodyLayer => LayerMask.NameToLayer(ShipBodyLayerName);

        /// <summary>The committed collision matrix is the fix — verify it loaded as intended: hull ship-ship off, bumper
        /// ship-ship on, bumper isolated from every other layer, and the hull's asteroid/projectile cells untouched.</summary>
        private static void AssertCollisionMatrix()
        {
            Assert.AreNotEqual(-1, ShipBodyLayer, "ShipBody layer missing — TagManager not updated");
            var ship = ShipLayer;
            var body = ShipBodyLayer;
            var asteroid = LayerMask.NameToLayer("Asteroid");
            var projectile = LayerMask.NameToLayer("Projectile");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(ship, ship), "Ship×Ship hull collision must be OFF");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(body, body), "ShipBody×ShipBody bumper collision must be ON");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(body, ship), "ShipBody×Ship must be OFF");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(body, asteroid), "ShipBody×Asteroid must be OFF (threading via hull)");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(body, projectile), "ShipBody×Projectile must be OFF (no double-hit)");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(ship, asteroid), "Ship×Asteroid must stay ON (hull threading)");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(ship, projectile), "Ship×Projectile must stay ON (laser hits hull)");
        }

        /// <summary>Each spawned ship keeps its convex BouncyShip hull (layer Ship) as Colliders[0] and carries a
        /// non-trigger frictionless ShipBody bumper sphere on the ShipBody layer.</summary>
        private static void AssertShipComposition(Ship ship)
        {
            Assert.Greater(ship.Colliders.Length, 1, $"{ship.name} must carry both hull and bumper colliders");
            var hull = ship.Colliders[0];
            Assert.IsInstanceOf<MeshCollider>(hull, $"{ship.name} Colliders[0] must stay the convex hull");
            StringAssert.Contains("BouncyShip", hull.sharedMaterial.name, $"{ship.name} hull lost the BouncyShip material");

            var bumper = ship.GetComponentInChildren<SphereCollider>();
            Assert.IsNotNull(bumper, $"{ship.name} has no ShipBody bumper sphere");
            Assert.AreEqual(ShipBodyLayer, bumper.gameObject.layer, "bumper must be on the ShipBody layer");
            Assert.IsFalse(bumper.isTrigger, "bumper must be a solid (non-trigger) collider");
            Assert.AreEqual(0f, bumper.sharedMaterial.dynamicFriction, "bumper must be frictionless");
        }

        private static float BumperWorldRadius(Ship ship)
        {
            var bumper = ship.GetComponentInChildren<SphereCollider>();
            var s = bumper.transform.lossyScale;
            return bumper.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }

        private RamBenchDelta WriteDelta(in ConditionAgg baseline, in ConditionAgg split)
        {
            var delta = new RamBenchDelta
            {
                schema = RamBenchDelta.SchemaId,
                checkpoint = CheckpointPath,
                contactRange = benchContactRange,
                baseline = baseline,
                layerSplit = split,
                sustainedContactSplitOverBaseline = baseline.meanLongestContactSec > 1e-4f
                    ? split.meanLongestContactSec / baseline.meanLongestContactSec : 0f,
            };
            var dir = ResultsDir();
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(dir, $"{stamp}-ram-bench-layersplit.json");
            File.WriteAllText(path, JsonUtility.ToJson(delta, true));
            Debug.Log($"[RamBench] wrote layer-split delta to {path}");
            return delta;
        }

        private void DisposeComposition()
        {
            projectiles?.ReturnAllToPool();
            if (agentA) UnityEngine.Object.DestroyImmediate(agentA.gameObject);
            if (agentB) UnityEngine.Object.DestroyImmediate(agentB.gameObject);
            agentA = null;
            agentB = null;
            pair?.Dispose();
            pair = null;
        }

        private static string ResultsDir() => Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "..", "..", "results", "rl-ram-bench"));

        /// <summary>Per-fixed-step ship-ship contact sampler: longest contiguous in-contact run (sustained contact),
        /// overall in-contact fraction, and relative angular velocity (tumble) while touching. Contact = centers within
        /// the summed bumper-sphere radii + epsilon, a geometric threshold identical across both conditions.</summary>
        private sealed class ContactSampler
        {
            private readonly Ship a;
            private readonly Ship b;
            private readonly float contactRange;
            private int totalSteps;
            private int contactSteps;
            private int longestRun;
            private int currentRun;
            private float tumbleSumInContact;
            private float minRange = float.MaxValue;

            public ContactSampler(EpisodePair pair, float contactRange)
            {
                a = pair.Agent;
                b = pair.Baseline;
                this.contactRange = contactRange;
            }

            public void Sample()
            {
                totalSteps++;
                var range = (a.Kinematics.pos - b.Kinematics.pos).magnitude;
                if (range < minRange) minRange = range;
                if (range <= contactRange)
                {
                    contactSteps++;
                    currentRun++;
                    if (currentRun > longestRun) longestRun = currentRun;
                    tumbleSumInContact += Mathf.Abs(a.Kinematics.yawRate - b.Kinematics.yawRate);
                }
                else
                {
                    currentRun = 0;
                }
            }

            public EpisodeMetrics ToMetrics(EpisodeResult r) => new()
            {
                longestContactSec = longestRun * Time.fixedDeltaTime,
                contactFraction = totalSteps > 0 ? (float)contactSteps / totalSteps : 0f,
                minRange = minRange == float.MaxValue ? 0f : minRange,
                ttk = r.simSeconds,
                mutualKill = r.mutualKill,
                agentHpLostPct = r.startMyPool > 0f ? (r.startMyPool - r.endMyPool) / r.startMyPool : 0f,
                oppHpLostPct = r.startEnemyPool > 0f ? (r.startEnemyPool - r.endEnemyPool) / r.startEnemyPool : 0f,
                meanTumbleInContact = contactSteps > 0 ? tumbleSumInContact / contactSteps : 0f,
                outcome = r.outcome,
            };
        }
    }
}
#endif
