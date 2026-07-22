using AI;
using AI.Context;
using AI.States;
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
                velocityReference = velocityReference,
            };
        }
    }
}
