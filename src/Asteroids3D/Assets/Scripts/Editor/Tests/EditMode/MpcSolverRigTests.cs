#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Game.RLHarness;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Runs the production MpcSettings asset and Ship_1 dynamics so churn pins characterize the shipped controller.</summary>
    [Category("MPC")]
    public class MpcSolverRigTests
    {
        private const string MpcSettingsPath = "Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset";
        private const string ShipPrefabPath = "Assets/Prefabs/Ships/Ship_1.prefab";

        private MpcSettings settings;
        private Dynamics dynamics;

        [SetUp]
        public void SetUp()
        {
            settings = AssetDatabase.LoadAssetAtPath<MpcSettings>(MpcSettingsPath);
            var ship = AssetDatabase.LoadAssetAtPath<Ship>(ShipPrefabPath);
            Assert.That(settings, Is.Not.Null, $"Missing MPC settings at {MpcSettingsPath}");
            Assert.That(ship, Is.Not.Null, $"Missing ship prefab at {ShipPrefabPath}");
            dynamics = ship.ResolveStats().Dynamics;
        }

        private static RigScenario ShortScenario()
        {
            var scenario = RigScenario.VersusDummy(40f);
            scenario.warmupSeconds = 1f;
            scenario.durationSeconds = 4f;
            return scenario;
        }

        [Test]
        public void Run_SameSeed_ReplaysIdenticalTrace()
        {
            var scenario = ShortScenario();
            var first = new List<RigTraceRow>();
            var second = new List<RigTraceRow>();
            MpcSolverRig.Run(settings, dynamics, in scenario, 1234u, first);
            MpcSolverRig.Run(settings, dynamics, in scenario, 1234u, second);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (var i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].yawTorque, Is.EqualTo(first[i].yawTorque),
                    $"Command diverged at step {i}: a fixed seed must replay the closed loop bit-for-bit.");
                Assert.That(second[i].yawDeg, Is.EqualTo(first[i].yawDeg),
                    $"Plant state diverged at step {i}.");
            }
        }

        // Characterization pins for the settled controller (Probe 2 ruling: incumbent-elite
        // selection + fractional shift are the only paths). Successor of the retired churn pin
        // Run_VersusDummy_ReproducesYawChurnSignature; a redesign that changes the loop updates these.
        [Test]
        public void Run_VersusDummy_OnTarget_HoldsTheFixedPointInertly()
        {
            var scenario = RigScenario.VersusDummy(40f);
            var result = MpcSolverRig.Run(settings, dynamics, in scenario, 1234u);

            Assert.That(result.steps, Is.EqualTo(1000));
            Assert.That(result.torqueReversalsPerSec, Is.LessThan(0.5f),
                "The on-target start is the settled fixed point; the incumbent must persist " +
                $"unperturbed. Measured {result.torqueReversalsPerSec:F2} reversals/s.");
            Assert.That(result.meanFacingErrorDeg, Is.LessThan(1f),
                $"Settled on-target hold should not wander; measured {result.meanFacingErrorDeg:F1} deg.");
        }

        [Test]
        public void Run_VersusDummy_OffTarget_ConvergesWithinHullRate()
        {
            var scenario = RigScenario.VersusDummy(40f, startFacingErrorDeg: 90f);
            var result = MpcSolverRig.Run(settings, dynamics, in scenario, 1234u);

            Assert.That(result.steps, Is.EqualTo(1000));
            Assert.That(result.torqueReversalsPerSec, Is.LessThan(6f),
                "Converged means reversals at or under the hull's own 4-5/s (ruling 3); " +
                $"measured {result.torqueReversalsPerSec:F2}/s (rig baseline 3.4-3.9).");
            Assert.That(result.meanFacingErrorDeg, Is.LessThan(15f),
                "The nose should track the anchor after the transient; " +
                $"measured mean facing error {result.meanFacingErrorDeg:F1} deg.");
            Assert.That(result.finalRange, Is.InRange(5f, 120f),
                $"Hold-at-range intent should keep the ship near the anchor; final range {result.finalRange:F1}.");
        }

        [Test]
        public void Trace_WritesCsvRowPerTick()
        {
            var scenario = ShortScenario();
            var trace = new List<RigTraceRow>();
            MpcSolverRig.Run(settings, dynamics, in scenario, 42u, trace);

            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
            try
            {
                RigTraceCsv.Write(path, trace);
                var lines = File.ReadAllLines(path);
                Assert.That(lines.Length, Is.EqualTo(trace.Count + 1), "Header plus one line per tick.");
                Assert.That(lines[0], Does.StartWith("t,posX"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        // Investigation entry point: emits full traces for offline plotting; the investigation
        // owns deleting its artifacts. Env-gated because the batch runner never executes [Explicit].
        [Test]
        public void Run_EmitTraceArtifact()
        {
            if (System.Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1")
                Assert.Ignore("Set MPC_RIG_EMIT=1 to emit the investigation trace artifact.");

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/mpc-rig"));
            Directory.CreateDirectory(outDir);

            foreach (var startErrorDeg in new[] { 0f, 90f })
            foreach (var seed in new uint[] { 1234u, 99u, 7u })
            {
                var scenario = RigScenario.VersusDummy(40f, startErrorDeg);
                var trace = new List<RigTraceRow>();
                var result = MpcSolverRig.Run(settings, dynamics, in scenario, seed, trace);
                var path = Path.Combine(outDir, $"trace-dummy-err{startErrorDeg:F0}-seed{seed}.csv");
                RigTraceCsv.Write(path, trace);
                Debug.Log($"[MpcSolverRig] err{startErrorDeg:F0} seed {seed} | strict {result.torqueReversalsPerSec:F2}/s | " +
                          $"deadband {result.torqueDeadbandReversalsPerSec:F2}/s | " +
                          $"|yawRate| {result.meanAbsYawRateDegPerSec:F1} deg/s | " +
                          $"facing err {result.meanFacingErrorDeg:F1} deg (p90 {result.p90FacingErrorDeg:F1}) | " +
                          $"range {result.finalRange:F1} | incumbent wins {result.incumbentWinFraction:P1} | " +
                          $"|emit-incumbent yaw| {result.meanAbsEmitYawDeltaFromIncumbent:F3}");
            }
        }
    }
}
#endif
