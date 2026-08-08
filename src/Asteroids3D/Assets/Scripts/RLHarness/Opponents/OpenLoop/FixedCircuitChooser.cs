using AI;
using AI.Context;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The open-loop composition's non-reactive enemy — THE fixed path shape (K1-2): a CCW circle around the arena center, held by a tangential command plus a radial P-term off the mover's own kinematics only, so the measured ship can never steer it. Never aims, never fires, and carries no anchor at all.</summary>
    public sealed class FixedCircuitChooser : IIntentChooser
    {
        private const float CircuitRadius = 14f;
        // v²/R stays well inside thrust authority (~16 u/s² at these radii) so the circle actually holds.
        private const float SpeedFraction = 0.4f;
        private const float RadialGain = 0.9f;

        private Vector2 center;

        public void Configure(Vector2 arenaCenter)
        {
            center = arenaCenter;
        }

        public BrainDecision? Decide(AIContext ctx, float dt)
        {
            if (ctx?.Self == null) return null;

            var self = ctx.Self.Kinematics;
            var maxSpeed = ctx.Self.Dynamics.maxSpeed;
            var fromCenter = self.pos - center;
            var r = fromCenter.magnitude;
            var outHat = r > 1e-4f ? fromCenter / r : Vector2.right;
            var tangent = new Vector2(-outHat.y, outHat.x);
            var vRef = SpeedFraction * maxSpeed * tangent + RadialGain * (CircuitRadius - r) * outHat;

            return new BrainDecision(NavObjective.Planar(Vector2.ClampMagnitude(vRef, maxSpeed)));
        }
    }
}
