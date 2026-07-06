#if UNITY_EDITOR
using System;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Solver-level tests for the extracted <see cref="Mpc"/> planner. These drive
    /// <c>Mpc.Plan(in MpcInputs)</c> directly — no ship, physics, or NavigationIntent — which
    /// is the unit the old MpcEnemyProjection PlayMode tests were really probing through
    /// expensive closed-loop motion. Assertions are differential (compare two configurations)
    /// so they're robust to the sampler's stochasticity.
    /// </summary>
    [Category("MPC")]
    public class MpcSolverTests
    {
        private const string MpcSettingsPath = "Assets/Settings/AI/MPC/MpcSettings.asset";
        private const string ShipSettingsPath = "Assets/Settings/Ships/DefaultSettings.asset";

        private MpcSettings settings;
        private Dynamics dynamics;

        [SetUp]
        public void SetUp()
        {
            settings = AssetDatabase.LoadAssetAtPath<MpcSettings>(MpcSettingsPath);
            var ship = AssetDatabase.LoadAssetAtPath<ShipSettings>(ShipSettingsPath);
            Assert.That(settings, Is.Not.Null, $"Missing MPC settings at {MpcSettingsPath}");
            Assert.That(ship, Is.Not.Null, $"Missing ship settings at {ShipSettingsPath}");
            dynamics = ship.Dynamics;
        }

        private static MpcInputs WaypointInputs(float2 goalPos, float2 goalVel = default) => new()
        {
            kinematics = default,                 // ship at rest at origin, yaw 0
            goalPos = goalPos,
            goalVel = goalVel,
            goalMode = GoalMode.Waypoint,
            facingRad = float.NaN,
            enemyYaw = float.NaN,                 // no enemy (NaN == no tactical target)
            weightOverrides = Array.Empty<WeightOverride>(),
            obstacleScan = default,
            enableObstacleAvoidance = false,
        };

        // Warm-start the solver, then average the predicted terminal state over several solves
        // so per-solve sampling noise cancels out.
        private State SolveTerminal(MpcInputs inputs, int warmup = 8, int average = 6)
        {
            using var mpc = new Mpc(settings, dynamics);
            for (var i = 0; i < warmup; i++) mpc.Plan(in inputs);

            float2 posSum = default;
            float yawSum = 0f;
            for (var i = 0; i < average; i++)
            {
                mpc.Plan(in inputs);
                var terminal = mpc.PredictedStates[^1];
                posSum += terminal.pos;
                yawSum += terminal.yaw;
            }
            return new State { pos = posSum / average, yaw = yawSum / average };
        }

        [Test]
        public void Plan_HeadsTowardGoal()
        {
            var terminal = SolveTerminal(WaypointInputs(new float2(30f, 0f)));

            Assert.That(terminal.pos.x, Is.GreaterThan(1f),
                "Planned trajectory should advance toward an eastward goal");
            Assert.That(Mathf.Abs(terminal.pos.y), Is.LessThan(terminal.pos.x),
                "Planned trajectory should stay roughly on the axis toward the goal");
        }

        [Test]
        public void Plan_GoalVelocity_LeadsInTravelDirection()
        {
            // Same stationary goal position, opposite goal velocities.
            var north = SolveTerminal(WaypointInputs(new float2(30f, 0f), new float2(0f, 6f)));
            var south = SolveTerminal(WaypointInputs(new float2(30f, 0f), new float2(0f, -6f)));

            Assert.That(north.pos.y, Is.GreaterThan(south.pos.y),
                "Goal-velocity projection should lead the trajectory in the goal's travel direction");
        }

        [Test]
        public void Plan_FacingOverride_SteersYawTowardRequestedHeading()
        {
            var left = WaypointInputs(float2.zero);
            left.facingRad = 0.5f * Mathf.PI;          // +90°
            var right = WaypointInputs(float2.zero);
            right.facingRad = -0.5f * Mathf.PI;        // -90°

            var yawLeft = SolveTerminal(left).yaw;
            var yawRight = SolveTerminal(right).yaw;

            Assert.That(yawLeft, Is.GreaterThan(yawRight),
                "Opposite facing overrides should drive planned yaw in opposite directions");
        }

        [Test]
        public void KnotNoise_ProducesSustainedSameSignRuns()
        {
            const uint seed = 12345u;
            const int horizon = 17;
            const int candidates = 128;
            var sustainedRuns = 0;

            for (var candidate = 1; candidate <= candidates; candidate++)
            {
                var longestRun = 0;
                var currentRun = 0;
                var lastSign = 0;

                for (var step = 0; step < horizon; step++)
                {
                    var noise = GenerateCandidatesJob.KnotNoise(seed, candidate, step, horizon, 5, 1, 1f);
                    var sign = Math.Sign(noise);
                    if (sign == 0)
                    {
                        currentRun = 0;
                        lastSign = 0;
                        continue;
                    }

                    currentRun = sign == lastSign ? currentRun + 1 : 1;
                    longestRun = Math.Max(longestRun, currentRun);
                    lastSign = sign;
                }

                if (longestRun >= 5)
                    sustainedRuns++;
            }

            Assert.That(sustainedRuns, Is.GreaterThan(candidates / 2),
                "Knot-interpolated strafe noise should commonly hold a direction for at least five steps.");
        }

        [Test]
        public void ObstacleCost_BankedHullCanClearNarrowGap()
        {
            var obstacles = new Unity.Collections.NativeArray<ObstacleData>(1, Unity.Collections.Allocator.Temp);
            try
            {
                obstacles[0] = new ObstacleData { position = new float2(2.35f, 0f), radius = 1f, weight = 1f };

                var unbanked = Cost.ObstacleCost(float2.zero, float2.zero, obstacles, 1, shipRadius: 1.4f, maxDecel: 4f);
                var banked = Cost.ObstacleCost(float2.zero, float2.zero, obstacles, 1, shipRadius: 0.7f, maxDecel: 4f);

                Assert.That(unbanked, Is.GreaterThan(10f),
                    "Unbanked hull should collide with this just-too-narrow clearance.");
                Assert.That(banked, Is.EqualTo(0f).Within(0.001f),
                    "Bank-narrowed hull should clear the same obstacle when not closing.");
            }
            finally
            {
                obstacles.Dispose();
            }
        }

        [Test]
        public void ObstacleCost_StoppingDistanceIncreasesWithClosingSpeed()
        {
            var obstacles = new Unity.Collections.NativeArray<ObstacleData>(1, Unity.Collections.Allocator.Temp);
            try
            {
                obstacles[0] = new ObstacleData { position = new float2(10f, 0f), radius = 1f, weight = 1f };

                var slow = Cost.ObstacleCost(float2.zero, new float2(1f, 0f), obstacles, 1, shipRadius: 1.4f, maxDecel: 4f);
                var fast = Cost.ObstacleCost(float2.zero, new float2(10f, 0f), obstacles, 1, shipRadius: 1.4f, maxDecel: 4f);

                Assert.That(slow, Is.EqualTo(0f).Within(0.001f));
                Assert.That(fast, Is.GreaterThan(slow),
                    "A state that cannot brake before the obstacle should receive admissibility cost.");
            }
            finally
            {
                obstacles.Dispose();
            }
        }

        [Test]
        public void GapDetector_FindsGapBetweenTwoDiscs()
        {
            var obstacles = new Unity.Collections.NativeArray<ObstacleData>(2, Unity.Collections.Allocator.Temp);
            try
            {
                obstacles[0] = new ObstacleData { position = new float2(10f, -3f), radius = 1f, weight = 1f };
                obstacles[1] = new ObstacleData { position = new float2(10f, 3f), radius = 1f, weight = 1f };

                var found = GapDetector.TryFindBestGap(float2.zero, new float2(20f, 0f),
                    obstacles, 2, shipRadius: 1f, maxBankAngleRad: 0.61f, out var gap);

                Assert.That(found, Is.True);
                Assert.That(gap.axis.x, Is.GreaterThan(0.95f));
                Assert.That(Mathf.Abs(gap.axis.y), Is.LessThan(0.05f));
                Assert.That(gap.width, Is.EqualTo(4f).Within(0.001f));
                Assert.That(gap.bankOnly, Is.EqualTo(0));
            }
            finally
            {
                obstacles.Dispose();
            }
        }

        [Test]
        public void GapDetector_ClassifiesBankOnlyGap()
        {
            var obstacles = new Unity.Collections.NativeArray<ObstacleData>(2, Unity.Collections.Allocator.Temp);
            try
            {
                obstacles[0] = new ObstacleData { position = new float2(10f, -1.95f), radius = 1f, weight = 1f };
                obstacles[1] = new ObstacleData { position = new float2(10f, 1.95f), radius = 1f, weight = 1f };

                var found = GapDetector.TryFindBestGap(float2.zero, new float2(20f, 0f),
                    obstacles, 2, shipRadius: 1f, maxBankAngleRad: 0.61f, out var gap);

                Assert.That(found, Is.True);
                Assert.That(gap.width, Is.LessThan(2.1f));
                Assert.That(gap.bankOnly, Is.EqualTo(1));
            }
            finally
            {
                obstacles.Dispose();
            }
        }

        // NOTE: A projectile-lead facing test (enemy moving, projectileSpeed > 0 shifts the
        // planned facing toward the intercept) belongs here, but base-weight facing/exposure
        // authority was intentionally collapsed in the MPC retune (authority moved to per-state
        // weight multipliers). At base weights the shift is sub-degree, so it isn't meaningfully
        // assertable yet — same reason the old EnemyState_WithProjectileSpeed PlayMode test is
        // [Ignore]d. Revive with amplified facing weightOverrides after the reward refactor.
    }
}
#endif
