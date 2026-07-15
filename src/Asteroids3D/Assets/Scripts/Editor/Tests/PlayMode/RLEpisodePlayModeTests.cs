#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Tests.Common;
using Game;
using Game.RLHarness;
using Game.Services;
using NUnit.Framework;
using Ships;
using Ships.Command;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    [TestFixture]
    [Category("AI")]
    public class RLEpisodePlayModeTests
    {
        private const float RangerHoldRange = 15f;

        private GameObject arenaHost;
        private UnitService unitService;
        private ArenaContext arena;
        private float savedTimeScale;
        private float savedMaxDelta;
        private float savedCaptureDelta;

        private EpisodePair pair;
        private Ship agent;
        private Ship baseline;

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            arenaHost = new GameObject("[EpisodeArena]");
            unitService = arenaHost.AddComponent<UnitService>();
            arena = TestArena.On(arenaHost, unitService.ActiveRegistry);
            unitService.SetArena(arena);
            SweepForeignDebris();

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

            ProjectileFlush.ReturnAllToPool();

            pair?.Dispose();
            pair = null;
            agent = null;
            baseline = null;

            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            arena = null;

            AudioListener.pause = false;
        }

        // Debris leaked by earlier fixtures (drifting ships, live projectiles) enters scans and cover checks and varies between recordings, breaking trajectory equivalence.
        private void SweepForeignDebris()
        {
            ProjectileFlush.ReturnAllToPool();
            foreach (var ship in UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(ship.gameObject);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator EpisodeLoop_Smoke_BackToBackEpisodesTerminateLegally()
        {
            var spec = RewardSpec.Default;
            spec.timeoutDecisions = 150;
            spec.minSeparation = 18f;
            spec.maxSeparation = 24f;

            SpawnPair(in spec);

            for (var i = 0; i < 3; i++)
            {
                var poses = pair.Reset(in spec, i);
                Assert.AreEqual(0, ProjectileFlush.ActiveCount(),
                    $"Episode {i} must start with zero active projectiles");
                AssertPoseApplied(agent, poses.agentPos, $"agent episode {i}");
                AssertPoseApplied(baseline, poses.baselinePos, $"baseline episode {i}");

                var runner = new EpisodeRunner(agent, baseline, spec, i, arena.Offset);
                yield return RunToCompletion(runner, spec);

                var result = runner.Result;
                Assert.AreNotEqual(EpisodeOutcome.Unresolved.ToString(), result.outcome,
                    $"Episode {i} did not terminate legally");
                Assert.AreNotEqual(EndKind.None.ToString(), result.endKind,
                    $"Episode {i} must report how it ended");
                Assert.Greater(result.decisions, 0);
                AssertFinite(result.sumDense, "sumDense");
                AssertFinite(result.sumShapingEnvelope, "sumShapingEnvelope");
                AssertFinite(result.sumShapingBorder, "sumShapingBorder");
                AssertFinite(result.totalReward, "totalReward");

                var line = result.ToJsonLine();
                var roundTrip = JsonUtility.FromJson<EpisodeResult>(line);
                Assert.AreEqual(EpisodeResult.SchemaId, roundTrip.schema);
                Assert.AreEqual(i, roundTrip.episodeIndex);
                Assert.AreEqual(result.endKind, roundTrip.endKind);
                Assert.AreEqual(spec.runSeed, roundTrip.spec.runSeed);
                Assert.AreEqual(spec.decisionIntervalSteps, roundTrip.spec.decisionIntervalSteps);
            }

            var again = EpisodePoses.Derive(in spec, 1, arena.Offset);
            var reference = EpisodePoses.Derive(in spec, 1, arena.Offset);
            Assert.AreEqual(reference.agentPos, again.agentPos, "(runSeed, i) must reproduce poses");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator TrajectoryEquivalence_PairResetRestoresFreshSpawnState()
        {
            // Locked frame pacing (1 fixed step per frame) removes Update/FixedUpdate phase noise from the comparison.
            Time.timeScale = 1f;
            Time.captureDeltaTime = Time.fixedDeltaTime;

            var spec = RewardSpec.Default;
            spec.minSeparation = 18f;
            spec.maxSeparation = 22f;

            // 2 s at sub-physical tolerance: forgotten resets diverge immediately and macroscopically; longer windows reach combat, where pool identity and CEM near-ties amplify float noise into honest drift.
            const int recordSteps = 100;
            const int dirtySteps = 80;

            SpawnPair(in spec);
            // Warm-up so both recordings run on Burst-compiled solver code (the managed fallback rounds differently).
            for (var i = 0; i < dirtySteps; i++)
                yield return new WaitForFixedUpdate();

            pair.Reset(in spec, 0);
            yield return null;
            var trajectoryA = new List<float>();
            yield return Record(trajectoryA, recordSteps);

            pair.Reset(in spec, 1);
            yield return null;
            for (var i = 0; i < dirtySteps; i++)
                yield return new WaitForFixedUpdate();

            pair.Reset(in spec, 0);
            yield return null;
            var trajectoryB = new List<float>();
            yield return Record(trajectoryB, recordSteps);

            Assert.AreEqual(trajectoryA.Count, trajectoryB.Count);
            for (var i = 0; i < trajectoryA.Count; i++)
                Assert.AreEqual(trajectoryA[i], trajectoryB[i], 1e-3f,
                    $"Trajectory diverged at sample {i / 10} channel {i % 10}: a pair-reset left stale state behind — fix the reset, never loosen this test");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Telescoping_SumsMatchPoolSwingAndStartPotential()
        {
            var spec = RewardSpec.Default;
            spec.timeoutDecisions = 120;
            spec.minSeparation = 18f;
            spec.maxSeparation = 24f;

            SpawnPair(in spec);
            pair.Reset(in spec, 0);

            var runner = new EpisodeRunner(agent, baseline, spec, 0, arena.Offset, tracePerDecision: true);
            yield return RunToCompletion(runner, spec);
            var result = runner.Result;
            Assert.AreNotEqual(EpisodeOutcome.Unresolved.ToString(), result.outcome);

            var myMaxPool = agent.Damage.Health.MaxValue + agent.Damage.Shield.MaxValue;
            var enemyMaxPool = baseline.Damage.Health.MaxValue + baseline.Damage.Shield.MaxValue;
            var expectedDense = spec.lambda * (result.startEnemyPool - result.endEnemyPool) / enemyMaxPool
                                - spec.lambda * (result.startMyPool - result.endMyPool) / myMaxPool;
            Assert.AreEqual(expectedDense, result.sumDense, 1e-3f,
                "Delta-sampled dense contributions must telescope to the start-to-end pool differential");

            AssertShapingTelescopes(result);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Telescoping_TruncationKeepsFinalPotential()
        {
            var spec = RewardSpec.Default;
            // Short clock, wide start: guarantees a timeout end so the truncation path (Φ kept, not forced to 0) is what telescopes.
            spec.timeoutDecisions = 12;
            spec.minSeparation = 50f;
            spec.maxSeparation = 60f;

            SpawnPair(in spec);
            pair.Reset(in spec, 0);

            var runner = new EpisodeRunner(agent, baseline, spec, 0, arena.Offset, tracePerDecision: true);
            yield return RunToCompletion(runner, spec);
            var result = runner.Result;

            Assert.AreEqual(EndKind.Truncation.ToString(), result.endKind);
            Assert.AreEqual(EpisodeOutcome.Draw.ToString(), result.outcome);
            Assert.IsTrue(result.timedOut);
            Assert.AreEqual(spec.timeoutDecisions, result.decisions);
            Assert.AreEqual(EndKind.Truncation, runner.LastBoundary.endKind);
            Assert.AreEqual(0f, runner.LastBoundary.outcomeReward);

            AssertShapingTelescopes(result);
        }

        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator Characterization_WritesJsonl()
        {
            var watchFlag = System.IO.File.Exists(System.IO.Path.Combine(
                Application.dataPath, "..", "..", "..", "results", "rl-episodes", "watch.flag"));
            if (!watchFlag && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RL_EPISODES")))
                Assert.Ignore("Set RL_EPISODES=1 (or create results/rl-episodes/watch.flag) to run the ranger-vs-baseline characterization.");

            var trace = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RL_EPISODE_TRACE"));
            // Pacing contract: all RL runs (characterization included) share frame ≙ fixed-step sim semantics.
            Time.timeScale = 1f;
            Time.captureDeltaTime = Time.fixedDeltaTime;
            var spec = RewardSpec.Default;
            var episodes = Mathf.Max(1, int.TryParse(Environment.GetEnvironmentVariable("RL_EPISODE_COUNT"), out var n)
                ? n : (watchFlag ? 3 : 20));

            SpawnPair(in spec);

            var path = EpisodeJsonl.NewRunPath("ranger-vs-baseline");
            for (var i = 0; i < episodes; i++)
            {
                pair.Reset(in spec, i);
                var runner = new EpisodeRunner(agent, baseline, spec, i, arena.Offset, trace);
                yield return RunToCompletion(runner, spec);
                EpisodeJsonl.Append(path, runner.Result);
            }

            Debug.Log($"[RLEpisode] wrote {episodes} rows to {path}");
        }

        private void SpawnPair(in RewardSpec spec)
        {
            pair = EpisodePair.Spawn(unitService, arena, in spec, (agentShip, baselineShip) =>
            {
                var ranger = new RangerChooser();
                ranger.Configure(baselineShip, RangerHoldRange,
                    agentShip.Weapons.Context.ProjectileSpeed(WeaponSlot.Primary));
                return ranger;
            });
            agent = pair.Agent;
            baseline = pair.Baseline;
        }

        private static void AssertShapingTelescopes(in EpisodeResult result)
        {
            float traceMidPhiEnv = 0f, traceMidPhiBorder = 0f, traceDense = 0f;
            for (var i = 0; i < result.trace.Count; i++)
            {
                traceDense += result.trace[i].dense;
                if (i < result.trace.Count - 1)
                {
                    traceMidPhiEnv += result.trace[i].phiEnvelope;
                    traceMidPhiBorder += result.trace[i].phiBorder;
                }
            }
            Assert.AreEqual(result.sumDense, traceDense, 1e-4f);

            // −Φ(s₀) + (γ−1)·ΣΦ_mid + γ·Φ_end, where Φ_end is 0 on terminal and the kept final potential on truncation.
            var expectedShapingEnv = -result.startPhiEnvelope
                + (result.spec.gamma - 1f) * traceMidPhiEnv + result.spec.gamma * result.endPhiEnvelope;
            var expectedShapingBorder = -result.startPhiBorder
                + (result.spec.gamma - 1f) * traceMidPhiBorder + result.spec.gamma * result.endPhiBorder;
            Assert.AreEqual(expectedShapingEnv, result.sumShapingEnvelope, 1e-3f,
                "Shaping must telescope to −Φ(s₀) + (γ−1)·ΣΦ_mid + γ·Φ_end");
            Assert.AreEqual(expectedShapingBorder, result.sumShapingBorder, 1e-3f);
        }

        private IEnumerator RunToCompletion(EpisodeRunner runner, RewardSpec spec)
        {
            runner.Begin();
            var maxSimSeconds = spec.timeoutDecisions * spec.decisionIntervalSteps * Time.fixedDeltaTime;
            var deadline = Time.realtimeSinceStartup + 120f + maxSimSeconds;
            while (!runner.IsDone && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
                runner.Tick();
            }
            Assert.IsTrue(runner.IsDone, "Episode wall-clock deadline exceeded before termination");
        }

        private IEnumerator Record(List<float> samples, int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                Sample(samples, agent);
                Sample(samples, baseline);
            }
        }

        private static void Sample(List<float> samples, Ship ship)
        {
            var k = ship.Kinematics;
            samples.Add(k.pos.x);
            samples.Add(k.pos.y);
            samples.Add(k.vel.x);
            samples.Add(k.vel.y);
            samples.Add(k.yaw);
        }

        private static void AssertPoseApplied(Ship ship, Vector2 expectedPlanePos, string label)
        {
            var actual = GamePlane.WorldPointToPlane(ship.transform.position);
            Assert.Less((actual - expectedPlanePos).magnitude, 1.5f,
                $"Respawn did not place the {label} at its (runSeed, i)-derived pose");
        }

        private static void AssertFinite(float value, string name)
        {
            Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value), $"{name} must be finite, was {value}");
        }
    }
}
#endif
