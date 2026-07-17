using AI;
using AI.Context;
using AI.States;
using Movement;
using Movement.MPC;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The gate driver: a constant commanded velocity across the field, free-yaw (no target, so the solver yaws to exploit forward thrust) — measures pure velocity-mode obstacle competence.</summary>
    public class VelocityTraversalChooser : IIntentChooser
    {
        public const string DriverTag = "velocity-ref";

        private Vector2 velocityReference;

        public void Configure(Vector2 crossingDir, float speed) =>
            velocityReference = crossingDir.normalized * speed;

        public NavigationIntent Decide(AIContext ctx, float dt)
        {
            if (ctx?.Self == null) return NavigationIntent.None;
            return new NavigationIntent
            {
                isValid = true,
                goalMode = GoalMode.VelocityReference,
                velocityReference = velocityReference,
            };
        }
    }

    /// <summary>The legacy comparator: the same crossing through the old goal-mode path — MaintainRange(0) onto a static target at the far edge, which engages the shared nav/terminal field (Navigator routes MaintainRange-with-target to NavFieldService) plus the position-goal cost stack.</summary>
    public class LegacyNavTraversalChooser : IIntentChooser
    {
        public const string DriverTag = "legacy-nav";

        private DummyTarget destination;

        public void Configure(DummyTarget destination) => this.destination = destination;

        public NavigationIntent Decide(AIContext ctx, float dt)
        {
            if (destination == null || ctx?.Self == null) return NavigationIntent.None;
            var destPlane = destination.PlanePosition;
            return new NavigationIntent
            {
                isValid = true,
                goalMode = GoalMode.MaintainRange,
                goalPosition = destPlane,
                desiredRange = 0f,
                rangeTolerance = 0f,
                hasTarget = true,
                target = new EnemyTarget
                {
                    kinematics = new Kinematics(destPlane, Vector2.zero, 0f, 0f, 0f),
                    dynamics = ctx.Self.Dynamics,
                    source = destination.transform,
                },
                // Routes SetEnemyState so the terminal-field bake centers on the destination; the phantom "enemy" is static and unarmed, so the tactical block sees a benign anchor — this IS the production chase stack the comparator measures.
                applyTacticalCosts = true,
            };
        }
    }
}
