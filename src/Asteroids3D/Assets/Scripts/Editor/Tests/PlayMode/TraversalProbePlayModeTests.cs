#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AI;
using Game.RLHarness;
using Game.Services;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Tests.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>The asteroid-traversal probe: diameter crossings of the harness field on the combat-episode airframe, per-density completion/speed/collision curves. The gate before any asteroid-curriculum training spend, and a durable MPC-tuning instrument — the driver is an <see cref="IIntentChooser"/> seam (scripted velocity reference vs the legacy nav/terminal-field goal-mode stack; a learned policy slots in post-PR-B).</summary>
    [TestFixture]
    [Category("AI")]
    public class TraversalProbePlayModeTests
    {
        private GameObject arenaHost;
        private UnitService unitService;
        private ArenaContext arena;
        private ProjectileService projectiles;
        private float savedTimeScale;
        private float savedMaxDelta;
        private float savedCaptureDelta;

        private HarnessField field;
        private Ship ship;
        private MpcSettings settingsClone;

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            arenaHost = new GameObject("[TraversalArena]");
            unitService = arenaHost.AddComponent<UnitService>();
            arena = TestArena.On(arenaHost, unitService.ActiveRegistry);
            unitService.SetArena(arena);
            projectiles = new ProjectileService(arenaHost.transform);
            unitService.SetProjectiles(projectiles);

            savedTimeScale = Time.timeScale;
            savedMaxDelta = Time.maximumDeltaTime;
            savedCaptureDelta = Time.captureDeltaTime;
            Time.maximumDeltaTime = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = savedTimeScale;
            Time.maximumDeltaTime = savedMaxDelta;
            Time.captureDeltaTime = savedCaptureDelta;

            field?.Dispose();
            field = null;
            if (ship)
            {
                unitService.ActiveRegistry.ActiveShips.Remove(ship);
                UnityEngine.Object.DestroyImmediate(ship.gameObject);
            }
            ship = null;
            if (settingsClone) UnityEngine.Object.DestroyImmediate(settingsClone);
            settingsClone = null;

            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            arena = null;
            projectiles = null;

            AudioListener.pause = false;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Smoke_VelocityDriver_CrossesSparseField()
        {
            PacingContract.Apply();
            ComposeProbe(maxDensityScale: 0.5f);

            var spec = TraversalSpec.Default;
            spec.densityScale = 0.5f;
            spec.speedFraction = 0.9f;

            TraversalResult result = default;
            yield return RunCrossing(spec, 0, r => result = r);

            Assert.AreEqual(TraversalResult.SchemaId, result.schema);
            Assert.AreNotEqual(TraversalOutcome.Unresolved.ToString(), result.outcome,
                "Crossing must terminate (arrival, death, or timeout)");
            Assert.Greater(result.alongTrack, 0.5f * spec.crossingRadius,
                "Ship made no meaningful crossing progress — chooser/MPC wiring broken?");
            Assert.Greater(result.effectiveSpeed, 0f);

            var roundTrip = JsonUtility.FromJson<TraversalResult>(result.ToJsonLine());
            Assert.AreEqual(result.outcome, roundTrip.outcome);
            Assert.AreEqual(spec.densityScale, roundTrip.spec.densityScale);
            Assert.AreEqual(VelocityTraversalChooser.DriverTag, roundTrip.spec.driver);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Smoke_LegacyDriver_CrossesViaAuthoredWaypointRegime()
        {
            PacingContract.Apply();
            ComposeProbe(maxDensityScale: 0.5f);

            var spec = TraversalSpec.Default;
            spec.driver = LegacyNavTraversalChooser.DriverTag;
            spec.densityScale = 0.5f;
            spec.speedFraction = 0.9f;
            // Generous budget: the comparator's speed is an authored-asset property, not the probe's to assume; the sweep's speed curves report it.
            spec.timeoutFactor = 12f;

            TraversalResult result = default;
            yield return RunCrossing(spec, 0, r => result = r);

            Assert.AreNotEqual(TraversalOutcome.Unresolved.ToString(), result.outcome);
            Assert.Greater(result.alongTrack, 0.5f * spec.crossingRadius,
                "Legacy waypoint stack made no meaningful crossing progress");
        }

        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator Sweep_WritesJsonl()
        {
            var resultsDir = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "results", "rl-probe"));
            var watchFlag = File.Exists(Path.Combine(resultsDir, "watch.flag"));
            if (!watchFlag && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RL_PROBE")))
                Assert.Ignore("Set RL_PROBE=1 (or create results/rl-probe/watch.flag) to run the traversal-probe sweep.");

            // Watch (human real-time eyeball) is the one unlocked mode; measurement runs keep the pacing contract.
            if (watchFlag) Time.timeScale = 1f;
            else PacingContract.Apply();

            var densities = watchFlag ? new[] { 1f } : new[] { 0.5f, 1f, 1.5f, 2f, 3f };
            var speedFractions = watchFlag ? new[] { 0.9f } : new[] { 0.5f, 0.9f };
            var layoutSeeds = watchFlag ? new[] { 1 } : new[] { 1, 2, 3 };
            var wVelTracks = new[] { 50f };
            var drivers = new[] { VelocityTraversalChooser.DriverTag, LegacyNavTraversalChooser.DriverTag };

            ComposeProbe(maxDensityScale: densities[densities.Length - 1]);

            var path = EpisodeJsonl.NewRunPath("traversal", "rl-probe");
            var episodeIndex = 0;
            foreach (var driver in drivers)
            foreach (var density in densities)
            foreach (var speedFraction in speedFractions)
            foreach (var wVelTrack in wVelTracks)
            {
                var cell = TraversalSpec.Default;
                cell.driver = driver;
                cell.densityScale = density;
                cell.speedFraction = speedFraction;
                cell.wVelTrack = wVelTrack;
                if (driver == LegacyNavTraversalChooser.DriverTag) cell.timeoutFactor = 12f;

                var rows = new List<TraversalResult>();
                foreach (var layoutSeed in layoutSeeds)
                {
                    var spec = cell;
                    spec.layoutSeed = layoutSeed;
                    TraversalResult result = default;
                    yield return RunCrossing(spec, episodeIndex++, r => result = r);
                    rows.Add(result);
                    File.AppendAllText(path, result.ToJsonLine() + "\n");
                }
                File.AppendAllText(path, TraversalSummary.Summarize(in cell, rows).ToJsonLine() + "\n");
            }

            Debug.Log($"[TraversalProbe] wrote {episodeIndex} crossings (+summaries) to {path}");
        }

        /// <summary>Single-ship composition: field + arena + the combat-episode airframe on the inert TestPilotMPC host. Field pool pre-sizes for the densest sweep cell (the pool cap is fixed at first spawn).</summary>
        private void ComposeProbe(float maxDensityScale)
        {
            field = HarnessField.Spawn(arena, maxDensityScale, arenaHost.transform);

            ship = EpisodePair.SpawnLasersOnlyShip(unitService, projectiles, EpisodePair.AgentPilotPath,
                Vector2.zero, 0f, team: 0, decisionSeed: 1234567);
            unitService.WireShipDependencies(ship);

            var nav = ship.GetComponentInChildren<AICommander>().Navigator;
            settingsClone = UnityEngine.Object.Instantiate(nav.mpcSettings);
            nav.mpcSettings = settingsClone;
        }

        private IEnumerator RunCrossing(TraversalSpec spec, int episodeIndex, Action<TraversalResult> onComplete)
        {
            TraversalCrossing.Derive(in spec, arena.Offset, out var start, out var destination, out var dir);

            field.SetDensityScale(spec.densityScale);
            field.Rebuild(spec.layoutSeed, start, destination);

            var brain = ship.GetComponentInChildren<Brain>();
            switch (spec.driver)
            {
                case VelocityTraversalChooser.DriverTag:
                    var velocityChooser = new VelocityTraversalChooser();
                    velocityChooser.Configure(dir, spec.speedFraction * ship.Dynamics.maxSpeed);
                    brain.InstallChooser(velocityChooser);
                    break;
                case LegacyNavTraversalChooser.DriverTag:
                    var legacyChooser = new LegacyNavTraversalChooser();
                    // Waypoint a full crossing-radius past the exit: arrival deceleration stays outside the measured segment.
                    legacyChooser.Configure(destination + spec.crossingRadius * dir);
                    brain.InstallChooser(legacyChooser);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(spec), spec.driver, "Unknown traversal driver tag");
            }

            settingsClone.wVelTrack = spec.wVelTrack;
            // Respawn last: it re-creates the solver from the (possibly overridden) settings clone, re-inits the installed chooser, and restores health/pose.
            var facingDeg = Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
            unitService.RespawnShip(ship.Id, start, facingDeg);

            var runner = new TraversalRunner(ship, in spec, episodeIndex, start, dir);
            runner.Begin();
            var deadline = Time.realtimeSinceStartup + 120f + runner.MaxSimSeconds * 2f;
            while (!runner.IsDone && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
                runner.Tick();
            }
            Assert.IsTrue(runner.IsDone, "Crossing wall-clock deadline exceeded before termination");
            onComplete(runner.Result);
        }
    }
}
#endif
