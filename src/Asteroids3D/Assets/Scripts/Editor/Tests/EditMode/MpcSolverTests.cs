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
        private const string ShipSettingsPath = "Assets/Settings/Ships/Default_Frame.asset";

        private MpcSettings settings;
        private Dynamics dynamics;

        [SetUp]
        public void SetUp()
        {
            settings = AssetDatabase.LoadAssetAtPath<MpcSettings>(MpcSettingsPath);
            var ship = AssetDatabase.LoadAssetAtPath<FrameSettings>(ShipSettingsPath);
            Assert.That(settings, Is.Not.Null, $"Missing MPC settings at {MpcSettingsPath}");
            Assert.That(ship, Is.Not.Null, $"Missing ship settings at {ShipSettingsPath}");
            dynamics = ship.Resolve().Dynamics;
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

        // NOTE: A projectile-lead facing test (enemy moving, projectileSpeed > 0 shifts the
        // planned facing toward the intercept) belongs here, but base-weight facing/exposure
        // authority was intentionally collapsed in the MPC retune (authority moved to per-state
        // weight multipliers). At base weights the shift is sub-degree, so it isn't meaningfully
        // assertable yet — same reason the old EnemyState_WithProjectileSpeed PlayMode test is
        // [Ignore]d. Revive with amplified facing weightOverrides after the reward refactor.
    }
}
#endif
