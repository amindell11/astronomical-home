using AI;
using AI.Context;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The open-loop composition's non-reactive enemy — THE fixed path shape (K1-2): a CCW circle around the arena center, held by a tangential command plus a radial P-term off the mover's own kinematics only, so the measured ship can never steer it. Never aims, never fires; the measured ship rides in the intent's target slot purely so obstacle exclusion keeps the mover blind to it.</summary>
    public sealed class FixedCircuitChooser : IIntentChooser
    {
        private const float CircuitRadius = 14f;
        // v²/R stays well inside thrust authority (~16 u/s² at these radii) so the circle actually holds.
        private const float SpeedFraction = 0.4f;
        private const float RadialGain = 0.9f;

        private Ship measured;
        private Vector2 center;

        public void Configure(Ship measuredShip, Vector2 arenaCenter)
        {
            measured = measuredShip;
            center = arenaCenter;
        }

        public ActIntent Decide(AIContext ctx, float dt)
        {
            if (ctx?.Self == null) return ActIntent.None;

            var self = ctx.Self.Kinematics;
            var maxSpeed = ctx.Self.Dynamics.maxSpeed;
            var fromCenter = self.pos - center;
            var r = fromCenter.magnitude;
            var outHat = r > 1e-4f ? fromCenter / r : Vector2.right;
            var tangent = new Vector2(-outHat.y, outHat.x);
            var vRef = SpeedFraction * maxSpeed * tangent + RadialGain * (CircuitRadius - r) * outHat;

            return new ActIntent
            {
                isValid = true,
                velocityReference = Vector2.ClampMagnitude(vRef, maxSpeed),
                hasTarget = true,
                target = new EnemyTarget
                {
                    kinematics = measured.Kinematics,
                    dynamics = measured.Dynamics,
                    source = measured.transform,
                },
            };
        }
    }
}
