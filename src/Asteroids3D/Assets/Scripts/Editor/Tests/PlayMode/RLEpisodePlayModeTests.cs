#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AI;
using AI.Observation;
using Tests.Common;
using Tests.PlayMode.Common;
using Game;
using Game.Capture;
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
        private ProjectileService projectiles;
        private float savedTimeScale;
        private float savedMaxDelta;
        private float savedCaptureDelta;

        private EpisodePair pair;
        private Ship agent;
        private Ship baseline;
        private HarnessField field;

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            arenaHost = new GameObject("[EpisodeArena]");
            unitService = arenaHost.AddComponent<UnitService>();
            arena = TestArena.On(arenaHost, unitService.ActiveRegistry);
            unitService.SetArena(arena);
            projectiles = new ProjectileService(arenaHost.transform);
            unitService.SetProjectiles(projectiles);
            AssertNoForeignDebris();

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

            field?.Dispose();
            field = null;
            pair?.Dispose();
            pair = null;
            agent = null;
            baseline = null;

            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            arena = null;
            projectiles = null;

            AudioListener.pause = false;
        }

        // Debris leaked by earlier fixtures (drifting ships, live projectiles) enters scans and cover checks and varies between recordings, breaking trajectory equivalence. Registration is mandatory and transients die with their fixture root, so debris here is a leaking fixture to FIX — assert, never sweep.
        private static void AssertNoForeignDebris()
        {
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<Combat.Projectile.ProjectileBase>(FindObjectsSortMode.None).Length,
                "A previous fixture leaked live projectiles — its transients escaped their registry/root");
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None).Length,
                "A previous fixture leaked ships — fix its teardown");
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
                Assert.AreEqual(0, projectiles.ActiveCount,
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
        public IEnumerator TrajectoryEquivalence_FieldEnabled_PairResetRestoresFreshFieldAndSpawnState()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = Time.fixedDeltaTime;

            var spec = RewardSpec.Default;
            spec.minSeparation = 18f;
            spec.maxSeparation = 22f;
            spec.useAsteroidField = true;

            const int recordSteps = 100;
            const int dirtySteps = 80;

            field = HarnessField.Spawn(arena, spec.fieldDensityScale, arenaHost.transform);
            SpawnPair(in spec);
            for (var i = 0; i < dirtySteps; i++)
                yield return new WaitForFixedUpdate();

            ResetWithField(in spec, 0);
            var digestA = CaptureFieldDigest(in spec);
            Assert.Greater(digestA.Count, 1, "Field-enabled episode must actually contain asteroids");
            AssertSpawnClearings(in spec, 0);
            yield return null;
            var trajectoryA = new List<float>();
            yield return Record(trajectoryA, recordSteps);

            ResetWithField(in spec, 1);
            yield return null;
            for (var i = 0; i < dirtySteps; i++)
                yield return new WaitForFixedUpdate();

            ResetWithField(in spec, 0);
            var digestB = CaptureFieldDigest(in spec);
            yield return null;
            var trajectoryB = new List<float>();
            yield return Record(trajectoryB, recordSteps);

            Assert.AreEqual(digestA.Count, digestB.Count,
                "Field rebuild returned a different asteroid set — the layout is not a pure function of (runSeed, episodeIndex)");
            for (var i = 0; i < digestA.Count; i++)
                Assert.AreEqual(digestA[i], digestB[i],
                    $"Field digest diverged at channel {i}: a rebuild left stale field state behind — fix the reset, never loosen this test");

            Assert.AreEqual(trajectoryA.Count, trajectoryB.Count);
            for (var i = 0; i < trajectoryA.Count; i++)
                Assert.AreEqual(trajectoryA[i], trajectoryB[i], 1e-3f,
                    $"Trajectory diverged at sample {i / 10} channel {i % 10}: a pair-reset left stale state behind — fix the reset, never loosen this test");
        }

        // Anchorless fields ran collision-less for a full gate sweep before this pin existed (2026-07-18) — never loosen it.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator FieldColliders_AnchorlessFieldKeepsDetailedCollidersLive()
        {
            var spec = RewardSpec.Default;
            spec.useAsteroidField = true;

            field = HarnessField.Spawn(arena, spec.fieldDensityScale, arenaHost.transform);
            field.Reset(in spec, 0, EpisodePoses.Derive(in spec, 0, arena.Offset));
            yield return null; // frame 1: the field's Start runs the staged build
            yield return null; // frame 2: LateUpdate applies the no-anchor collider default

            var controllers = field.Field.GetComponentsInChildren<Asteroids.AsteroidController>();
            Assert.Greater(controllers.Length, 0, "Field-enabled episode must actually contain asteroids");
            foreach (var controller in controllers)
            {
                var mesh = controller.GetComponent<MeshCollider>();
                if (!mesh) continue;
                Assert.IsTrue(mesh.enabled,
                    $"Anchorless asteroid '{controller.name}' has a disabled MeshCollider — ships fly through it");
            }
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ObstacleTokens_FieldEnabled_FillProducesSortedNonZeroTokens()
        {
            var spec = RewardSpec.Default;
            spec.useAsteroidField = true;

            field = HarnessField.Spawn(arena, spec.fieldDensityScale, arenaHost.transform);
            SpawnPair(in spec);
            ResetWithField(in spec, 0);
            yield return null; // frame 1: the field's Start runs the staged build
            yield return null; // frame 2: Scout.Update scans the live field

            var scout = agent.GetComponentInChildren<AICommander>().Scout;
            var scan = scout.AsteroidScan;
            Assert.Greater(scan.count, 0, "Scout must see asteroids in a field-enabled episode");

            var enemyKin = baseline.Kinematics;
            var target = new TargetView(true, enemyKin.pos, enemyKin.vel, enemyKin.Forward,
                baseline.HealthPct, baseline.ShieldPct);
            var buffer = new float[AgentObservations.Size];
            AgentObservations.Fill(buffer, agent, in target,
                inMyEnvelope: false, inEnemyEnvelope: false, primaryWeaponReady: false, primaryHeatPct: 0f,
                arena.Offset, spec.arenaRadius, scan);

            var occupied = Mathf.Min(AgentObservations.ObstacleTokenCount, scan.count);
            Assert.Greater(occupied, 0);
            var previousDistance = 0f;
            for (var s = 0; s < occupied; s++)
            {
                var b = 24 + s * AgentObservations.ObstacleTokenFloats;
                Assert.Greater(buffer[b + 5], 0f, $"occupied slot {s} must carry a real radius");
                Assert.Greater(buffer[b + 2], previousDistance, $"slot {s} must sort ascending by distance");
                previousDistance = buffer[b + 2];
            }
            for (var s = occupied; s < AgentObservations.ObstacleTokenCount; s++)
                for (var c = 0; c < AgentObservations.ObstacleTokenFloats; c++)
                    Assert.AreEqual(0f, buffer[24 + s * AgentObservations.ObstacleTokenFloats + c],
                        $"slot {s} channel {c} must zero-pad");
        }

        /// <summary>The episode-boundary contract in host order: field rebuild first (poses become clearings), then the pair-reset onto the carved ground.</summary>
        private void ResetWithField(in RewardSpec spec, int episodeIndex)
        {
            var poses = EpisodePoses.Derive(in spec, episodeIndex, arena.Offset);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            field.Reset(in spec, episodeIndex, in poses);
            sw.Stop();
            Debug.Log($"[FieldReset] episode {episodeIndex}: RebuildField took {sw.Elapsed.TotalMilliseconds:F1} ms");
            pair.Reset(in spec, episodeIndex);
        }

        /// <summary>The full loaded-asteroid set (count + positions + radii) via the same QueryObstacles the MPC scans, captured synchronously after the rebuild so it reflects generation, not drift.</summary>
        private List<float> CaptureFieldDigest(in RewardSpec spec)
        {
            var buffer = new AI.Scanning.DetectedObstacle[1024];
            var count = arena.ObstacleField.QueryObstacles(arena.Offset, spec.arenaRadius + 40f, buffer);
            Assert.Less(count, buffer.Length, "Digest buffer saturated — it must hold the FULL loaded set");
            var digest = new List<float>(1 + count * 3) { count };
            for (var i = 0; i < count; i++)
            {
                digest.Add(buffer[i].position.x);
                digest.Add(buffer[i].position.y);
                digest.Add(buffer[i].radius);
            }
            return digest;
        }

        private void AssertSpawnClearings(in RewardSpec spec, int episodeIndex)
        {
            var poses = EpisodePoses.Derive(in spec, episodeIndex, arena.Offset);
            var buffer = new AI.Scanning.DetectedObstacle[1024];
            var count = arena.ObstacleField.QueryObstacles(arena.Offset, spec.arenaRadius + 40f, buffer);
            for (var i = 0; i < count; i++)
            {
                Assert.Greater((buffer[i].position - poses.agentPos).magnitude, HarnessField.SpawnClearRadius,
                    "Generation must carve a clearing around the agent spawn");
                Assert.Greater((buffer[i].position - poses.baselinePos).magnitude, HarnessField.SpawnClearRadius,
                    "Generation must carve a clearing around the baseline spawn");
            }
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
            Assert.AreEqual(-spec.timeCostPerDecision * result.decisions, result.sumTimeCost, 1e-6f,
                "Every paid decision pays the flat time cost");

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
            Assert.AreEqual(-spec.timeCostPerDecision * spec.timeoutDecisions, result.sumTimeCost, 1e-6f,
                "A full-clock draw nets the whole time-cost drag — waiting is never free");

            AssertShapingTelescopes(result);
        }

        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator Characterization_WritesJsonl()
        {
            var resultsDir = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "results", "rl-episodes"));
            var watchFlag = File.Exists(Path.Combine(resultsDir, "watch.flag"));
            var recordPath = Path.Combine(resultsDir, "record.flag");
            var record = File.Exists(recordPath) ? LoadRecordConfig(recordPath) : null;
            if (!watchFlag && record == null && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RL_EPISODES")))
                Assert.Ignore("Set RL_EPISODES=1 (or create results/rl-episodes/watch.flag or record.flag) to run the ranger-vs-baseline characterization.");

            var trace = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RL_EPISODE_TRACE"));
            // Watch (human real-time eyeball) is the one unlocked mode; record and measurement runs keep the pacing contract.
            using var pacing = record != null && !watchFlag ? CapturePacing.Locked() : null;
            if (watchFlag || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RL_WATCH")))
                Time.timeScale = 1f;
            else if (record == null)
                PacingContract.Apply();

            var spec = RewardSpec.Default;
            if (record != null)
                spec.runSeed = record.runSeed;
            var episodes = Mathf.Max(1, int.TryParse(Environment.GetEnvironmentVariable("RL_EPISODE_COUNT"), out var n)
                ? n : (watchFlag || record != null ? 3 : 20));
            if (record?.episodes is { Length: > 0 })
                episodes = Mathf.Max(episodes, record.episodes.Max() + 1);
            var runStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            SpawnPair(in spec);

            var path = EpisodeJsonl.NewRunPath("ranger-vs-baseline");
            for (var i = 0; i < episodes; i++)
            {
                pair.Reset(in spec, i);
                var runner = new EpisodeRunner(agent, baseline, spec, i, arena.Offset, trace);
                using var recorder = record != null && record.ShouldRecord(i)
                    ? new CaptureRecorder(record.ClipConfig(i, runStamp))
                    : null;
                yield return RunToCompletion(runner, spec, recorder);
                EpisodeJsonl.Append(path, runner.Result);
            }

            Debug.Log($"[RLEpisode] wrote {episodes} rows to {path}");
        }

        private void SpawnPair(in RewardSpec spec)
        {
            pair = EpisodePair.Spawn(unitService, arena, projectiles, in spec, (agentShip, baselineShip) =>
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

        private IEnumerator RunToCompletion(EpisodeRunner runner, RewardSpec spec, CaptureRecorder recorder = null)
        {
            var captureSubjects = new Vector2[2];
            Action<CaptureDraw> drawOverlay = ctx => ShipDiagnosticsOverlay.Draw(ctx, agent, baseline, projectiles);
            runner.Begin();
            var maxSimSeconds = spec.timeoutDecisions * spec.decisionIntervalSteps * Time.fixedDeltaTime;
            // Synchronous render/readback/PNG on captured steps eats wall clock; the sim-step timeout still bounds the episode itself.
            var wallClockScale = recorder != null ? 10f : 1f;
            var deadline = Time.realtimeSinceStartup + 120f + maxSimSeconds * wallClockScale;
            while (!runner.IsDone && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
                runner.Tick();
                if (recorder != null)
                {
                    captureSubjects[0] = agent.Kinematics.pos;
                    captureSubjects[1] = baseline.Kinematics.pos;
                    recorder.Step(captureSubjects, drawOverlay);
                }
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

        [Serializable]
        private sealed class RecordConfig
        {
            public int runSeed;
            public int[] episodes;
            public int captureEveryFixedSteps = 5;
            public int width = 960;
            public int height = 540;

            /// <summary>Empty/absent episodes list records every episode run.</summary>
            public bool ShouldRecord(int episode) =>
                episodes == null || episodes.Length == 0 || Array.IndexOf(episodes, episode) >= 0;

            public CaptureConfig ClipConfig(int episode, string runStamp) => new()
            {
                outputRoot = "results/rl-episodes",
                clipName = $"ep{episode:D2}",
                runStamp = runStamp,
                width = width,
                height = height,
                everyFixedSteps = captureEveryFixedSteps,
            };
        }

        private static RecordConfig LoadRecordConfig(string path)
        {
            var json = File.ReadAllText(path);
            var config = new RecordConfig { runSeed = RewardSpec.Default.runSeed };
            if (string.IsNullOrWhiteSpace(json)) return config;

            // FromJsonOverwrite silently ignores unknown keys — a typo'd key would quietly keep its default — so whitelist every key first.
            var validKeys = new[] { "runSeed", "episodes", "captureEveryFixedSteps", "width", "height" };
            foreach (Match match in Regex.Matches(json, "\"([^\"]+)\"\\s*:"))
                if (Array.IndexOf(validKeys, match.Groups[1].Value) < 0)
                    Assert.Fail($"record.flag: unknown key '{match.Groups[1].Value}' — valid keys: {string.Join(", ", validKeys)}");

            try
            {
                JsonUtility.FromJsonOverwrite(json, config);
            }
            catch (Exception e)
            {
                Assert.Fail($"record.flag: malformed JSON — {e.Message}");
            }

            if (config.episodes != null)
                for (var i = 0; i < config.episodes.Length; i++)
                {
                    if (config.episodes[i] < 0)
                        Assert.Fail($"record.flag: negative episode index {config.episodes[i]}");
                    if (Array.IndexOf(config.episodes, config.episodes[i]) != i)
                        Assert.Fail($"record.flag: duplicate episode index {config.episodes[i]}");
                }

            return config;
        }
    }
}
#endif
