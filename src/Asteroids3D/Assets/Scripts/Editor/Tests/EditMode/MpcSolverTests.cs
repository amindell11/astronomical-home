#if UNITY_EDITOR
using AI.Scanning;
using Movement;
using AI.Navigation.MPC;
using NUnit.Framework;
using Ships;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Solver-level tests for the extracted <see cref="Mpc"/> planner. These drive
    /// <c>Mpc.Plan(in MpcInputs)</c> directly — no ship, physics, or decision seam.
    /// Assertions are differential (compare two configurations) or invariants held across a
    /// seed sweep, so they're robust to the sampler's stochasticity.
    /// </summary>
    [Category("MPC")]
    public class MpcSolverTests
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

        private static MpcInputs VelocityInputs(float2 velocityReference) => new()
        {
            kinematics = default,                 // ship at rest at origin, yaw 0 (nose +Y)
            velocityReference = velocityReference,
            facingRad = float.NaN,
            enemyYaw = float.NaN,                 // no enemy
            obstacleScan = default,
            enableObstacleAvoidance = false,
        };

        // Warm-start the solver, then average the predicted terminal state over several solves
        // so per-solve sampling noise cancels out.
        private State SolveTerminal(MpcInputs inputs, int warmup = 8, int average = 6)
        {
            using var mpc = new Mpc(settings, dynamics, 0u);
            for (var i = 0; i < warmup; i++) mpc.Plan(in inputs);

            float2 posSum = default;
            float2 velSum = default;
            var yawSum = 0f;
            for (var i = 0; i < average; i++)
            {
                mpc.Plan(in inputs);
                var terminal = mpc.PredictedStates[^1];
                posSum += terminal.pos;
                velSum += terminal.vel;
                yawSum += terminal.yaw;
            }
            return new State { pos = posSum / average, vel = velSum / average, yaw = yawSum / average };
        }

        private Control[] SolveSequence(int seed, MpcInputs inputs, int solves = 6)
        {
            using var mpc = new Mpc(settings, dynamics, (uint)seed);
            for (var i = 0; i < solves; i++) mpc.Plan(in inputs);
            return (Control[])mpc.BestSequence.Clone();
        }

        // Candidate noise is where the injected seed acts. The elite-averaged control converges
        // toward the same near-optimum regardless of seed (good MPC behavior), so divergence is
        // asserted here, at the raw candidate buffer, not on the planned output.
        private Control[] SolveCandidates(int seed, MpcInputs inputs)
        {
            using var mpc = new Mpc(settings, dynamics, (uint)seed);
            mpc.Plan(in inputs);
            var solver = mpc.Solver;
            var count = solver.LastSampleCount * solver.LastHorizon;
            var snapshot = new Control[count];
            for (var i = 0; i < count; i++) snapshot[i] = solver.Candidates[i];
            return snapshot;
        }

        private static bool SequencesEqual(Control[] a, Control[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
                if (a[i].thrust != b[i].thrust || a[i].strafe != b[i].strafe ||
                    a[i].yawTorque != b[i].yawTorque)
                    return false;
            return true;
        }

        [Test]
        public void Plan_SameSeedAndInputs_ReplaysIdenticalControls()
        {
            var inputs = VelocityInputs(new float2(6f, 3f));
            var first = SolveSequence(1234, inputs);
            var second = SolveSequence(1234, inputs);

            Assert.That(SequencesEqual(first, second), Is.True,
                "A fixed seed with identical inputs must reproduce the same planned controls bit-for-bit.");
        }

        [Test]
        public void Plan_SameSeed_SamplesIdenticalCandidateNoise()
        {
            var inputs = VelocityInputs(new float2(6f, 3f));
            Assert.That(SequencesEqual(SolveCandidates(1234, inputs), SolveCandidates(1234, inputs)), Is.True,
                "A fixed seed must reproduce the same candidate noise.");
        }

        [Test]
        public void Plan_DifferentSeeds_SampleDifferentCandidateNoise()
        {
            var inputs = VelocityInputs(new float2(6f, 3f));
            Assert.That(SequencesEqual(SolveCandidates(1, inputs), SolveCandidates(2, inputs)), Is.False,
                "Two ships with different seeds must sample different candidate noise.");
        }

        [Test]
        public void Plan_TracksCommandedVelocity()
        {
            const float speed = 6f;
            var terminal = SolveTerminal(VelocityInputs(new float2(0f, speed)));

            Assert.That(terminal.vel.y, Is.GreaterThan(0.2f * speed),
                "Planned trajectory should accelerate toward the commanded velocity");
            Assert.That(terminal.vel.y, Is.GreaterThan(math.abs(terminal.vel.x)),
                "Planned velocity should track the commanded axis, not drift sideways");
            Assert.That(terminal.pos.y, Is.GreaterThan(1f),
                "Tracking the command should carry the ship along the commanded direction");
        }

        [Test]
        public void Plan_FacingOverride_SteersYawTowardRequestedHeading()
        {
            var left = VelocityInputs(float2.zero);
            left.facingRad = 0.5f * Mathf.PI;          // +90°
            var right = VelocityInputs(float2.zero);
            right.facingRad = -0.5f * Mathf.PI;        // -90°

            var yawLeft = SolveTerminal(left).yaw;
            var yawRight = SolveTerminal(right).yaw;

            Assert.That(yawLeft, Is.GreaterThan(yawRight),
                "Opposite facing overrides should drive planned yaw in opposite directions");
        }

        private static readonly uint[] SelectionSeeds = { 1u, 2u, 3u, 4u, 5u, 6u, 7u, 8u };
        private static readonly float2 RockAheadVelocity = new(0f, 25f);

        // Full-speed run at a rock the coasting incumbent hits: swerves clear it on either side, and their average can steer back in.
        private static MpcInputs RockAheadInputs() => new()
        {
            kinematics = new Kinematics(Vector2.zero, new Vector2(0f, 25f), 0f, 0f, 0f),
            dt = 0.02f,
            velocityReference = RockAheadVelocity,
            facingRad = float.NaN,
            enemyYaw = float.NaN,
            obstacleScan = new ObstacleScan(new[] { new DetectedObstacle(new Vector3(0f, 30f, 0f), 4f, null) }, 1),
            enableObstacleAvoidance = true,
        };

        // Scores one sequence through the production Burst cost job against the inputs of the last solve.
        private static float ScoreThroughJob(Mpc mpc, Control[] sequence, Control lastControl)
        {
            var solver = mpc.Solver;
            using var rows = new NativeArray<Control>(sequence, Allocator.TempJob);
            using var cost = new NativeArray<float>(1, Allocator.TempJob);
            new EvaluateCandidatesJob
            {
                candidates = rows,
                costs = cost,
                costInput = new CostInput
                {
                    velocityReference = RockAheadVelocity,
                    obstacles = solver.Obstacles,
                    obstacleCount = solver.ObstacleCount,
                    enemyYaw = float.NaN,
                    enemyStates = solver.EnemyStates,
                    enemyStateCount = solver.LastEnemyStateCount,
                    initialVel = mpc.LastInitialState.vel,
                },
                initialState = mpc.LastInitialState,
                cfg = mpc.Config,
                dynamics = mpc.Dynamics,
                lastControl = lastControl,
            }.Schedule(1, 1).Complete();
            return cost[0];
        }

        // Independent reconstruction of the elite rule: up to K lowest-cost candidates that strictly beat
        // the incumbent, in index order, averaged and clamped. Null when none beat the incumbent.
        private static Control[] EliteMean(SolverBuffers solver, float eliteFraction)
        {
            var samples = solver.LastSampleCount;
            var horizon = solver.LastHorizon;
            var costs = solver.Costs;
            var eliteCount = math.max(1, (int)(samples * math.clamp(eliteFraction, 0.01f, 0.5f)));
            var sorted = costs.ToArray();
            System.Array.Sort(sorted);
            var threshold = eliteCount >= samples ? float.MaxValue : sorted[eliteCount - 1];

            var sum = new Control[horizon];
            var counted = 0;
            for (var i = 1; i < samples && counted < eliteCount; i++)
            {
                if (costs[i] > threshold || costs[i] >= costs[0]) continue;
                for (var j = 0; j < horizon; j++)
                {
                    var s = solver.Candidates[i * horizon + j];
                    sum[j] = new Control
                    {
                        thrust = sum[j].thrust + s.thrust,
                        strafe = sum[j].strafe + s.strafe,
                        yawTorque = sum[j].yawTorque + s.yawTorque,
                    };
                }
                counted++;
            }
            if (counted == 0) return null;

            var invCount = 1f / counted;
            for (var j = 0; j < horizon; j++)
                sum[j] = new Control
                {
                    thrust = math.clamp(sum[j].thrust * invCount, -1f, 1f),
                    strafe = math.clamp(sum[j].strafe * invCount, -1f, 1f),
                    yawTorque = math.clamp(sum[j].yawTorque * invCount, -1f, 1f),
                };
            return sum;
        }

        private static float CostTolerance(float cost) => 1e-5f * math.max(1f, math.abs(cost));

        [Test]
        public void Plan_EliteMeanScoringWorse_IsNotEmitted_AndReportedCostIsTheEmittedPlans()
        {
            var meanLost = 0;
            foreach (var seed in SelectionSeeds)
            {
                using var mpc = new Mpc(settings, dynamics, seed);
                var inputs = RockAheadInputs();
                var result = mpc.Plan(in inputs);

                var mean = EliteMean(mpc.Solver, settings.eliteFraction);
                if (mean == null) continue;
                var meanCost = ScoreThroughJob(mpc, mean, default);
                if (meanCost <= result.cost) continue;
                meanLost++;

                var emitted = (Control[])mpc.BestSequence.Clone();
                Assert.That(SequencesEqual(emitted, mean), Is.False,
                    $"Seed {seed}: the elite mean scored {meanCost:F2}, worse than the reported {result.cost:F2}, yet was emitted.");
                var emittedCost = ScoreThroughJob(mpc, emitted, default);
                Assert.That(result.cost, Is.EqualTo(emittedCost).Within(CostTolerance(emittedCost)),
                    $"Seed {seed}: the reported cost must be the emitted plan's own score.");
            }

            Assert.That(meanLost, Is.GreaterThan(0),
                "No seed produced an elite mean scoring worse than the emitted plan; the rock-ahead case no longer exercises selection.");
        }

        [Test]
        public void Plan_ReportedCost_IsTheEmittedSequencesScore_AndNeverAboveTheIncumbent()
        {
            foreach (var seed in SelectionSeeds)
            {
                using var mpc = new Mpc(settings, dynamics, seed);
                var inputs = RockAheadInputs();
                for (var solve = 0; solve < 6; solve++)
                {
                    var lastControl = mpc.LastControl;
                    var result = mpc.Plan(in inputs);

                    var emittedCost = ScoreThroughJob(mpc, mpc.BestSequence, lastControl);
                    Assert.That(result.cost, Is.EqualTo(emittedCost).Within(CostTolerance(emittedCost)),
                        $"Seed {seed}, solve {solve}: reported cost must match the emitted sequence's score.");
                    Assert.That(mpc.LastBestCost, Is.EqualTo(result.cost),
                        $"Seed {seed}, solve {solve}: LastBestCost must carry the same cost the result reports.");
                    Assert.That(result.cost, Is.LessThanOrEqualTo(mpc.Solver.Costs[0]),
                        $"Seed {seed}, solve {solve}: the emitted plan must never score worse than the incumbent.");
                }
            }
        }

        // NOTE: A projectile-lead facing test (enemy moving, projectileSpeed > 0 shifts the
        // planned facing toward the intercept) belongs here, but at base facing weights the
        // shift is sub-degree, so it isn't meaningfully assertable. Revive with an
        // amplified-wFacing settings asset.
    }
}
#endif
