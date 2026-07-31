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
            var sessionSpec = new SessionSpec
            {
                onnxAssetPath = ShipAgentFactory.SmokeFixturePath,
                seeds = seeds,
                tag = "test-eval",
                episodesPerSeed = 1,
                probes = new[]
                {
                    ProbeSpec.Named(ArchetypeGateProbe.ProbeName),
                    ProbeSpec.Named(CombatTelemetryProbe.ProbeName),
                    ProbeSpec.Named(ContactProbe.ProbeName),
                    ProbeSpec.Named(FacingProbe.ProbeName),
                },
            };
            CheckpointEvaluator.Summary summary = default;
            yield return CheckpointEvaluator.Run(NewHost(sessionSpec), sessionSpec, spec, s => summary = s);

            Assert.AreEqual(CheckpointEvaluator.SchemaId, summary.schema);
            // Stratified eval: one standalone block per opponent, and no blended aggregate anywhere.
            CollectionAssert.AreEquivalent(
                new[] { "Aggressor", "Evader", "Orbiter", "Kiter", "Dummy" },
                System.Array.ConvertAll(summary.opponents, o => o.opponent));
            foreach (var block in summary.opponents)
            {
                Assert.AreEqual(seeds.Length, block.episodes,
                    $"{block.opponent}: episodesPerSeed × seeds episodes per opponent block");
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

            // Each selected probe rides as its own sidecar pair the summary points at.
            Assert.AreEqual(4, summary.probes.Length);
            Assert.AreEqual(
                new[]
                {
                    ArchetypeGateProbe.ProbeName, CombatTelemetryProbe.ProbeName,
                    ContactProbe.ProbeName, FacingProbe.ProbeName,
                },
                System.Array.ConvertAll(summary.probes, p => p.name));
            foreach (var probe in summary.probes)
            {
                Assert.IsTrue(System.IO.File.Exists(probe.jsonl), $"{probe.name} probe JSONL sidecar missing");
                Assert.IsTrue(System.IO.File.Exists(probe.summary), $"{probe.name} probe summary sidecar missing");
                Assert.AreEqual(seeds.Length * 5, System.IO.File.ReadAllLines(probe.jsonl).Length,
                    $"{probe.name}: one probe row per episode");
            }

            // Mirror block: same substrate, checkpoint vs itself, self-fingerprinted rows.
            var mirrorSpec = new SessionSpec
            {
                onnxAssetPath = ShipAgentFactory.SmokeFixturePath,
                seeds = new[] { seeds[0] },
                tag = "test-eval-mirror",
                episodesPerSeed = 1,
                opponentKind = OpponentKind.Mirror,
                probes = new ProbeSpec[0],
            };
            CheckpointEvaluator.Summary mirrorSummary = default;
            yield return CheckpointEvaluator.Run(NewHost(mirrorSpec), mirrorSpec, spec, s => mirrorSummary = s);

            Assert.AreEqual(1, mirrorSummary.opponents.Length, "a mirror eval is a single opponent block");
            Assert.AreEqual("Mirror", mirrorSummary.opponents[0].opponent);
            Assert.AreEqual(1, mirrorSummary.opponents[0].episodes);
            var mirrorRows = System.IO.File.ReadAllLines(mirrorSummary.episodesJsonl);
            Assert.AreEqual(1, mirrorRows.Length);
            var mirrorRow = JsonUtility.FromJson<EpisodeResult>(mirrorRows[0]);
            Assert.AreEqual("Mirror", mirrorRow.opponent.archetype,
                "mirror episodes must self-fingerprint in the JSONL row");

            // Checkpoint-opponent block: the smoke fixture imported into the second slot is a DISTINCT
            // asset, so both per-side ModelRunners genuinely exist in this one Academy session.
            var opponentAssetPath = TrainingBootstrap.ImportEvalOpponent(ShipAgentFactory.SmokeFixturePath);
            var slot2Spec = new SessionSpec
            {
                onnxAssetPath = ShipAgentFactory.SmokeFixturePath,
                opponentKind = OpponentKind.Checkpoint,
                opponentOnnxAssetPath = opponentAssetPath,
                opponentOnnxSourcePath = ShipAgentFactory.SmokeFixturePath,
                opponentLabel = "ShipCombat-smoke",
                seeds = new[] { seeds[0] },
                tag = "test-eval-slot2",
                episodesPerSeed = 1,
                probes = new ProbeSpec[0],
            };
            CheckpointEvaluator.Summary slot2Summary = default;
            yield return CheckpointEvaluator.Run(NewHost(slot2Spec), slot2Spec, spec, s => slot2Summary = s);

            Assert.AreEqual(1, slot2Summary.opponents.Length, "a checkpoint eval is a single opponent block");
            Assert.AreEqual("ShipCombat-smoke", slot2Summary.opponents[0].opponent,
                "checkpoint blocks are labeled by the opponent checkpoint's stem");
            Assert.AreEqual(opponentAssetPath, slot2Summary.opponentCheckpoint);
            Assert.AreEqual(ShipAgentFactory.SmokeFixturePath, slot2Summary.opponentCheckpointSource,
                "slot-2 provenance must survive into the summary");
            var slot2Row = JsonUtility.FromJson<EpisodeResult>(
                System.IO.File.ReadAllLines(slot2Summary.episodesJsonl)[0]);
            Assert.AreEqual("ShipCombat-smoke", slot2Row.opponent.archetype,
                "checkpoint episodes must fingerprint the opponent stem in the JSONL row");
        }

        /// <summary>Host on an inactive GameObject so its Start never fires — the test composes the arena and drives the client coroutine itself.</summary>
        private HarnessSessionHost NewHost(SessionSpec sessionSpec)
        {
            var hostObject = new GameObject("[HarnessSessionHost]");
            hostObject.transform.SetParent(arenaHost.transform, false);
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<HarnessSessionHost>();
            host.Initialize(sessionSpec, assets, unitService, arena, projectiles);
            return host;
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
