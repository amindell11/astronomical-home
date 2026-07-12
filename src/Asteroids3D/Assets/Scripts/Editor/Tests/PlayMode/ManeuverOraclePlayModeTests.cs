#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using AI;
using Game;
using Game.RLHarness;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// PR-2a maneuver-oracle gate. Drives one ship-under-test through the real learner path
    /// (a scripted <see cref="ManeuverChooser"/> emitting VelocityReference intents, tracked by
    /// the ship's own AICommander/MPC loop) against a single dummy target, and measures whether a
    /// 2D velocity command can hold orbit / break / range maneuvers at a swept wVelTrack.
    ///
    /// Smoke is always-on (guards the harness). The full sweep is opt-in via ORACLE_SWEEP=1 and
    /// writes one JSONL row per (maneuver x wVelTrack) to results/maneuver-oracle/ for a human read.
    /// </summary>
    [TestFixture]
    [Category("ManeuverOracle")]
    public class ManeuverOraclePlayModeTests
    {
        private ShipRegistry registry;
        private readonly List<GameObject> createdObjects = new();
        private readonly List<UnityEngine.Object> createdAssets = new();
        private float savedTimeScale;
        private float savedMaxDelta;

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            registry = new ShipRegistry();

            savedTimeScale = Time.timeScale;
            savedMaxDelta = Time.maximumDeltaTime;
            Time.timeScale = 20f;
            Time.maximumDeltaTime = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = savedTimeScale;
            Time.maximumDeltaTime = savedMaxDelta;

            registry?.Dispose();
            registry = null;

            foreach (var go in createdObjects)
                if (go) UnityEngine.Object.DestroyImmediate(go);
            createdObjects.Clear();

            foreach (var asset in createdAssets)
                if (asset) UnityEngine.Object.DestroyImmediate(asset);
            createdAssets.Clear();

            AudioListener.pause = false;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Smoke_Orbit_ProducesMetrics()
        {
            OracleRunResult result = default;
            yield return RunManeuver(new OracleRunConfig
            {
                maneuver = ManeuverKind.Orbit,
                wVelTrack = 50f,
                orbitRadius = 15f,
                desiredRange = 15f,
                orbitSpeedFraction = 0.9f,
                aimRateDegPerSec = 0f,
                useField = false,
                duration = 6f,
                tag = "smoke",
            }, r => result = r);

            Assert.AreEqual(OracleRunResult.SchemaId, result.schema);
            Assert.Greater(result.duration, 5.5f, "Episode did not run its configured sim duration");
            Assert.Greater(result.meanSpeed, 0.1f, "Ship never moved — chooser/MPC wiring broken?");
            Assert.Greater(result.radiusSampleCount, 0, "No post-settle radius samples were taken");
            Assert.AreNotEqual(0f, result.angularProgressRad, "LOS angle never changed — ship is inert");
        }

        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator Sweep_WritesJsonl()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ORACLE_SWEEP")))
                Assert.Ignore("Set ORACLE_SWEEP=1 to run the maneuver-oracle sweep.");

            var tag = Environment.GetEnvironmentVariable("ORACLE_SWEEP_TAG") ?? "run";
            var duration = EnvFloat("ORACLE_SWEEP_DURATION", 12f);
            var maneuvers = new[] { ManeuverKind.Orbit, ManeuverKind.Break, ManeuverKind.Range };
            var wVelTracks = new[] { 5f, 20f, 50f, 100f };

            var results = new List<OracleRunResult>();
            foreach (var maneuver in maneuvers)
            foreach (var w in wVelTracks)
            {
                OracleRunResult result = default;
                yield return RunManeuver(new OracleRunConfig
                {
                    maneuver = maneuver,
                    wVelTrack = w,
                    orbitRadius = 15f,
                    desiredRange = 20f,
                    orbitSpeedFraction = 0.9f,
                    aimRateDegPerSec = maneuver == ManeuverKind.Break ? 30f : 0f,
                    useField = false,
                    duration = duration,
                    tag = tag,
                }, r => result = r);
                results.Add(result);
            }

            WriteJsonl(tag, results);
        }

        /// <summary>
        /// Localizing probe (opt-in ORACLE_PROBE=1) for the first-pass orbit/break failure. Separates
        /// "the hand-authored command was too aggressive" (gentler speed x radius, nose-on-target aim)
        /// from "the ceiling is aim-while-strafe" (free-yaw: nose tracks velocity so forward thrust
        /// curves the path). wVelTrack pinned at 20 (orbit worsened with higher weight in the sweep).
        /// </summary>
        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator Probe_LocalizeOrbit_WritesJsonl()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ORACLE_PROBE")))
                Assert.Ignore("Set ORACLE_PROBE=1 to run the orbit/break localizing probe.");

            const float wVel = 20f;
            const float probeDuration = 24f;
            var results = new List<OracleRunResult>();

            // A — gentler nose-on-target orbit: is any aimed circle-strafe feasible, or only the aggressive one?
            foreach (var frac in new[] { 0.3f, 0.5f })
            foreach (var radius in new[] { 15f, 30f })
            {
                OracleRunResult r = default;
                yield return RunManeuver(new OracleRunConfig
                {
                    maneuver = ManeuverKind.Orbit, wVelTrack = wVel, orbitRadius = radius,
                    desiredRange = radius, orbitSpeedFraction = frac, freeYaw = false,
                    aimRateDegPerSec = 0f, useField = false, duration = probeDuration, tag = "probe-aim",
                }, x => r = x);
                results.Add(r);
            }

            // B — free-yaw orbit (nose follows velocity → forward thrust curves the path): does dropping the aim constraint unblock it?
            foreach (var frac in new[] { 0.5f, 0.9f })
            {
                OracleRunResult r = default;
                yield return RunManeuver(new OracleRunConfig
                {
                    maneuver = ManeuverKind.Orbit, wVelTrack = wVel, orbitRadius = 15f,
                    desiredRange = 15f, orbitSpeedFraction = frac, freeYaw = true,
                    aimRateDegPerSec = 0f, useField = false, duration = probeDuration, tag = "probe-freeyaw",
                }, x => r = x);
                results.Add(r);
            }

            // C — free-yaw break vs the same 30 deg/s aimer: can nose-follows-velocity out-turn it?
            {
                OracleRunResult r = default;
                yield return RunManeuver(new OracleRunConfig
                {
                    maneuver = ManeuverKind.Break, wVelTrack = wVel, orbitRadius = 15f,
                    desiredRange = 20f, orbitSpeedFraction = 0.9f, freeYaw = true,
                    aimRateDegPerSec = 30f, useField = false, duration = 12f, tag = "probe-freeyaw",
                }, x => r = x);
                results.Add(r);
            }

            WriteJsonl("probe", results);
        }

        private IEnumerator RunManeuver(OracleRunConfig cfg, Action<OracleRunResult> onComplete)
        {
            var startPlane = StartPlanePosition(cfg);
            var shipWorld = GamePlane.PlanePointToWorld(startPlane);

            var ship = ShipTestFactory.CreateDefaultShipAt(shipWorld, GamePlane.Rotation);
            Assert.IsNotNull(ship, "Failed to create ship — check test asset paths");
            createdObjects.Add(ship.gameObject);
            registry.ActiveShips.Add(ship);

            var cmdr = ship.GetComponentInChildren<AICommander>();
            Assert.IsNotNull(cmdr, "Ship missing AICommander");
            var nav = cmdr.Navigator;
            Assert.IsNotNull(nav, "AICommander has no Navigator");

            var baseSettings = nav.mpcSettings ? nav.mpcSettings : ScriptableObject.CreateInstance<MpcSettings>();
            var settings = UnityEngine.Object.Instantiate(baseSettings);
            settings.wVelTrack = cfg.wVelTrack;
            nav.mpcSettings = settings;
            createdAssets.Add(settings);

            var dummy = BuildDummy(cfg, ship.transform);

            var brain = cmdr.GetComponentInChildren<Brain>();
            Assert.IsNotNull(brain, "AICommander has no Brain");
            var chooser = new ManeuverChooser();
            chooser.Configure(dummy, cfg.maneuver, cfg.orbitRadius, cfg.desiredRange,
                cfg.orbitSpeedFraction, cfg.freeYaw);
            typeof(Brain).GetField("chooser", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(brain, chooser);

            cmdr.SetRegistry(registry);

            var metrics = new OracleMetrics(cfg);
            var deadline = Time.realtimeSinceStartup + 60f + cfg.duration * 10f;
            var elapsed = 0f;
            while (elapsed < cfg.duration && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
                var dt = Time.fixedDeltaTime;
                elapsed += dt;
                var k = ship.Kinematics;
                metrics.Sample(k.pos, k.vel, dummy.PlanePosition, dummy.PlaneForward, dt);
            }

            onComplete(metrics.Finalize());

            registry.ActiveShips.Remove(ship);
            UnityEngine.Object.DestroyImmediate(dummy.gameObject);
            UnityEngine.Object.DestroyImmediate(ship.gameObject);
        }

        private DummyTarget BuildDummy(OracleRunConfig cfg, Transform shipTransform)
        {
            var go = new GameObject("DummyTarget");
            createdObjects.Add(go);
            var dummy = go.AddComponent<DummyTarget>();

            var aim = cfg.maneuver == ManeuverKind.Break;
            var initialForward = (StartPlanePosition(cfg) - Vector2.zero).normalized;
            dummy.Configure(Vector2.zero, initialForward, aim, cfg.aimRateDegPerSec);
            if (aim) dummy.SetAimTarget(shipTransform);
            return dummy;
        }

        private static Vector2 StartPlanePosition(OracleRunConfig cfg) => cfg.maneuver switch
        {
            ManeuverKind.Orbit => new Vector2(0.8f * cfg.orbitRadius, 0f),
            ManeuverKind.Range => new Vector2(1.6f * cfg.desiredRange, 0f),
            ManeuverKind.Break => new Vector2(15f, 0f),
            _ => new Vector2(15f, 0f),
        };

        private static string ResultsDir()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            var dir = Path.Combine(repoRoot, "results", "maneuver-oracle");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void WriteJsonl(string tag, List<OracleRunResult> results)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(ResultsDir(), $"{stamp}-{tag}.jsonl");
            using (var w = new StreamWriter(path, false))
                foreach (var r in results)
                    w.WriteLine(r.ToJsonLine());
            Debug.Log($"[ManeuverOracle] wrote {results.Count} rows to {path}");
        }

        private static float EnvFloat(string name, float fallback)
        {
            var v = Environment.GetEnvironmentVariable(name);
            return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;
        }
    }
}
#endif
