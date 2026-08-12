using AI;
using AI.Context;
using Movement;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Scripted stand-in policy over the velocity interface: closes to weapon range on a live target, holds, and hands both triggers to the gunner; the per-weapon Gunsight/ShouldFire deliberately still applies on top.</summary>
    public class RangerBrain : Brain
    {
        private const int RecomputeIntervalTicks = 10;
        private const float RangeGain = 0.6f;
        private const float RangeDamping = 0.5f;

        private Ship target;
        private float desiredRange;

        private int tickCounter;
        private BrainDecision? cachedDecision;

        public void Configure(Ship target, float desiredRange)
        {
            this.target = target;
            this.desiredRange = desiredRange;
            ResetState();
        }

        public override void ResetState()
        {
            tickCounter = 0;
            cachedDecision = null;
        }

        public override BrainDecision? Decide(AIContext ctx)
        {
            if (!target || !target.gameObject.activeInHierarchy || ctx?.Self == null)
                return null;

            if (tickCounter % RecomputeIntervalTicks == 0)
                cachedDecision = BuildDecision(ctx);
            tickCounter++;
            return cachedDecision;
        }

        /// <summary>The pure hold-range velocity law (close, hold, damp along the LOS) — also the agent Heuristic's inverse-mapped source policy.</summary>
        public static Vector2 HoldRangeVelocity(in Kinematics self, in Kinematics enemy, float desiredRange, float maxSpeed)
        {
            var los = enemy.pos - self.pos;
            var r = los.magnitude;
            var losHat = r > 1e-4f ? los / r : Vector2.up;
            var closing = RangeGain * (r - desiredRange) * losHat;
            var damping = RangeDamping * Vector2.Dot(self.vel, losHat) * losHat;
            return Vector2.ClampMagnitude(closing - damping, maxSpeed);
        }

        private BrainDecision BuildDecision(AIContext ctx)
        {
            var self = ctx.Self.Kinematics;
            var enemy = target.Kinematics;
            var vRef = HoldRangeVelocity(in self, in enemy, desiredRange, ctx.Self.Dynamics.maxSpeed);

            var nav = NavObjective
                .Anchored(target.Id)
                .Planar(vRef)
                .Facing(0f, 1f);

            return new BrainDecision(nav, FireControl.Auto, FireControl.Auto);
        }
    }
}
