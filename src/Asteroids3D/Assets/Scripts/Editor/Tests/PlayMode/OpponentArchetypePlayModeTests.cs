#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.RLHarness;
using Game.Services;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>The per-archetype degeneracy gate (env PR-C): each scripted opponent archetype exercised against the deterministic <see cref="RangerChooser"/> stand-in on the agent side, with per-episode JSONL rows + per-archetype summaries for the human go/no-go before any training hours. The sweep is opt-in (RL_ARCHETYPES env / results/rl-archetypes/watch.flag); the smoke always runs.</summary>
    [TestFixture]
    [Category("AI")]
    public class OpponentArchetypePlayModeTests
    {
        private const float RangerHoldRange = 15f;

        private static readonly OpponentArchetype[] Archetypes =
        {
            OpponentArchetype.Aggressor,
            OpponentArchetype.Evader,
            OpponentArchetype.Orbiter,
            OpponentArchetype.Kiter,
            OpponentArchetype.Dummy,
        };

        private GameObject arenaHost;
        private UnitService unitService;
        private ArenaContext arena;
        private ProjectileService projectiles;
        private HarnessAssets assets;
        private float savedTimeScale;
        private float savedMaxDelta;
        private float savedCaptureDelta;

        private EpisodePair pair;
        private OpponentRoster roster;

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            arenaHost = new GameObject("[ArchetypeArena]");
            unitService = arenaHost.AddComponent<UnitService>();
            arena = TestArena.On(arenaHost, unitService.ActiveRegistry);
            unitService.SetArena(arena);
            projectiles = new ProjectileService(arenaHost.transform);
            assets = UnityEditor.AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            Assert.IsNotNull(assets, $"HarnessAssets missing at {HarnessAssets.AssetPath}");
            unitService.SetProjectiles(projectiles);

            savedTimeScale = Time.timeScale;
            savedMaxDelta = Time.maximumDeltaTime;
            savedCaptureDelta = Time.captureDeltaTime;
            Time.timeScale = 20f;
            Time.maximumDeltaTime = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = savedTimeScale;
            Time.maximumDeltaTime = savedMaxDelta;
            Time.captureDeltaTime = savedCaptureDelta;

            projectiles?.ReturnAllToPool();
            roster?.Dispose();
            roster = null;
            pair?.Dispose();
            pair = null;

            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            arena = null;
            projectiles = null;

            AudioListener.pause = false;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Smoke_EachArchetype_ProducesIntentsAndTerminates()
        {
            var spec = RewardSpec.Default;
            spec.timeoutDecisions = 60;
            spec.minSeparation = 18f;
            spec.maxSeparation = 24f;

            SpawnPairWithRoster(in spec);

            foreach (var archetype in Archetypes)
            {
                var draw = roster.Install(archetype, in spec, 0, arena.Offset);
                pair.Reset(in spec, 0);
                using var probe = new ArchetypeGateSampler(pair.Baseline, pair.Agent, arena.Offset,
                    spec.arenaRadius, in draw);
                var runner = new EpisodeRunner(pair.Agent, pair.Baseline, spec, 0, arena.Offset);
                runner.RecordOpponent(in draw);
                yield return RunToCompletion(runner, spec, probe);

                var row = probe.ToRow(runner.Result);
                Assert.AreEqual(archetype.ToString(), row.opponent.archetype);
                Assert.AreNotEqual(EpisodeOutcome.Unresolved.ToString(), row.outcome,
                    $"{archetype} episode did not terminate legally");
                Assert.AreNotEqual(EndKind.None.ToString(), row.endKind);

                switch (archetype)
                {
                    case OpponentArchetype.Dummy:
                        Assert.AreEqual(0, row.shotsFired, "Dummy must never fire");
                        Assert.Less(row.maxDisplacement, 10f, "Dummy must hold station");
                        break;
                    case OpponentArchetype.Evader:
                        Assert.AreEqual(0, row.shotsFired, "Evader must never fire");
                        Assert.Greater(row.maxDisplacement, 5f, "Evader must actually flee");
                        break;
                    default:
                        Assert.Greater(row.maxDisplacement, 2f,
                            $"{archetype} produced no motion — chooser install/wiring broken?");
                        break;
                }

                var gateRoundTrip = JsonUtility.FromJson<ArchetypeGateRow>(row.ToJsonLine());
                Assert.AreEqual(row.outcome, gateRoundTrip.outcome);
                Assert.AreEqual(row.opponent.archetype, gateRoundTrip.opponent.archetype);

                // The archetype draw rides the standard episode row additively.
                var episodeRoundTrip = JsonUtility.FromJson<EpisodeResult>(runner.Result.ToJsonLine());
                Assert.AreEqual(EpisodeResult.SchemaId, episodeRoundTrip.schema);
                Assert.AreEqual(archetype.ToString(), episodeRoundTrip.opponent.archetype);
            }
        }

        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator Sweep_WritesJsonl()
        {
            var resultsDir = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "results", "rl-archetypes"));
            var watchFlag = File.Exists(Path.Combine(resultsDir, "watch.flag"));
            if (!watchFlag && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RL_ARCHETYPES")))
                Assert.Ignore("Set RL_ARCHETYPES=1 (or create results/rl-archetypes/watch.flag) to run the archetype degeneracy sweep.");

            // Watch (human real-time eyeball) is the one unlocked mode; measurement runs keep the pacing contract.
            if (watchFlag) Time.timeScale = 1f;
            else PacingContract.Apply();

            var episodesPerArchetype = watchFlag ? 1 : 10;

            var spec = RewardSpec.Default;
            spec.timeoutDecisions = 300;

            SpawnPairWithRoster(in spec);

            var path = EpisodeJsonl.NewRunPath("archetype-gate", "rl-archetypes");
            var episodes = 0;
            foreach (var archetype in Archetypes)
            {
                var rows = new List<ArchetypeGateRow>();
                for (var i = 0; i < episodesPerArchetype; i++)
                {
                    var draw = roster.Install(archetype, in spec, i, arena.Offset);
                    pair.Reset(in spec, i);
                    using var probe = new ArchetypeGateSampler(pair.Baseline, pair.Agent, arena.Offset,
                        spec.arenaRadius, in draw);
                    var runner = new EpisodeRunner(pair.Agent, pair.Baseline, spec, i, arena.Offset);
                    yield return RunToCompletion(runner, spec, probe);
                    var row = probe.ToRow(runner.Result);
                    rows.Add(row);
                    File.AppendAllText(path, row.ToJsonLine() + "\n");
                    episodes++;
                }
                File.AppendAllText(path, ArchetypeGateSummary.Summarize(archetype.ToString(), rows).ToJsonLine() + "\n");
            }

            Debug.Log($"[ArchetypeGate] wrote {episodes} episodes (+{Archetypes.Length} summaries) to {path}");
        }

        /// <summary>The gate composition: the canonical pair with the deterministic ranger stand-in on the agent side, and the roster bound to the opponent while its prefab-default utility chooser is still installed.</summary>
        private void SpawnPairWithRoster(in RewardSpec spec)
        {
            pair = EpisodePair.Spawn(unitService, arena, projectiles, in spec, (agentShip, baselineShip) =>
            {
                var ranger = new RangerChooser();
                ranger.Configure(baselineShip, RangerHoldRange);
                return ranger;
            }, assets);
            roster = new OpponentRoster(pair.Baseline, pair.Agent);
        }

        private IEnumerator RunToCompletion(EpisodeRunner runner, RewardSpec spec, ArchetypeGateSampler probe)
        {
            runner.Begin();
            var maxSimSeconds = spec.timeoutDecisions * spec.decisionIntervalSteps * Time.fixedDeltaTime;
            var deadline = Time.realtimeSinceStartup + 120f + maxSimSeconds * 2f;
            while (!runner.IsDone && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
                runner.Tick();
                probe.Sample();
            }
            Assert.IsTrue(runner.IsDone, "Episode wall-clock deadline exceeded before termination");
        }
    }
}
#endif
