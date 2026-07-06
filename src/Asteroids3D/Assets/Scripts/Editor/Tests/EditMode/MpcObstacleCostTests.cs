#if UNITY_EDITOR
using Movement.MPC;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Unit tests for the A2 obstacle-cost redesign: the hard hull-overlap collision term
    /// (bank narrowing applied to the hull radius, not padding) and the stopping-distance
    /// admissibility term that replaced the graded threshold repulsion. All closed-form —
    /// these call the cost statics directly with hand-built configs and obstacle buffers.
    /// </summary>
    [Category("MPC")]
    public class MpcObstacleCostTests
    {
        // Mirrors the live DefaultSettings.asset dynamics the solver runs with.
        private const float ShipRadius = 1.4f;
        private const float SafetyMargin = 0.1f;
        private const float MaxBankRad = 35f * Mathf.Deg2Rad;
        private const float BrakingDecel = 3.5f;  // reverseForce 2800 / mass 800
        private const float BrakingDrag = 0.3f;
        private const float CollisionPenalty = 1000f;

        private static Config CollisionOnlyConfig() => new()
        {
            dt = 0.1f,
            invDt = 10f,
            horizon = 17,
            shipRadius = ShipRadius,
            collisionSafetyMargin = SafetyMargin,
            collisionPenalty = CollisionPenalty,
            maxBankAngleRad = MaxBankRad,
            brakingDecel = BrakingDecel,
            brakingDrag = BrakingDrag,
            facingTarget = float.NaN,
            // Every weight left at 0 so Evaluate's total isolates the collision term.
        };

        private static void WithObstacles(float2[] positions, float radius,
            System.Action<NativeArray<ObstacleData>> assert)
        {
            var obstacles = new NativeArray<ObstacleData>(positions.Length, Allocator.Temp);
            try
            {
                for (var i = 0; i < positions.Length; i++)
                    obstacles[i] = new ObstacleData { position = positions[i], radius = radius, weight = 1f };
                assert(obstacles);
            }
            finally
            {
                obstacles.Dispose();
            }
        }

        [Test]
        public void Collision_JustTooNarrowGap_UnbankedHits_MaxBankClears()
        {
            // Gap between two discs of radius 2 centered at x = ±3.4:
            //   unbanked hull 1.4 + 0.1 = 1.5  → collision range 3.5  > 3.4 → hit
            //   max-bank hull 1.4·cos35° + 0.1 ≈ 1.247 → range ≈ 3.247 < 3.4 → clear
            var cfg = CollisionOnlyConfig();
            WithObstacles(new[] { new float2(-3.4f, 0f), new float2(3.4f, 0f) }, 2f, obstacles =>
            {
                var input = new CostInput { obstacles = obstacles, obstacleCount = obstacles.Length, enemyYaw = float.NaN };
                var state = new State(); // ship at the gap center, at rest

                var unbanked = Cost.Evaluate(state, new Control(), new Control(), input, cfg, false);
                var banked = Cost.Evaluate(state, new Control { strafe = 1f }, new Control(), input, cfg, false);

                Assert.That(unbanked, Is.GreaterThanOrEqualTo(CollisionPenalty),
                    "Unbanked hull overlaps the gap walls — the collision penalty must fire");
                Assert.That(banked, Is.LessThan(1f),
                    "At max strafe the bank-narrowed hull fits the gap — no collision, and the " +
                    "admissibility term is zero at rest");
            });
        }

        [Test]
        public void Collision_SafetyMargin_IsNotSpeedScaled()
        {
            // Same geometry, ship flying fast: the collision boundary must not move with speed.
            var cfg = CollisionOnlyConfig();
            WithObstacles(new[] { new float2(-3.4f, 0f), new float2(3.4f, 0f) }, 2f, obstacles =>
            {
                var input = new CostInput { obstacles = obstacles, obstacleCount = obstacles.Length, enemyYaw = float.NaN };
                var fast = new State { vel = new float2(0f, 25f) }; // flying along the gap axis

                var banked = Cost.Evaluate(fast, new Control { strafe = 1f }, new Control(), input, cfg, false);
                Assert.That(banked, Is.LessThan(CollisionPenalty),
                    "Speed must not inflate the collision boundary (that feedback loop was the " +
                    "threshold cost's failure mode)");
            });
        }

        [Test]
        public void Admissibility_MonotonicInClosingSpeed_AndZeroWhenBrakeable()
        {
            // One obstacle dead ahead: clearance = 20 − 2 − hull(1.5) = 16.5.
            var hull = ShipRadius + SafetyMargin;
            WithObstacles(new[] { new float2(0f, 20f) }, 2f, obstacles =>
            {
                var prev = 0f;
                for (var v = 0f; v <= 25f; v += 1f)
                {
                    var cost = Cost.AdmissibilityCost(float2.zero, new float2(0f, v),
                        obstacles, obstacles.Length, hull, BrakingDecel, BrakingDrag);

                    Assert.That(cost, Is.GreaterThanOrEqualTo(prev - 1e-5f),
                        $"Admissibility cost decreased between v={v - 1} and v={v}");
                    Assert.That(cost, Is.InRange(0f, 1f));

                    // Below the admissibility boundary the state must be completely free.
                    var stopping = v * v / (2f * (BrakingDecel + BrakingDrag * v));
                    if (stopping < 16.5f)
                        Assert.That(cost, Is.Zero, $"Brakeable state at v={v} must cost nothing");
                    prev = cost;
                }

                Assert.That(prev, Is.GreaterThan(0f),
                    "At max speed the stopping distance exceeds clearance — cost must be positive");
            });
        }

        [Test]
        public void Admissibility_ZeroWhenReceding_EvenWhenClose()
        {
            // Skimming just outside the hull boundary while flying away: no penalty —
            // proximity alone is free (close-and-tight flying is the point).
            var hull = ShipRadius + SafetyMargin;
            WithObstacles(new[] { new float2(0f, 4f) }, 2f, obstacles =>
            {
                var cost = Cost.AdmissibilityCost(float2.zero, new float2(0f, -25f),
                    obstacles, obstacles.Length, hull, BrakingDecel, BrakingDrag);
                Assert.That(cost, Is.Zero);
            });
        }

        [Test]
        public void Admissibility_ContinuousAtBoundary()
        {
            // Cost just above the admissibility boundary must be small — the term ramps
            // smoothly from zero instead of jumping (no set-membership discontinuity).
            var hull = ShipRadius + SafetyMargin;
            WithObstacles(new[] { new float2(0f, 20f) }, 2f, obstacles =>
            {
                // Solve stopping(v) = clearance = 16.5 → v ≈ 16.78; probe just above
                // (stopping(17) ≈ 16.80 → violation ≈ 0.018).
                var cost = Cost.AdmissibilityCost(float2.zero, new float2(0f, 17f),
                    obstacles, obstacles.Length, hull, BrakingDecel, BrakingDrag);
                Assert.That(cost, Is.GreaterThan(0f));
                Assert.That(cost, Is.LessThan(0.05f),
                    "Cost must rise smoothly from the boundary, not jump");
            });
        }
    }
}
#endif
