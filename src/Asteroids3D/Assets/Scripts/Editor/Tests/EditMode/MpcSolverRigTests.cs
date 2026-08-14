#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Game.RLHarness;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Unity.Mathematics;
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
                Assert.That(lines[0], Does.Contain("costPos").And.Contain("costObstacle").And.Contain("costTotal"),
                    "The per-term breakdown columns are the trace's diagnostic payload.");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void SyntheticObstacles_EnterThroughConvertObstacles()
        {
            var scenario = RigScenario.VersusDummy(40f);
            scenario.obstacles = new[] { new RigCircle(new float2(0f, 0f), 5f) };
            scenario.warmupSeconds = 0f;
            scenario.durationSeconds = 0.1f;

            var trace = new List<RigTraceRow>();
            MpcSolverRig.Run(settings, dynamics, in scenario, 1234u, trace);

            Assert.That(trace[0].costCollision, Is.GreaterThan(0f),
                "An overlapped hull pays the collision penalty through the production obstacle path.");
            Assert.That(trace[0].underThreat, Is.EqualTo(1),
                "The threat classifier mirrors Cost.ObstacleCosts' branches.");
        }

        [Test]
        public void BingoCard_AuthorsFourteenRows()
        {
            var rows = RigBingoCard.Rows();
            Assert.That(rows.Length, Is.EqualTo(14));

            var names = new HashSet<string>();
            foreach (var row in rows)
            {
                Assert.That(names.Add(row.name), $"Duplicate row name '{row.name}'.");
                Assert.That(row.scenario.intent.AnyArmed,
                    $"Row '{row.name}' authors no armed slot — an absent sentence is not a row.");
                Assert.That(row.scenario.durationSeconds, Is.GreaterThan(0f));
            }

            var movement = new HashSet<string>();
            foreach (var row in rows)
                if (row.movement) movement.Add(row.name);
            Assert.That(movement, Is.EquivalentTo(new[] { "orbit", "kite", "missile-drag" }),
                "The VEL-zeroed protocol covers exactly the card's movement rows (§Staging).");

            var velZeroed = RigBingoCard.VelZeroed(rows[0].scenario);
            Assert.That(velZeroed.intent.vel.armed, "VEL-zeroed keeps the slot armed; only authority drops.");
            Assert.That(velZeroed.intent.vel.weight, Is.Zero);

            var fieldZeroed = RigBingoCard.FieldZeroed(FindRow("field-authority").scenario);
            Assert.That(fieldZeroed.intent.field.armed, "FIELD-zeroed keeps the slot armed; only authority drops.");
            Assert.That(fieldZeroed.intent.field.weight, Is.Zero);
        }

        private static BingoRow FindRow(string name)
        {
            foreach (var row in RigBingoCard.Rows())
                if (row.name == name) return row;
            throw new AssertionException($"No bingo row named '{name}'.");
        }

        // ---- Stage C1 composition proofs: the named rows compose on stock asset values ----

        [Test]
        public void Bingo_MinefieldTransit_ComposesWithoutPosWidthOverride()
        {
            var row = FindRow("minefield-transit");
            Assert.That(row.scenario.posWidthOverride, Is.Zero,
                "the hand-tuned override is retired: error-relative width must carry the 90 m reach");

            // The from-rest transit is chaotically marginal under incumbent-elite selection — the
            // Stage B constant-60 override lands 4/5 on these same seeds (measured 2026-08-13, PR
            // #419 build), so a per-seed pin would gate on basin luck, not the width law. The bar
            // is the measured majority; a regression below it is real.
            var seeds = new uint[] { 1234u, 7u, 99u, 2001u, 2002u };
            var transits = 0;
            var report = "";
            foreach (var seed in seeds)
            {
                var result = MpcSolverRig.Run(settings, dynamics, in row.scenario, seed);
                if (result.finalRange < 20f) transits++;
                report += $" seed {seed}: {result.finalRange:F1} m;";
            }
            Assert.That(transits, Is.GreaterThanOrEqualTo(3),
                $"the transit must complete on stock asset values on most draws;{report}");
        }

        [Test]
        public void Bingo_WingmanHold_ComposesWithoutPosWidthOverride()
        {
            var row = FindRow("wingman-hold");
            Assert.That(row.scenario.posWidthOverride, Is.Zero,
                "the hand-tuned override is retired: the asset posWidth is the settle floor");
            var result = MpcSolverRig.Run(settings, dynamics, in row.scenario, 1234u);
            Assert.That(result.finalRange, Is.InRange(6f, 18f),
                $"on station means ~12 m off the ally's wing; final range {result.finalRange:F1} m.");
        }

        [Test]
        public void Bingo_FireLaneDodge_ExitsTheLane()
        {
            var row = FindRow("fire-lane-dodge");
            var trace = new List<RigTraceRow>();
            MpcSolverRig.Run(settings, dynamics, in row.scenario, 1234u, trace);

            // The enemy is static at (0,50) facing the spawn: its lane is the segment (0,50) → (0,50−laneRange).
            var start = new float2(0f, 50f);
            var end = new float2(0f, 50f - settings.laneRange);
            var tail = trace.Count * 3 / 4;
            var meanTailDistance = 0f;
            for (var i = tail; i < trace.Count; i++)
                meanTailDistance += SegmentDistance(new float2(trace[i].posX, trace[i].posY), start, end);
            meanTailDistance /= trace.Count - tail;

            Assert.That(meanTailDistance, Is.GreaterThan(settings.laneWidth),
                $"a dodged lane means settling beyond laneWidth of it; mean tail distance {meanTailDistance:F1} m.");
        }

        [Test]
        public void Bingo_FieldAuthority_ZeroVersusOne_MeasurablyDiverges()
        {
            var row = FindRow("field-authority");
            var full = new List<RigTraceRow>();
            var zeroed = new List<RigTraceRow>();
            var fullResult = MpcSolverRig.Run(settings, dynamics, in row.scenario, 1234u, full);
            var fieldZero = RigBingoCard.FieldZeroed(row.scenario);
            var zeroedResult = MpcSolverRig.Run(settings, dynamics, in fieldZero, 1234u, zeroed);

            Assert.That(fullResult.threatStepFraction, Is.GreaterThan(0f),
                "fixture validity: the spawn-at-speed head-on rock must force threat states on the applied path");
            Assert.That(zeroedResult.threatStepFraction, Is.GreaterThan(0f),
                "fixture validity: the FIELD-zeroed arm must face the same forced approach");

            var steps = Mathf.Min(full.Count, zeroed.Count);
            var meanDivergence = 0f;
            for (var i = 0; i < steps; i++)
                meanDivergence += math.distance(new float2(full[i].posX, full[i].posY),
                    new float2(zeroed[i].posX, zeroed[i].posY));
            meanDivergence /= steps;

            // Pre-registered failure rule (§Stage C): no demonstrable differential authority is a
            // design event surfaced by this assert — never a silent ship. Measured 2026-08-13:
            // divergence 0.9-2.3 m across seeds, but the clearance ordering F1-vs-F0 flips sign —
            // trajectory divergence is demonstrable, a systematic wider-berth effect is not
            // (recorded in PR #419 for the user's ruling).
            Assert.That(meanDivergence, Is.GreaterThan(0.5f),
                $"FIELD 0 vs 1 must measurably diverge; mean trajectory divergence {meanDivergence:F2} m.");
        }

        private static float SegmentDistance(float2 pos, float2 start, float2 end)
        {
            var seg = end - start;
            var t = math.saturate(math.dot(pos - start, seg) / math.max(math.lengthsq(seg), 1e-6f));
            return math.distance(pos, start + t * seg);
        }

        // Bingo rows run off-gate (MPC_RIG_EMIT precedent): the acceptance's same-seed replay
        // proof runs on shortened copies so the full emitter isn't doubled.
        [Test]
        public void Bingo_SameSeedReplayHolds()
        {
            if (System.Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1")
                Assert.Ignore("Set MPC_RIG_EMIT=1 to run the bingo replay proof.");

            foreach (var (name, scenario) in BingoVariants())
            {
                var shortened = scenario;
                shortened.warmupSeconds = 1f;
                shortened.durationSeconds = 4f;

                var first = new List<RigTraceRow>();
                var second = new List<RigTraceRow>();
                MpcSolverRig.Run(settings, dynamics, in shortened, 1234u, first);
                MpcSolverRig.Run(settings, dynamics, in shortened, 1234u, second);

                Assert.That(second.Count, Is.EqualTo(first.Count), $"{name}: step counts diverged.");
                for (var i = 0; i < first.Count; i++)
                {
                    Assert.That(second[i].yawTorque, Is.EqualTo(first[i].yawTorque),
                        $"{name}: command diverged at step {i} — same-seed replay must hold on every row.");
                    Assert.That(second[i].yawDeg, Is.EqualTo(first[i].yawDeg),
                        $"{name}: plant state diverged at step {i}.");
                }
            }
        }

        [Test]
        public void Bingo_EmitRowArtifacts()
        {
            if (System.Environment.GetEnvironmentVariable("MPC_RIG_EMIT") != "1")
                Assert.Ignore("Set MPC_RIG_EMIT=1 to emit the bingo trace artifacts.");

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/mpc-rig/bingo"));
            Directory.CreateDirectory(outDir);

            foreach (var (name, scenario) in BingoVariants())
            {
                var trace = new List<RigTraceRow>();
                var result = MpcSolverRig.Run(settings, dynamics, in scenario, 1234u, trace);
                RigTraceCsv.Write(Path.Combine(outDir, $"{name}.csv"), trace);
                Debug.Log($"[Bingo] {name} | strict {result.torqueReversalsPerSec:F2}/s | " +
                          $"deadband {result.torqueDeadbandReversalsPerSec:F2}/s | " +
                          $"|yawRate| {result.meanAbsYawRateDegPerSec:F1} deg/s | " +
                          $"facing err {result.meanFacingErrorDeg:F1} deg (p90 {result.p90FacingErrorDeg:F1}) | " +
                          $"range {result.finalRange:F1} | threat {result.threatStepFraction:P1} | " +
                          $"incumbent wins {result.incumbentWinFraction:P1}");
            }
        }

        /// <summary>All 13 rows plus the movement rows' VEL-zeroed protocol arms.</summary>
        private static IEnumerable<(string name, RigScenario scenario)> BingoVariants()
        {
            foreach (var row in RigBingoCard.Rows())
            {
                yield return (row.name, row.scenario);
                if (row.movement)
                    yield return (row.name + "-velzero", RigBingoCard.VelZeroed(row.scenario));
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
