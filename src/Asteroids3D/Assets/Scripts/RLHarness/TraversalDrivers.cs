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

    /// <summary>The legacy comparator: the same crossing through the old goal-mode path's NAV identity — MaintainRange onto a marker past the far edge, which routes the shared nav/terminal-field bake (Navigator keys it on MaintainRange-with-target) plus the position/closing/obstacle cost stack. Tactical costs stay OFF: with them on, the marker is treated as an armed enemy and exposure-avoid + tangential-strafe fight the closing gradient — combat jinking against a phantom, not traversal.</summary>
    public class LegacyNavTraversalChooser : IIntentChooser
    {
        public const string DriverTag = "legacy-nav";

        public const float DesiredRange = 15f;
        public const float RangeTolerance = 5f;
        /// <summary>Place the goal marker this far past the crossing exit so the whole range-hold band lies beyond the finish line — the ship cannot settle short of it.</summary>
        public const float GoalStandoff = DesiredRange + RangeTolerance;

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
                desiredRange = DesiredRange,
                rangeTolerance = RangeTolerance,
                hasTarget = true,
                target = new EnemyTarget
                {
                    kinematics = new Kinematics(destPlane, Vector2.zero, 0f, 0f, 0f),
                    dynamics = ctx.Self.Dynamics,
                    source = destination.transform,
                },
                applyTacticalCosts = false,
            };
        }
    }
}
