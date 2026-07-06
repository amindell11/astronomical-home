#if UNITY_EDITOR
using AI.Scanning;
using Game;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Gap-primitive injection in a contrived narrow (bank-only) gap: a straight rollout collides
    /// (large penalty) while the synthesized bank primitive threads (far cheaper), and injecting it
    /// into the CEM candidate set lowers the solver's best-elite cost.
    /// </summary>
    [Category("MPC")]
    public class MpcGapInjectionEditModeTests
    {
        private const string MpcSettingsPath = "Assets/Settings/AI/MPC/MpcSettings.asset";
        private const string ShipSettingsPath = "Assets/Settings/Ships/DefaultSettings.asset";

        private MpcSettings settings;
        private Dynamics dynamics;
        private Config cfg;
        private bool configuredHere;

        [SetUp]
        public void SetUp()
        {
            if (!GamePlane.IsConfigured) { GamePlane.Configure(PlaneAxis.Y); configuredHere = true; }
            settings = AssetDatabase.LoadAssetAtPath<MpcSettings>(MpcSettingsPath);
            var ship = AssetDatabase.LoadAssetAtPath<ShipSettings>(ShipSettingsPath);
            Assert.That(settings, Is.Not.Null);
            Assert.That(ship, Is.Not.Null);
            dynamics = ship.Dynamics;
            cfg = settings.ToConfig();
            Mpc.ApplyDynamicsTo(ref cfg, dynamics);
        }

        [TearDown]
        public void TearDown()
        {
            if (configuredHere) { GamePlane.Reset(); configuredHere = false; }
        }

        // Linear width 3.0: passable banked (hull 1.147) but a straight hull (1.4) collides at
        // safetyMargin 0.3 — a bank-only gap between two discs 12 ahead.
        private static ObstacleScan NarrowGapScan() =>
            new(new[]
            {
                new DetectedObstacle(new Vector3(-2.5f, 0f, 12f), 1f, null),
                new DetectedObstacle(new Vector3(2.5f, 0f, 12f), 1f, null),
            }, 2);

        private static State Initial() =>
            new() { pos = float2.zero, vel = new float2(0f, 10f), yaw = 0f, yawRate = 0f };

        private (Gap gap, Control[] prims, int count) DetectAndSynthesize(ObstacleScan scan)
        {
            var initial = Initial();
            var gaps = new Gap[3];
            var n = new GapDetector().Detect(initial.pos, new float2(0f, 1f), scan,
                dynamics.shipRadius, dynamics.maxBankAngleRad, settings.obstacleSafetyMargin, 40f, gaps, gaps.Length);
            Assert.That(n, Is.GreaterThanOrEqualTo(1), "the narrow gap should be detected");
            Assert.That(gaps[0].classification, Is.EqualTo(GapClass.BankOnly), "gap should require banking");

            var prims = new Control[GapPrimitives.MaxVariants * cfg.horizon];
            var count = GapPrimitives.Synthesize(initial, gaps[0], cfg, dynamics, prims, GapPrimitives.MaxVariants, cfg.horizon);
            Assert.That(count, Is.GreaterThan(0));
            return (gaps[0], prims, count);
        }

        private float RolloutCost(Control[] flat, int primIndex, State initial, CostInput input)
        {
            var total = 0f;
            var current = initial;
            var prevU = new Control();
            for (var i = 0; i < cfg.horizon; i++)
            {
                var u = flat[primIndex * cfg.horizon + i];
                total += Cost.Evaluate(current, u, prevU, input, cfg, i == cfg.horizon - 1, i);
                current = Model.Step(current, u, cfg, dynamics);
                prevU = u;
            }
            return total;
        }

        [Test]
        public void BankPrimitive_ThreadsGap_WhereStraightRolloutCollides()
        {
            var scan = NarrowGapScan();
            var (_, prims, count) = DetectAndSynthesize(scan);

            var obstacles = new NativeArray<ObstacleData>(scan.count, Allocator.Temp);
            try
            {
                for (var i = 0; i < scan.count; i++)
                    obstacles[i] = new ObstacleData
                    {
                        position = new float2(scan.buffer[i].position.x, scan.buffer[i].position.y),
                        radius = scan.buffer[i].radius,
                        weight = 1f,
                    };
                var input = new CostInput
                {
                    goalPos = new float2(0f, 30f),
                    obstacles = obstacles,
                    obstacleCount = scan.count,
                    enemyYaw = float.NaN,
                    initialVel = Initial().vel,
                };

                // A straight, un-banked rollout drives through the gap and collides.
                var straight = new Control[cfg.horizon];
                for (var i = 0; i < cfg.horizon; i++) straight[i] = new Control { thrust = 1f };
                var straightCost = RolloutCost(straight, 0, Initial(), input);

                // The best synthesized bank primitive threads for far less.
                var bestPrim = float.MaxValue;
                for (var p = 0; p < count; p++)
                    bestPrim = math.min(bestPrim, RolloutCost(prims, p, Initial(), input));

                Assert.That(straightCost, Is.GreaterThan(settings.collisionPenalty),
                    "the straight rollout must actually collide (incur the collision penalty)");
                Assert.That(bestPrim, Is.LessThan(straightCost - settings.collisionPenalty),
                    $"a bank primitive should thread the gap far cheaper (prim {bestPrim:F0} vs straight {straightCost:F0})");
            }
            finally { obstacles.Dispose(); }
        }

        [Test]
        public void Injection_LowersSolverBestEliteCost()
        {
            var scan = NarrowGapScan();
            var (_, prims, count) = DetectAndSynthesize(scan);

            using var solver = new SolverBuffers();
            const int reps = 4;
            float costOff = 0f, costOn = 0f;
            for (var r = 0; r < reps; r++)
            {
                costOff += SolveOnce(solver, scan, null, 0).cost;
                costOn += SolveOnce(solver, scan, prims, count).cost;
            }
            // The injected primitive threads and becomes the winning (cheapest) elite. Whether the
            // softmax mean visibly commits to a bank depends on how many Gaussian samples also
            // thread (razor-thin in this game's coupled bank/strafe) — that commitment is measured
            // in the closed-loop benchmark; here we assert the robust mechanism: the best elite drops.
            Assert.That(costOn, Is.LessThan(costOff),
                $"injection should lower the best-elite cost (on {costOn / reps:F0} vs off {costOff / reps:F0})");
        }

        private (float cost, float peakStrafe) SolveOnce(SolverBuffers solver, ObstacleScan scan,
            Control[] primitives, int primitiveCount)
        {
            var seq = new Control[cfg.horizon];
            var cost = solver.Solve(Initial(), seq, cfg, dynamics,
                scan, true,
                new float2(0f, 30f), float2.zero,
                float2.zero, float2.zero, float.NaN, 0f,
                default, 0f,
                settings.samples, settings.noiseStd, default,
                0f, 0f,
                settings.eliteFraction, settings.noiseKnots,
                settings.cemIterations, settings.strafeSigmaFloor, settings.sigmaFloor,
                settings.meanMomentum, primitives, primitiveCount, settings.eliteTemperature);
            var peak = 0f;
            for (var j = 0; j < cfg.horizon; j++) peak = math.max(peak, math.abs(seq[j].strafe));
            return (cost, peak);
        }
    }
}
#endif
