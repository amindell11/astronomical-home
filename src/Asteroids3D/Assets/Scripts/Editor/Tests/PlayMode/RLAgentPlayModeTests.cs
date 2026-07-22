#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>Python-free proof of the whole ML-Agents loop: the Academy auto-steps while the EpisodeLoopDriver paces the Heuristic policy, headless. The agent's accumulated reward must equal the runner's — one reward channel, no drift.</summary>
    [TestFixture]
    [Category("AI")]
    public class RLAgentPlayModeTests
    {
        private GameObject arenaHost;
        private UnitService unitService;
        private ArenaContext arena;
        private ProjectileService projectiles;
        private HarnessAssets assets;
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
            projectiles = new ProjectileService(arenaHost.transform);
            assets = UnityEditor.AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            Assert.IsNotNull(assets, $"HarnessAssets missing at {HarnessAssets.AssetPath}");
            unitService.SetProjectiles(projectiles);
            AssertNoForeignDebris();

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
            if (agent) UnityEngine.Object.DestroyImmediate(agent.gameObject);
            agent = null;
            pair?.Dispose();
            pair = null;
            chooser = null;
            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            arena = null;
            projectiles = null;

            if (Academy.IsInitialized)
                Academy.Instance.AutomaticSteppingEnabled = true;
            AudioListener.pause = false;
        }

        // Registration is mandatory and transients die with their fixture root — foreign debris means a leaking fixture to FIX, so assert, never sweep.
        private static void AssertNoForeignDebris()
        {
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<Combat.Projectile.ProjectileBase>(FindObjectsSortMode.None).Length,
                "A previous fixture leaked live projectiles — its transients escaped their registry/root");
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None).Length,
                "A previous fixture leaked ships — fix its teardown");
        }

        private void Compose(in RewardSpec spec)
        {
            pair = EpisodePair.SpawnWithAgentChooser(unitService, arena, projectiles, in spec, assets, out chooser);
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
        public IEnumerator AutoStep_PrimedBoundaryAction_MovesAgentExactlyOneTickLater()
        {
            // The permanent zero-latency guard (PR-0 finding). Under auto-stepping the primed boundary action must
            // move the agent on the FIRST fixed step (t+1) — the AICommander-after-the-stepper ordering. A one-tick
            // regression (AICommander back before the stepper) delays first motion to step 2. Integer step index, not
            // a float golden: the stepping move changes WHEN the step fires, never the action values.
            var spec = RewardSpec.Default;
            spec.timeoutDecisions = 10;
            spec.minSeparation = 18f;
            spec.maxSeparation = 24f;

            Compose(in spec);
            var driver = new EpisodeLoopDriver(pair, agent, arena.Offset);

            var speeds = new List<float>();
            var academyBefore = Academy.Instance.TotalStepCount;
            yield return driver.RunEpisode(spec, 0,
                onFixedStep: () => speeds.Add(pair.Agent.Kinematics.vel.magnitude));
            var academyStepped = Academy.Instance.TotalStepCount - academyBefore;

            // Instrument validity: the Academy must actually have auto-stepped, else the motion timing is meaningless.
            Assert.Greater(academyStepped, speeds.Count / 2,
                "Academy did not auto-step — AutomaticSteppingEnabled never engaged, so latency cannot be measured");

            Assert.Less(speeds[0], 1e-3f, "the agent must be at rest before the primed action lands");
            var firstMotion = speeds.FindIndex(v => v > 1e-3f);
            Assert.AreEqual(1, firstMotion,
                $"the boundary action must first move the agent at t+1 (step 1); first motion at step {firstMotion} is a decision-latency regression");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator InferenceOnly_PinnedCheckpoint_DrivesAFullEpisode()
        {
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(ShipAgentFactory.SmokeFixturePath) == null)
                Assert.Fail($"ONNX fixture missing at {ShipAgentFactory.SmokeFixturePath} — a binding merge gate; produce via the trainer smoke (training/rl/README.md) and commit it (LFS)");

            var spec = RewardSpec.Default;
            spec.timeoutDecisions = 40;
            spec.minSeparation = 18f;
            spec.maxSeparation = 24f;

            pair = EpisodePair.SpawnWithAgentChooser(unitService, arena, projectiles, in spec, assets, out chooser);
            agent = ShipAgentFactory.ComposeInferenceOnly(pair, chooser, in spec, arena.Offset,
                ShipAgentFactory.SmokeFixturePath);

            var behavior = agent.GetComponent<BehaviorParameters>();
            Assert.AreEqual(BehaviorType.InferenceOnly, behavior.BehaviorType);
            Assert.IsTrue(behavior.DeterministicInference,
                "InferenceOnly alone samples stochastically — eval must pin DeterministicInference");
            Assert.AreEqual(InferenceDevice.Burst, behavior.InferenceDevice,
                "eval must pin the inference device (Default may change between releases)");

            var driver = new EpisodeLoopDriver(pair, agent, arena.Offset);

            yield return driver.RunEpisode(spec, 0);
            var result = driver.Runner.Result;
            Assert.AreNotEqual(EpisodeOutcome.Unresolved.ToString(), result.outcome);
            Assert.AreEqual(result.decisions, agent.DecisionsReceived);
            Assert.AreEqual(result.totalReward, driver.LastEpisodeCumulativeReward, 1e-3f);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CheckpointEvaluator_SmallRun_AggregatesOutcomesAndWritesArtifacts()
        {
            var spec = RewardSpec.Default;
            spec.timeoutDecisions = 8;
            spec.minSeparation = 50f;
            spec.maxSeparation = 60f;

            var seeds = new[] { EvalProtocol.HeldOutSeeds[0], EvalProtocol.HeldOutSeeds[1] };
            CheckpointEvaluator.Summary summary = default;
            yield return CheckpointEvaluator.Run(unitService, arena, projectiles, assets, ShipAgentFactory.SmokeFixturePath,
                seeds, episodesPerSeed: 1, spec, "test-eval", s => summary = s);

            // Stratified eval: one standalone block per archetype, and no blended aggregate anywhere.
            CollectionAssert.AreEquivalent(
                new[] { "Aggressor", "Evader", "Orbiter", "Kiter", "Dummy" },
                System.Array.ConvertAll(summary.archetypes, a => a.archetype));
            foreach (var block in summary.archetypes)
            {
                Assert.AreEqual(seeds.Length, block.episodes,
                    $"{block.archetype}: episodesPerSeed × seeds episodes per archetype block");
                Assert.AreEqual(block.episodes, block.wins + block.losses + block.draws,
                    "every episode must land in exactly one W/L/D bucket");
                Assert.AreEqual(block.wins / (float)block.episodes, block.winRate, 1e-6f,
                    "draws must count as non-wins");
                Assert.AreEqual(EvalProtocol.WilsonLowerBound(block.wins, block.episodes),
                    block.wilsonLowerBound95, 1e-6f);
            }
            Assert.AreEqual(seeds, summary.seeds);
            Assert.IsTrue(System.IO.File.Exists(summary.episodesJsonl), "per-episode JSONL artifact missing");
            Assert.IsTrue(System.IO.File.Exists(summary.episodesJsonl.Replace(".jsonl", "-summary.json")),
                "summary artifact missing");

            // The teacher scorecard rides alongside the W/L/D tally, one behavior block per archetype in the same order.
            CollectionAssert.AreEqual(
                System.Array.ConvertAll(summary.archetypes, a => a.archetype),
                System.Array.ConvertAll(summary.behavior, b => b.archetype));
            foreach (var b in summary.behavior)
            {
                Assert.AreEqual(seeds.Length, b.episodes, $"{b.archetype}: one behavior row per episode");
                Assert.AreEqual(b.episodes, b.survived + b.died, "every episode: opponent survived xor died");
            }
            Assert.IsTrue(System.IO.File.Exists(summary.behaviorJsonl), "per-episode behavior JSONL artifact missing");
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
