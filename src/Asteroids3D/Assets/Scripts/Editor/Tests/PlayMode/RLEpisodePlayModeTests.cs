#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Tests.Common;
using AI;
using Game;
using Game.Capture;
using Game.RLHarness;
using Game.Services;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    [TestFixture]
    [Category("AI")]
    public class RLEpisodePlayModeTests
    {
        private const float RangerWVelTrack = 50f;
        private const float RangerHoldRange = 15f;
        // The production combat brain (utility chooser + state profiles); the default TestPilotMPC carries no profiles and would sit inert.
        private const string BaselinePilotPath = "Assets/Prefabs/Pilots/UtilityPilot.prefab";

        private GameObject arenaHost;
        private UnitService unitService;
        private ArenaContext arena;
        private readonly List<GameObject> createdObjects = new();
        private readonly List<UnityEngine.Object> createdAssets = new();
        private float savedTimeScale;
        private float savedMaxDelta;
        private float savedCaptureDelta;

        private Ship agent;
        private Ship baseline;
        private RangerChooser ranger;

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

            foreach (var go in createdObjects)
                if (go) UnityEngine.Object.DestroyImmediate(go);
            createdObjects.Clear();

            foreach (var asset in createdAssets)
                if (asset) UnityEngine.Object.DestroyImmediate(asset);
            createdAssets.Clear();

            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            arena = null;
            agent = null;
            baseline = null;
            ranger = null;

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

            SpawnPair(in spec, 0);

            for (var i = 0; i < 3; i++)
            {
                var poses = ResetPair(in spec, i);
                Assert.AreEqual(0, ProjectileFlush.ActiveCount(),
                    $"Episode {i} must start with zero active projectiles");
                AssertPoseApplied(agent, poses.agentPos, $"agent episode {i}");
                AssertPoseApplied(baseline, poses.baselinePos, $"baseline episode {i}");

                var runner = new EpisodeRunner(agent, baseline, spec, i, arena.Offset);
                yield return RunToCompletion(runner, spec);

                var result = runner.Result;
                Assert.AreNotEqual(EpisodeOutcome.Unresolved.ToString(), result.outcome,
                    $"Episode {i} did not terminate legally");
                Assert.Greater(result.decisions, 0);
                AssertFinite(result.sumDense, "sumDense");
                AssertFinite(result.sumShapingEnvelope, "sumShapingEnvelope");
                AssertFinite(result.sumShapingBorder, "sumShapingBorder");
                AssertFinite(result.totalReward, "totalReward");

                var line = result.ToJsonLine();
                var roundTrip = JsonUtility.FromJson<EpisodeResult>(line);
                Assert.AreEqual(EpisodeResult.SchemaId, roundTrip.schema);
                Assert.AreEqual(i, roundTrip.episodeIndex);
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

            SpawnPair(in spec, 0);
            // Warm-up so both recordings run on Burst-compiled solver code (the managed fallback rounds differently).
            for (var i = 0; i < dirtySteps; i++)
                yield return new WaitForFixedUpdate();

            ResetPair(in spec, 0);
            yield return null;
            var trajectoryA = new List<float>();
            yield return Record(trajectoryA, recordSteps);

            ResetPair(in spec, 1);
            yield return null;
            for (var i = 0; i < dirtySteps; i++)
                yield return new WaitForFixedUpdate();

            ResetPair(in spec, 0);
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

            SpawnPair(in spec, 0);
            ResetPair(in spec, 0);

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

            float traceDense = 0f, traceMidPhiEnv = 0f, traceMidPhiBorder = 0f;
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

            var expectedShapingEnv = -result.startPhiEnvelope + (spec.gamma - 1f) * traceMidPhiEnv;
            var expectedShapingBorder = -result.startPhiBorder + (spec.gamma - 1f) * traceMidPhiBorder;
            Assert.AreEqual(expectedShapingEnv, result.sumShapingEnvelope, 1e-3f,
                "Shaping must telescope to −Φ(s₀) + (γ−1)·ΣΦ_mid (terminal Φ forced to 0)");
            Assert.AreEqual(expectedShapingBorder, result.sumShapingBorder, 1e-3f);
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
            if (watchFlag || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RL_WATCH")))
                Time.timeScale = 1f;
            // Watch wins over record: a human watching needs real-time pacing; locked pacing is for reproducible clips.
            using var pacing = record != null && !watchFlag ? CapturePacing.Locked() : null;

            var spec = RewardSpec.Default;
            if (record != null)
                spec.runSeed = record.runSeed;
            var episodes = Mathf.Max(1, int.TryParse(Environment.GetEnvironmentVariable("RL_EPISODE_COUNT"), out var n)
                ? n : (watchFlag || record != null ? 3 : 20));
            if (record?.episodes is { Length: > 0 })
                episodes = Mathf.Max(episodes, record.episodes.Max() + 1);
            var runStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            SpawnPair(in spec, 0);

            var results = new List<EpisodeResult>();
            for (var i = 0; i < episodes; i++)
            {
                ResetPair(in spec, i);
                var runner = new EpisodeRunner(agent, baseline, spec, i, arena.Offset, trace);
                using var recorder = record != null && record.ShouldRecord(i)
                    ? new CaptureRecorder(record.ClipConfig(i, runStamp))
                    : null;
                yield return RunToCompletion(runner, spec, recorder);
                results.Add(runner.Result);
            }

            WriteJsonl("ranger-vs-baseline", results);
        }

        private void SpawnPair(in RewardSpec spec, int episodeIndex)
        {
            var poses = EpisodePoses.Derive(in spec, episodeIndex, arena.Offset);
            var rootScope = new SeedScope(spec.runSeed);

            agent = SpawnLasersOnlyShip(TestAssets.LoadTestPilotMpc(),
                poses.agentPos, poses.agentRotDeg, team: 0, decisionSeed: rootScope.Derive(101).ToSeed());
            baseline = SpawnLasersOnlyShip(TestAssets.LoadCommanderPrefab(BaselinePilotPath),
                poses.baselinePos, poses.baselineRotDeg, team: 1, decisionSeed: rootScope.Derive(202).ToSeed());

            var cmdr = agent.GetComponentInChildren<AICommander>();
            var nav = cmdr.Navigator;
            var settings = UnityEngine.Object.Instantiate(
                nav.mpcSettings ? nav.mpcSettings : ScriptableObject.CreateInstance<MpcSettings>());
            settings.wVelTrack = RangerWVelTrack;
            nav.mpcSettings = settings;
            createdAssets.Add(settings);

            ranger = new RangerChooser();
            ranger.Configure(baseline, RangerHoldRange,
                agent.Weapons.Context.ProjectileSpeed(WeaponSlot.Primary));
            var brain = cmdr.GetComponentInChildren<Brain>();
            typeof(Brain).GetField("chooser", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(brain, ranger);

            unitService.WireShipDependencies(agent);
            unitService.WireShipDependencies(baseline);
            Assert.AreNotEqual("None", baseline.GetComponentInChildren<AICommander>().CurrentStateName,
                "Baseline brain must run a real state policy");
        }

        private Ship SpawnLasersOnlyShip(AICommander pilotPrefab, Vector2 planePos, float rotDeg, int team, int decisionSeed)
        {
            Assert.IsNotNull(pilotPrefab, "Failed to load pilot prefab — check test asset paths");
            var ship = ShipTestFactory.CreateShip(TestAssets.LoadShip2Prefab(), pilotPrefab,
                team, GamePlane.PlanePointToWorld(planePos), PoseRotation(rotDeg), decisionSeed);
            Assert.IsNotNull(ship, "Failed to create ship — check test asset paths");
            createdObjects.Add(ship.gameObject);
            unitService.ActiveRegistry.ActiveShips.Add(ship);

            ship.Reequip(ship.Engine, ship.Shield, ship.Weapons.PrimaryMountPrefab, null);
            Assert.AreEqual(1, ship.Weapons.Context.Slots.Count, "Episode loadout must be lasers-only");
            return ship;
        }

        private SpawnPoses ResetPair(in RewardSpec spec, int episodeIndex)
        {
            var poses = EpisodePoses.Derive(in spec, episodeIndex, arena.Offset);
            unitService.RespawnShip(agent.Id, poses.agentPos, poses.agentRotDeg);
            unitService.RespawnShip(baseline.Id, poses.baselinePos, poses.baselineRotDeg);
            ProjectileFlush.ReturnAllToPool();
            return poses;
        }

        private IEnumerator RunToCompletion(EpisodeRunner runner, RewardSpec spec, CaptureRecorder recorder = null)
        {
            var captureSubjects = new Vector2[2];
            Action<CaptureDraw> drawOverlay = ctx => ShipDiagnosticsOverlay.Draw(ctx, agent, baseline);
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

        private static Quaternion PoseRotation(float rotDeg) =>
            GamePlane.Rotation * Quaternion.AngleAxis(rotDeg, Vector3.forward);

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

        private static void WriteJsonl(string tag, List<EpisodeResult> results)
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            var dir = Path.Combine(repoRoot, "results", "rl-episodes");
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(dir, $"{stamp}-{tag}.jsonl");
            using (var writer = new StreamWriter(path, false))
                foreach (var result in results)
                    writer.WriteLine(result.ToJsonLine());
            Debug.Log($"[RLEpisode] wrote {results.Count} rows to {path}");
        }
    }
}
#endif
