#if UNITY_EDITOR
using System.Collections;
using Game;
using Game.RLHarness;
using Game.Services;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.Common;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>Python-free proof of the whole ML-Agents loop: manual Academy stepping through the EpisodeLoopDriver with the Heuristic policy, headless. The agent's accumulated reward must equal the runner's — one reward channel, no drift.</summary>
    [TestFixture]
    [Category("AI")]
    public class RLAgentPlayModeTests
    {
        private GameObject arenaHost;
        private UnitService unitService;
        private ArenaContext arena;
        private float savedTimeScale;
        private float savedMaxDelta;
        private float savedCaptureDelta;

        private EpisodePair pair;
        private AgentChooser chooser;
        private ShipAgent agent;

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            arenaHost = new GameObject("[AgentArena]");
            unitService = arenaHost.AddComponent<UnitService>();
            arena = TestArena.On(arenaHost, unitService.ActiveRegistry);
            unitService.SetArena(arena);
            ProjectileFlush.ReturnAllToPool();
            foreach (var ship in UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(ship.gameObject);

            savedTimeScale = Time.timeScale;
            savedMaxDelta = Time.maximumDeltaTime;
            savedCaptureDelta = Time.captureDeltaTime;
            Time.maximumDeltaTime = 1f;
            PacingContract.Apply();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = savedTimeScale;
            Time.maximumDeltaTime = savedMaxDelta;
            Time.captureDeltaTime = savedCaptureDelta;

            ProjectileFlush.ReturnAllToPool();
            if (agent) UnityEngine.Object.DestroyImmediate(agent.gameObject);
            agent = null;
            pair?.Dispose();
            pair = null;
            chooser = null;
            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            arena = null;

            if (Academy.IsInitialized)
                Academy.Instance.AutomaticSteppingEnabled = true;
            AudioListener.pause = false;
        }

        private void Compose(in RewardSpec spec)
        {
            pair = EpisodePair.Spawn(unitService, arena, in spec, (agentShip, baselineShip) =>
            {
                chooser = new AgentChooser();
                chooser.Configure(baselineShip,
                    agentShip.Weapons.Context.ProjectileSpeed(WeaponSlot.Primary));
                return chooser;
            });
            agent = ShipAgentFactory.ComposeHeuristicOnly(pair, chooser, in spec, arena.Offset);
            Assert.IsNotNull(agent, "ShipAgent must be attachable (harness assembly is not editor-only)");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator HeuristicOnly_FullEpisodes_AgentRewardMatchesRunner()
        {
            var spec = RewardSpec.Default;
            spec.timeoutDecisions = 150;
            spec.minSeparation = 18f;
            spec.maxSeparation = 24f;

            Compose(in spec);
            var driver = new EpisodeLoopDriver(pair, agent, arena.Offset);

            for (var i = 0; i < 2; i++)
            {
                yield return driver.RunEpisode(spec, i);
                var result = driver.Runner.Result;

                Assert.IsTrue(driver.Runner.IsDone, $"episode {i} did not finish");
                Assert.AreNotEqual(EpisodeOutcome.Unresolved.ToString(), result.outcome);
                Assert.AreNotEqual(EndKind.None.ToString(), result.endKind);
                Assert.Greater(result.decisions, 0);
                Assert.AreEqual(result.decisions, agent.DecisionsReceived,
                    "one primed decision + one per non-terminal boundary must equal the paid decision count");
                Assert.AreEqual(result.totalReward, driver.LastEpisodeCumulativeReward, 1e-3f,
                    "the agent must accumulate exactly the runner's reward");
            }
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator InferenceOnly_PinnedCheckpoint_DrivesAFullEpisode()
        {
            const string onnxFixturePath = "Assets/Tests/Fixtures/ShipCombat-smoke.onnx";
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(onnxFixturePath) == null)
                Assert.Ignore($"ONNX fixture pending — produce via the trainer smoke (training/rl/README.md) and commit it at {onnxFixturePath}");

            var spec = RewardSpec.Default;
            spec.timeoutDecisions = 40;
            spec.minSeparation = 18f;
            spec.maxSeparation = 24f;

            pair = EpisodePair.Spawn(unitService, arena, in spec, (agentShip, baselineShip) =>
            {
                chooser = new AgentChooser();
                chooser.Configure(baselineShip,
                    agentShip.Weapons.Context.ProjectileSpeed(WeaponSlot.Primary));
                return chooser;
            });
            agent = ShipAgentFactory.ComposeInferenceOnly(pair, chooser, in spec, arena.Offset, onnxFixturePath);
            var driver = new EpisodeLoopDriver(pair, agent, arena.Offset);

            yield return driver.RunEpisode(spec, 0);
            var result = driver.Runner.Result;
            Assert.AreNotEqual(EpisodeOutcome.Unresolved.ToString(), result.outcome);
            Assert.AreEqual(result.decisions, agent.DecisionsReceived);
            Assert.AreEqual(result.totalReward, driver.LastEpisodeCumulativeReward, 1e-3f);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator HeuristicOnly_Timeout_TruncatesAndSurvivesIntoTheNextEpisode()
        {
            var spec = RewardSpec.Default;
            spec.timeoutDecisions = 10;
            spec.minSeparation = 50f;
            spec.maxSeparation = 60f;

            Compose(in spec);
            var driver = new EpisodeLoopDriver(pair, agent, arena.Offset);

            yield return driver.RunEpisode(spec, 0);
            Assert.AreEqual(EndKind.Truncation.ToString(), driver.Runner.Result.endKind);
            Assert.IsTrue(driver.Runner.Result.timedOut);

            yield return driver.RunEpisode(spec, 1);
            Assert.IsTrue(driver.Runner.IsDone, "EpisodeInterrupted must leave the agent usable");
            Assert.AreEqual(driver.Runner.Result.decisions, agent.DecisionsReceived);
        }
    }
}
#endif
