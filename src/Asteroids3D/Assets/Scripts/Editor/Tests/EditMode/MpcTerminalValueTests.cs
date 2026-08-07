#if UNITY_EDITOR
using System;
using System.Diagnostics;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Unity.InferenceEngine;
using Unity.Mathematics;
using UnityEditor;

namespace Tests.EditMode
{
    [Category("MPC")]
    public class MpcTerminalValueTests
    {
        private const string SettingsPath = "Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset";
        private const string ShipPath = "Assets/Prefabs/Ships/Ship_1.prefab";
        private const string ZeroModelPath = "Assets/Tests/Fixtures/MpcTerminalValue-zero.onnx";
        private const string BenchmarkEnvironmentVariable = "MPC_TERMINAL_VALUE_BENCHMARK";

        private MpcSettings settings;
        private Dynamics dynamics;

        [SetUp]
        public void SetUp()
        {
            settings = AssetDatabase.LoadAssetAtPath<MpcSettings>(SettingsPath);
            dynamics = AssetDatabase.LoadAssetAtPath<Ship>(ShipPath).ResolveStats().Dynamics;
        }

        [Test]
        public void EvaluateCandidates_ExportsEachRolloutTerminalState()
        {
            using var mpc = new Mpc(settings, dynamics, 41u);
            var inputs = Inputs();
            mpc.Plan(in inputs);

            var solver = mpc.Solver;
            for (var candidate = 0; candidate < solver.LastSampleCount; candidate++)
            {
                var expected = mpc.LastInitialState;
                var offset = candidate * solver.LastHorizon;
                for (var step = 0; step < solver.LastHorizon; step++)
                    expected = Movement.MPC.Model.Step(
                        expected, solver.Candidates[offset + step], mpc.Config, dynamics);

                AssertState(expected, solver.TerminalStates[candidate], candidate);
            }
        }

        [Test]
        public void ZeroSentisScorer_PreservesCostsAndChosenControls()
        {
            var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(ZeroModelPath);
            Assert.That(model, Is.Not.Null, $"Missing zero-value model at {ZeroModelPath}");

            using var scorer = new SentisTerminalValueScorer(model);
            using var baseline = new Mpc(settings, dynamics, 73u);
            using var scored = new Mpc(settings, dynamics, 73u, scorer);
            var inputs = Inputs();

            var baselineResult = baseline.Plan(in inputs);
            var scoredResult = scored.Plan(in inputs);

            Assert.That(scoredResult.cost, Is.EqualTo(baselineResult.cost));
            for (var i = 0; i < baseline.Solver.LastSampleCount; i++)
                Assert.That(scored.Solver.Costs[i], Is.EqualTo(baseline.Solver.Costs[i]),
                    $"candidate {i} cost/order changed");
            for (var i = 0; i < baseline.BestSequence.Length; i++)
                AssertControl(baseline.BestSequence[i], scored.BestSequence[i], i);
        }

        [Test, Category("Slow")]
        public void Benchmark_RepresentativeArenaCounts()
        {
            if (Environment.GetEnvironmentVariable(BenchmarkEnvironmentVariable) != "1")
                Assert.Ignore($"Set {BenchmarkEnvironmentVariable}=1 to run the latency kill test.");

            var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(ZeroModelPath);
            Assert.That(model, Is.Not.Null, $"Missing zero-value model at {ZeroModelPath}");

            using var scorer = new SentisTerminalValueScorer(model);
            foreach (var arenaCount in new[] { 1, 2 })
                Benchmark(arenaCount, scorer);
        }

        private void Benchmark(int arenaCount, ITerminalValueScorer scorer)
        {
            const int warmupFrames = 20;
            const int measuredFrames = 200;
            var shipCount = arenaCount * 2;
            var baseline = CreateFleet(shipCount, null);
            var scored = CreateFleet(shipCount, scorer);
            var inputs = Inputs();

            try
            {
                for (var i = 0; i < warmupFrames; i++)
                {
                    MeasureFrame(baseline, in inputs);
                    MeasureFrame(scored, in inputs);
                }

                var baselineMs = new double[measuredFrames];
                var scoredMs = new double[measuredFrames];
                var deltaMs = new double[measuredFrames];
                for (var i = 0; i < measuredFrames; i++)
                {
                    if ((i & 1) == 0)
                    {
                        baselineMs[i] = MeasureFrame(baseline, in inputs);
                        scoredMs[i] = MeasureFrame(scored, in inputs);
                    }
                    else
                    {
                        scoredMs[i] = MeasureFrame(scored, in inputs);
                        baselineMs[i] = MeasureFrame(baseline, in inputs);
                    }
                    deltaMs[i] = scoredMs[i] - baselineMs[i];
                }

                UnityEngine.Debug.Log(
                    $"[MPC terminal value] arenas={arenaCount} ships={shipCount} " +
                    $"samples={settings.samples} horizon={settings.Horizon} " +
                    $"baseline p50/p95={Percentile(baselineMs, 50):F3}/{Percentile(baselineMs, 95):F3}ms " +
                    $"scored p50/p95={Percentile(scoredMs, 50):F3}/{Percentile(scoredMs, 95):F3}ms " +
                    $"paired overhead p50/p95={Percentile(deltaMs, 50):F3}/{Percentile(deltaMs, 95):F3}ms " +
                    $"scored-p95 budget={Percentile(scoredMs, 95) / 20d:P1}");
            }
            finally
            {
                DisposeFleet(baseline);
                DisposeFleet(scored);
            }
        }

        private Mpc[] CreateFleet(int count, ITerminalValueScorer scorer)
        {
            var fleet = new Mpc[count];
            for (var i = 0; i < count; i++)
                fleet[i] = new Mpc(settings, dynamics, (uint)(1000 + i), scorer);
            return fleet;
        }

        private static double MeasureFrame(Mpc[] fleet, in MpcInputs inputs)
        {
            var start = Stopwatch.GetTimestamp();
            for (var i = 0; i < fleet.Length; i++)
                fleet[i].Plan(in inputs);
            return (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
        }

        private static double Percentile(double[] values, int percentile)
        {
            var sorted = (double[])values.Clone();
            Array.Sort(sorted);
            var index = Math.Max(0, (int)Math.Ceiling(percentile * sorted.Length / 100d) - 1);
            return sorted[index];
        }

        private static void DisposeFleet(Mpc[] fleet)
        {
            for (var i = 0; i < fleet.Length; i++)
                fleet[i].Dispose();
        }

        private static MpcInputs Inputs() => new()
        {
            velocityReference = new float2(6f, 3f),
            facingRad = float.NaN,
            enemyYaw = float.NaN,
            weightOverrides = Array.Empty<WeightOverride>(),
            enableObstacleAvoidance = false,
        };

        private static void AssertState(State expected, State actual, int candidate)
        {
            const float tolerance = 1e-5f;
            Assert.That(actual.pos.x, Is.EqualTo(expected.pos.x).Within(tolerance), $"candidate {candidate} pos.x");
            Assert.That(actual.pos.y, Is.EqualTo(expected.pos.y).Within(tolerance), $"candidate {candidate} pos.y");
            Assert.That(actual.vel.x, Is.EqualTo(expected.vel.x).Within(tolerance), $"candidate {candidate} vel.x");
            Assert.That(actual.vel.y, Is.EqualTo(expected.vel.y).Within(tolerance), $"candidate {candidate} vel.y");
            Assert.That(actual.yaw, Is.EqualTo(expected.yaw).Within(tolerance), $"candidate {candidate} yaw");
            Assert.That(actual.yawRate, Is.EqualTo(expected.yawRate).Within(tolerance), $"candidate {candidate} yaw rate");
            Assert.That(actual.boostCooldownRemaining,
                Is.EqualTo(expected.boostCooldownRemaining).Within(tolerance), $"candidate {candidate} boost cooldown");
        }

        private static void AssertControl(Control expected, Control actual, int step)
        {
            Assert.That(actual.thrust, Is.EqualTo(expected.thrust), $"step {step} thrust");
            Assert.That(actual.strafe, Is.EqualTo(expected.strafe), $"step {step} strafe");
            Assert.That(actual.yawTorque, Is.EqualTo(expected.yawTorque), $"step {step} yaw torque");
            Assert.That(actual.boost, Is.EqualTo(expected.boost), $"step {step} boost");
        }
    }
}
#endif
