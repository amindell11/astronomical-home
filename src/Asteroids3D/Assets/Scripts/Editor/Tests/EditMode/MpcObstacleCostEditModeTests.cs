#if UNITY_EDITOR
using System;
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
    /// A2 obstacle-cost tests: the near-binary hard-collision term (bank-narrowed hull) and the
    /// continuous stopping-distance admissibility term that replaced the threshold potential.
    /// Deterministic — no sampler involved.
    /// </summary>
    [Category("MPC")]
    public class MpcObstacleCostEditModeTests
    {
        private const string MpcSettingsPath = "Assets/Settings/AI/MPC/MpcSettings.asset";
        private const string ShipSettingsPath = "Assets/Settings/Ships/DefaultSettings.asset";

        private MpcSettings settings;
        private Dynamics dynamics;
        private Config cfg;

        [SetUp]
        public void SetUp()
        {
            settings = AssetDatabase.LoadAssetAtPath<MpcSettings>(MpcSettingsPath);
            var ship = AssetDatabase.LoadAssetAtPath<ShipSettings>(ShipSettingsPath);
            Assert.That(settings, Is.Not.Null, $"Missing MPC settings at {MpcSettingsPath}");
            Assert.That(ship, Is.Not.Null, $"Missing ship settings at {ShipSettingsPath}");
            dynamics = ship.Dynamics;
            cfg = BuildConfig(settings, dynamics);
        }

        // Mirrors Mpc.ApplyDynamics so the Config matches what the solver actually runs.
        private static Config BuildConfig(MpcSettings s, Dynamics d)
        {
            var c = s.ToConfig();
            c.maxBankAngleRad = d.maxBankAngleRad;
            c.maxSpeedSq = d.maxSpeed * d.maxSpeed;
            c.maxYawRateSq = d.maxYawRate * d.maxYawRate;
            c.shipRadius = d.shipRadius;
            var maxForce = Mathf.Max(d.forwardAcc, Mathf.Max(d.reverseAcc, d.maxStrafeAcc));
            c.maxDecel = d.mass > 0f ? maxForce / d.mass : maxForce;
            return c;
        }

        private static float Hull(Config c, float strafe)
        {
            var profileScale = c.maxBankAngleRad > 0f
                ? Mathf.Cos(Mathf.Abs(strafe) * c.maxBankAngleRad)
                : 1f;
            return c.shipRadius * profileScale;
        }

        private static float ObstacleCostAt(float2 pos, float2 vel, float hull,
            float2 obsPos, float obsRadius, Config c)
        {
            var obstacles = new NativeArray<ObstacleData>(1, Allocator.Temp);
            try
            {
                obstacles[0] = new ObstacleData { position = obsPos, radius = obsRadius, weight = 1f };
                return Cost.ObstacleCost(pos, vel, hull, obstacles, 1, c);
            }
            finally { obstacles.Dispose(); }
        }

        // (a) A gap just too narrow for the unbanked hull collides, but the same geometry clears
        // once the ship banks at full strafe (the hull narrows by cos(maxBank)).
        [Test]
        public void CollisionTerm_BankNarrowsHull_ClearsAJustTooNarrowGap()
        {
            var hullUnbanked = Hull(cfg, 0f);
            var hullBanked = Hull(cfg, 1f);
            Assert.That(hullBanked, Is.LessThan(hullUnbanked), "Banking must narrow the hull");

            const float obsRadius = 1f;
            // Place the obstacle between the two collision radii so only the wider (unbanked) hull hits.
            var collideUnbanked = obsRadius + hullUnbanked + cfg.obstacleSafetyMargin;
            var clearBanked = obsRadius + hullBanked + cfg.obstacleSafetyMargin;
            Assert.That(clearBanked, Is.LessThan(collideUnbanked));
            var dist = 0.5f * (collideUnbanked + clearBanked);
            var obsPos = new float2(dist, 0f);

            // vel = 0 isolates the collision term (admissibility is 0 when not closing).
            var unbanked = ObstacleCostAt(float2.zero, float2.zero, hullUnbanked, obsPos, obsRadius, cfg);
            var banked = ObstacleCostAt(float2.zero, float2.zero, hullBanked, obsPos, obsRadius, cfg);

            Assert.That(unbanked, Is.GreaterThanOrEqualTo(cfg.collisionPenalty),
                "Unbanked hull should overlap and incur the collision penalty");
            Assert.That(banked, Is.EqualTo(0f),
                "Max-strafe banked hull should clear the same gap with zero cost");
        }

        // (b) Admissibility rises monotonically with closing speed at fixed clearance, and is
        // exactly 0 while the ship can still stop in time (stoppingDist <= clearance).
        [Test]
        public void Admissibility_MonotoneInClosingSpeed_ZeroWhenBrakeable()
        {
            var hull = Hull(cfg, 0f);
            const float obsRadius = 1f;
            const float clearance = 5f;
            var dist = obsRadius + hull + clearance;      // hull-surface to obstacle-surface = clearance
            var obsPos = new float2(dist, 0f);            // obstacle straight ahead (+x)

            float Cost(float speed) =>
                ObstacleCostAt(float2.zero, new float2(speed, 0f), hull, obsPos, obsRadius, cfg);

            // Brakeable: stoppingDist = v^2/(2*maxDecel). With maxDecel ~7, v=5 => ~1.8 < 5.
            var brakeable = 5f;
            Assert.That(brakeable * brakeable / (2f * cfg.maxDecel), Is.LessThan(clearance),
                "Test setup: low speed must be brakeable within the clearance");
            Assert.That(Cost(brakeable), Is.EqualTo(0f),
                "Cost must be exactly 0 while the ship can stop before the obstacle");

            var c12 = Cost(12f);
            var c15 = Cost(15f);
            var c20 = Cost(20f);
            Assert.That(c12, Is.GreaterThan(0f), "Once stoppingDist overruns clearance, cost is positive");
            Assert.That(c15, Is.GreaterThan(c12), "Cost must increase with closing speed");
            Assert.That(c20, Is.GreaterThan(c15), "Cost must increase with closing speed");

            // Not closing => 0 regardless of proximity.
            Assert.That(ObstacleCostAt(float2.zero, new float2(-20f, 0f), hull, obsPos, obsRadius, cfg),
                Is.EqualTo(0f), "Receding motion must not incur admissibility cost");
        }

        // (c) A rollout that drives straight through an obstacle out-costs every rollout that
        // steers clear, in a fixed obstacle+goal scenario.
        [Test]
        public void CollidingRollout_OutCosts_EveryNonCollidingRollout()
        {
            var goal = new float2(0f, 12f);
            var obstacle = new float2(0f, 6f);   // directly between ship and goal
            const float obsRadius = 1f;

            var obstacles = new NativeArray<ObstacleData>(1, Allocator.Temp);
            try
            {
                obstacles[0] = new ObstacleData { position = obstacle, radius = obsRadius, weight = 1f };
                var input = new CostInput
                {
                    goalPos = goal,
                    obstacles = obstacles,
                    obstacleCount = 1,
                    enemyYaw = float.NaN,   // no enemy => tactical costs skipped
                    initialVel = float2.zero,
                };

                // Straight forward (yaw 0 => forward is +y) drives through the obstacle.
                var collidingCost = RolloutCost(BuildConstantSequence(new Control { thrust = 1f }), input);

                // Sequences that clearly avoid (0,6): strafe aside, reverse, or hold.
                var evasive = new[]
                {
                    BuildConstantSequence(new Control { strafe = 1f }),
                    BuildConstantSequence(new Control { strafe = -1f }),
                    BuildConstantSequence(new Control { thrust = -1f }),
                    BuildConstantSequence(new Control()),
                };

                foreach (var seq in evasive)
                {
                    var c = RolloutCost(seq, input);
                    Assert.That(collidingCost, Is.GreaterThan(c),
                        "Colliding rollout must out-cost every non-colliding rollout");
                }
            }
            finally { obstacles.Dispose(); }
        }

        private Control[] BuildConstantSequence(Control u)
        {
            var seq = new Control[cfg.horizon];
            for (var i = 0; i < seq.Length; i++) seq[i] = u;
            return seq;
        }

        private float RolloutCost(Control[] sequence, CostInput input)
        {
            var total = 0f;
            var current = new State();     // at rest at origin, yaw 0
            var prevU = new Control();
            for (var i = 0; i < cfg.horizon; i++)
            {
                var u = sequence[i];
                var isTerminal = i == cfg.horizon - 1;
                total += Cost.Evaluate(current, u, prevU, input, cfg, isTerminal, i);
                current = Model.Step(current, u, cfg, dynamics);
                prevU = u;
            }
            return total;
        }
    }
}
#endif
