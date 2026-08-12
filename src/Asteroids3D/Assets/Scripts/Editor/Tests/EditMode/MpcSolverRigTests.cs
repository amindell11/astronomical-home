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

        // Characterization pin, not a quality gate: the on-target yaw churn the retune pass is
        // hunting (Bench-1 strict torque reversals ~11/s) must reproduce on the rig's own plant.
        // A controller redesign that calms the loop SHOULD fail this test — update the pin then.
        [Test]
        public void Run_VersusDummy_ReproducesYawChurnSignature()
        {
            var scenario = RigScenario.VersusDummy(40f);
            var result = MpcSolverRig.Run(settings, dynamics, in scenario, 1234u);

            Assert.That(result.steps, Is.EqualTo(1000));
            Assert.That(result.torqueReversalsPerSec, Is.GreaterThan(2f),
                "Expected the self-generated yaw churn signature versus a stationary Dummy " +
                $"(bench strict ~11/s); measured {result.torqueReversalsPerSec:F2}/s. " +
                "If a controller redesign legitimately calmed the loop, update this pin.");
            Assert.That(result.meanFacingErrorDeg, Is.LessThan(90f),
                "The controller should at least broadly track the anchor while churning; " +
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

        [Test]
        public void Run_SelectionVariants_RunAndReplayDeterministically(
            [Values(MpcSelectionMode.Argmin, MpcSelectionMode.IncumbentElite)] MpcSelectionMode mode)
        {
            var variant = Object.Instantiate(settings);
            try
            {
                variant.selectionMode = mode;
                var scenario = ShortScenario();
                var first = new List<RigTraceRow>();
                var second = new List<RigTraceRow>();
                var result = MpcSolverRig.Run(variant, dynamics, in scenario, 1234u, first);
                MpcSolverRig.Run(variant, dynamics, in scenario, 1234u, second);

                Assert.That(result.steps, Is.GreaterThan(0));
                Assert.That(float.IsFinite(result.meanFacingErrorDeg), $"{mode} produced a non-finite facing error.");
                Assert.That(second.Count, Is.EqualTo(first.Count));
                for (var i = 0; i < first.Count; i++)
                    Assert.That(second[i].yawTorque, Is.EqualTo(first[i].yawTorque),
                        $"{mode} command diverged at step {i}: a fixed seed must replay the closed loop bit-for-bit.");
            }
            finally
            {
                Object.DestroyImmediate(variant);
            }
        }

        // Probe 2 entry point: one summary line per selection mode x seed, plus full traces for offline
        // spectra. Env-gated like Run_EmitTraceArtifact.
        [Test]
        public void Run_EmitSelectionProbeArtifacts()
        {
            if (System.Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1")
                Assert.Ignore("Set MPC_RIG_EMIT=1 to emit the Probe-2 selection artifacts.");

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/mpc-rig/probe2"));
            Directory.CreateDirectory(outDir);

            foreach (var mode in new[] { MpcSelectionMode.EliteAverage, MpcSelectionMode.Argmin, MpcSelectionMode.IncumbentElite })
            {
                var variant = Object.Instantiate(settings);
                try
                {
                    variant.selectionMode = mode;
                    foreach (var startErrorDeg in new[] { 0f, 90f })
                    foreach (var seed in new uint[] { 1234u, 99u, 7u })
                    {
                        var scenario = RigScenario.VersusDummy(40f, startErrorDeg);
                        var trace = new List<RigTraceRow>();
                        var result = MpcSolverRig.Run(variant, dynamics, in scenario, seed, trace);
                        RigTraceCsv.Write(Path.Combine(outDir, $"trace-dummy-{mode}-err{startErrorDeg:F0}-seed{seed}.csv"), trace);
                        Debug.Log($"[Probe2] {mode} err{startErrorDeg:F0} seed {seed} | strict {result.torqueReversalsPerSec:F2}/s | " +
                                  $"deadband {result.torqueDeadbandReversalsPerSec:F2}/s | " +
                                  $"|yawRate| {result.meanAbsYawRateDegPerSec:F1} deg/s | " +
                                  $"facing err {result.meanFacingErrorDeg:F1} deg (p90 {result.p90FacingErrorDeg:F1}) | " +
                                  $"range {result.finalRange:F1} | incumbent wins {result.incumbentWinFraction:P1} | " +
                                  $"mean rank {result.meanIncumbentRank:F1} | " +
                                  $"|emit-incumbent yaw| {result.meanAbsEmitYawDeltaFromIncumbent:F3}");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(variant);
                }
            }
        }

        // Investigation entry point: emits a full trace for offline plotting; the investigation
        // owns deleting its artifacts. Env-gated because the batch runner never executes [Explicit].
        [Test]
        public void Run_EmitTraceArtifact()
        {
            if (System.Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1")
                Assert.Ignore("Set MPC_RIG_EMIT=1 to emit the investigation trace artifact.");

            var scenario = RigScenario.VersusDummy(40f);
            var trace = new List<RigTraceRow>();
            var result = MpcSolverRig.Run(settings, dynamics, in scenario, 1234u, trace);

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/mpc-rig"));
            Directory.CreateDirectory(outDir);
            var path = Path.Combine(outDir, "trace-dummy-seed1234.csv");
            RigTraceCsv.Write(path, trace);
            Debug.Log($"[MpcSolverRig] {path} | strict {result.torqueReversalsPerSec:F2}/s | " +
                      $"deadband {result.torqueDeadbandReversalsPerSec:F2}/s | " +
                      $"|yawRate| {result.meanAbsYawRateDegPerSec:F1} deg/s | " +
                      $"facing err {result.meanFacingErrorDeg:F1} deg | range {result.finalRange:F1}");
        }
    }
}
#endif
