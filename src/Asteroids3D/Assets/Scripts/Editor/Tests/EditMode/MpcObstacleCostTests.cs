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
    /// (bank narrowing applied to the hull radius, not padding) and the collision-course-gated
    /// turn-away admissibility term that replaced the graded threshold repulsion. All
    /// closed-form — these call the cost statics directly with hand-built configs and
    /// obstacle buffers.
    /// </summary>
    [Category("MPC")]
    public class MpcObstacleCostTests
    {
        // Mirrors the live DefaultSettings.asset dynamics the solver runs with.
        private const float ShipRadius = 1.4f;
        private const float SafetyMargin = 0.1f;
        private const float MaxBankRad = 35f * Mathf.Deg2Rad;
        private const float MaxLatAccel = 6f;   // best-case strafe accel (maxStrafeAcc / mass)
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
            maxLatAccel = MaxLatAccel,
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
        public void TurnAway_MonotonicInClosingSpeed_AndZeroWhenAvoidable()
        {
            // One obstacle dead ahead at 20 u: corridor = 2 + 1.5 = 3.5.
            // Sidestep boundary: ½·a·(d/v)² == corridor → v = d·sqrt(a / (2·corridor)) ≈ 18.5.
            var hull = ShipRadius + SafetyMargin;
            const float corridor = 2f + ShipRadius + SafetyMargin;
            WithObstacles(new[] { new float2(0f, 20f) }, 2f, obstacles =>
            {
                var prev = 0f;
                for (var v = 1f; v <= 30f; v += 1f)
                {
                    var cost = Cost.TurnAwayCost(float2.zero, new float2(0f, v),
                        obstacles, obstacles.Length, hull, MaxLatAccel);

                    Assert.That(cost, Is.GreaterThanOrEqualTo(prev - 1e-5f),
                        $"Turn-away cost decreased between v={v - 1} and v={v}");
                    Assert.That(cost, Is.InRange(0f, 1f));

                    // Below the sidestep boundary the state must be completely free.
                    var dTurn = 0.5f * MaxLatAccel * (20f / v) * (20f / v);
                    if (dTurn > corridor)
                        Assert.That(cost, Is.Zero, $"Avoidable state at v={v} must cost nothing");
                    prev = cost;
                }

                Assert.That(prev, Is.GreaterThan(0f),
                    "At max speed the sidestep no longer covers the corridor — cost must be positive");
            });
        }

        [Test]
        public void TurnAway_CollisionCourseGate_OffPathObstacleIsFree()
        {
            // Obstacle 5 u off the velocity axis, corridor 3.5: the ship already passes clear,
            // so even a fast dead-straight rollout pays nothing — a weaving pursuer steers
            // around off-course rocks for free.
            var hull = ShipRadius + SafetyMargin;
            WithObstacles(new[] { new float2(5f, 10f) }, 2f, obstacles =>
            {
                var cost = Cost.TurnAwayCost(float2.zero, new float2(0f, 25f),
                    obstacles, obstacles.Length, hull, MaxLatAccel);
                Assert.That(cost, Is.Zero);
            });
        }

        [Test]
        public void TurnAway_ZeroWhenReceding_EvenWhenClose()
        {
            // Skimming just outside the hull boundary while flying away: no penalty —
            // proximity alone is free (close-and-tight flying is the point).
            var hull = ShipRadius + SafetyMargin;
            WithObstacles(new[] { new float2(0f, 4f) }, 2f, obstacles =>
            {
                var cost = Cost.TurnAwayCost(float2.zero, new float2(0f, -25f),
                    obstacles, obstacles.Length, hull, MaxLatAccel);
                Assert.That(cost, Is.Zero);
            });
        }

        [Test]
        public void TurnAway_ContinuousAtBoundary()
        {
            // Cost just above the sidestep boundary must be small — the squared deficit ramps
            // C¹-smoothly from zero instead of jumping (no set-membership discontinuity).
            var hull = ShipRadius + SafetyMargin;
            WithObstacles(new[] { new float2(0f, 20f) }, 2f, obstacles =>
            {
                // Boundary ≈ 18.52; probe just above (v = 19 → deficit ≈ 0.05, squared ≈ 0.0025).
                var cost = Cost.TurnAwayCost(float2.zero, new float2(0f, 19f),
                    obstacles, obstacles.Length, hull, MaxLatAccel);
                Assert.That(cost, Is.GreaterThan(0f));
                Assert.That(cost, Is.LessThan(0.05f),
                    "Cost must rise smoothly from the boundary, not jump");
            });
        }

        [Test]
        public void CollidingRollout_OutCosts_EveryNonCollidingState()
        {
            // The collision penalty must decisively dominate the bounded admissibility term:
            // an overlapping state costs at least the penalty, while the worst possible
            // non-colliding state costs at most wObstacle (bounded by 1) — never comparable.
            var cfg = CollisionOnlyConfig();
            cfg.wObstacle = 5f;
            WithObstacles(new[] { new float2(0f, 2f) }, 2f, obstacles =>
            {
                var input = new CostInput { obstacles = obstacles, obstacleCount = obstacles.Length, enemyYaw = float.NaN };

                var overlapping = Cost.Evaluate(new State(), new Control(), new Control(), input, cfg, false);
                var closingFast = Cost.Evaluate(new State { pos = new float2(0f, -8f), vel = new float2(0f, 30f) },
                    new Control(), new Control(), input, cfg, false);

                Assert.That(overlapping, Is.GreaterThanOrEqualTo(CollisionPenalty));
                Assert.That(closingFast, Is.LessThanOrEqualTo(cfg.wObstacle + 1f),
                    "Non-colliding admissibility cost is bounded — it must never rival the penalty");
                Assert.That(overlapping, Is.GreaterThan(closingFast * 10f));
            });
        }
    }
}
#endif
