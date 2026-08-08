#if UNITY_EDITOR
using System.Collections;
using Game;
using Game.RLHarness;
using Game.Services;
using NUnit.Framework;
using Ships;
using Tests.Common;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>Python-free proof of the symmetric self-play loop: both ships are ShipCombat agents (team 0/1) requesting on the shared boundary clock, headless, while the Academy auto-steps. Each agent must accumulate exactly its own mirror runner's reward — two independent reward channels off one clock.</summary>
    [TestFixture]
    [Category("AI")]
    public class RLSelfPlayPlayModeTests
    {
        private GameObject arenaHost;
        private UnitService unitService;
        private ArenaContext arena;
        private ProjectileService projectiles;
        private float savedTimeScale;
        private float savedMaxDelta;
        private float savedCaptureDelta;

        private EpisodePair pair;
        private ShipAgent agentA;
        private ShipAgent agentB;
        private HarnessAssets assets;

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            arenaHost = new GameObject("[SelfPlayArena]");
            unitService = arenaHost.AddComponent<UnitService>();
            arena = TestArena.On(arenaHost, unitService.ActiveRegistry);
            unitService.SetArena(arena);
            projectiles = new ProjectileService(arenaHost.transform);
            unitService.SetProjectiles(projectiles);
            assets = UnityEditor.AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            Assert.IsNotNull(assets, $"HarnessAssets missing at {HarnessAssets.AssetPath}");
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None).Length,
                "A previous fixture leaked ships — fix its teardown");

            savedTimeScale = Time.timeScale;
            savedMaxDelta = Time.maximumDeltaTime;
            savedCaptureDelta = Time.captureDeltaTime;
            Time.maximumDeltaTime = 1f;
            PacingContract.Apply();

            // An InferenceChooser test earlier in the suite leaves auto-stepping off.
            if (Academy.IsInitialized)
                Academy.Instance.AutomaticSteppingEnabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = savedTimeScale;
            Time.maximumDeltaTime = savedMaxDelta;
            Time.captureDeltaTime = savedCaptureDelta;

            projectiles?.ReturnAllToPool();
            if (agentA) UnityEngine.Object.DestroyImmediate(agentA.gameObject);
            if (agentB) UnityEngine.Object.DestroyImmediate(agentB.gameObject);
            agentA = null;
            agentB = null;
            pair?.Dispose();
            pair = null;
            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            arena = null;
            projectiles = null;

            if (Academy.IsInitialized)
                Academy.Instance.AutomaticSteppingEnabled = true;
            AudioListener.pause = false;
        }

        private EpisodeLoopDriver Compose(in RewardSpec spec)
        {
            pair = EpisodePair.SpawnSelfPlayPair(unitService, arena, projectiles, in spec, assets, out var chooserA, out var chooserB);
            (agentA, agentB) = ShipAgentFactory.ComposeSelfPlayPair(
                pair, chooserA, chooserB, in spec, arena.Offset, BehaviorType.HeuristicOnly);
            return new EpisodeLoopDriver(pair, agentA, arena.Offset, opponentAgent: agentB);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator SelfPlay_FullEpisodes_BothAgentsRewardedFromTheirOwnRunner()
        {
            var spec = RewardSpec.Default;
            spec.timeoutDecisions = 150;
            spec.minSeparation = 18f;
            spec.maxSeparation = 24f;

            var driver = Compose(in spec);

            // Parameter sharing: both ships run one behavior across two teams so the trainer's self_play engages.
            var a = agentA.GetComponent<BehaviorParameters>();
            var b = agentB.GetComponent<BehaviorParameters>();
            Assert.AreEqual(ShipAgentFactory.BehaviorName, a.BehaviorName);
            Assert.AreEqual(ShipAgentFactory.BehaviorName, b.BehaviorName);
            Assert.AreEqual(0, a.TeamId);
            Assert.AreEqual(1, b.TeamId, "the mirror ship must be team 1 so self_play engages");

            for (var i = 0; i < 2; i++)
            {
                yield return driver.RunEpisode(spec, i);

                Assert.IsTrue(driver.Runner.IsDone, $"episode {i} did not finish");
                var result = driver.Runner.Result;
                Assert.AreNotEqual(EndKind.None.ToString(), result.endKind);
                Assert.Greater(result.decisions, 0);

                // One clock: both agents pay the same number of decisions from the shared boundary counter.
                Assert.AreEqual(result.decisions, agentA.DecisionsReceived,
                    "team 0 decisions must equal the primary runner's paid count");
                Assert.AreEqual(result.decisions, agentB.DecisionsReceived,
                    "team 1 steps in lockstep on the shared auto-step clock");
                Assert.AreEqual(result.decisions, driver.OpponentRunner.Result.decisions,
                    "both perspectives share the termination clock");

                // Two independent reward channels: each agent accumulates exactly its own runner's total.
                Assert.AreEqual(result.totalReward, driver.LastEpisodeCumulativeReward, 1e-3f);
                Assert.AreEqual(driver.OpponentRunner.Result.totalReward,
                    driver.LastOpponentEpisodeCumulativeReward, 1e-3f,
                    "the team-1 agent must accumulate its own mirror-runner reward");
            }
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator SelfPlay_TransitionArtifact_RecordsBothTeamPerspectives()
        {
            var spec = RewardSpec.Default;
            spec.timeoutDecisions = 2;
            spec.minSeparation = 50f;
            spec.maxSeparation = 60f;

            var driver = Compose(in spec);
            var dir = System.IO.Path.Combine(Application.temporaryCachePath,
                $"rl-selfplay-transition-test-{System.Guid.NewGuid():N}");
            DecisionTransitionJsonl output = null;
            try
            {
                output = DecisionTransitionJsonl.Create(
                    "selfplay-transition-test", workerIndex: 1, arenaIndex: 0, dir, "-w1");
                var path = output.Path;
                yield return driver.RunEpisode(spec, episodeIndex: 4, transitionOutput: output);
                output.Dispose();
                output = null;

                var lines = System.IO.File.ReadAllLines(path);
                Assert.AreEqual(driver.Runner.Result.decisions * 2, lines.Length,
                    "each shared boundary must emit one row from each team perspective");
                for (var i = 0; i < lines.Length; i++)
                {
                    var row = JsonUtility.FromJson<DecisionTransition>(lines[i]);
                    row.Validate();
                    Assert.AreEqual(i % 2, row.teamId,
                        "the shared output must carry team 0 then team 1 at each boundary");
                    Assert.AreEqual(i / 2 + 1, row.decision);
                }
            }
            finally
            {
                output?.Dispose();
                if (System.IO.Directory.Exists(dir))
                    System.IO.Directory.Delete(dir, recursive: true);
            }
        }
    }
}
#endif
