using AI;
using AI.Context;
using Movement;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Scripted stand-in policy over the velocity interface: closes to weapon range on a live target, holds, and gates fire on; the per-weapon Gunsight/ShouldFire deliberately still applies on top of the fire gate.</summary>
    public class RangerChooser : IIntentChooser
    {
        private const int RecomputeIntervalTicks = 10;
        private const float RangeGain = 0.6f;
        private const float RangeDamping = 0.5f;

        private Ship target;
        private float desiredRange;
        private float projectileSpeed;

        private int tickCounter;
        private ActIntent cachedIntent = ActIntent.None;

        public void Configure(Ship target, float desiredRange, float projectileSpeed)
        {
            this.target = target;
            this.desiredRange = desiredRange;
            this.projectileSpeed = projectileSpeed;
            Reset();
        }

        public void Reset()
        {
            tickCounter = 0;
            cachedIntent = ActIntent.None;
        }

        public ActIntent Decide(AIContext ctx, float dt)
        {
            if (!target || !target.gameObject.activeInHierarchy || ctx?.Self == null)
                return ActIntent.None;

            if (tickCounter % RecomputeIntervalTicks == 0)
                cachedIntent = BuildIntent(ctx);
            tickCounter++;
            return cachedIntent;
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

        private ActIntent BuildIntent(AIContext ctx)
        {
            var self = ctx.Self.Kinematics;
            var enemy = target.Kinematics;
            var vRef = HoldRangeVelocity(in self, in enemy, desiredRange, ctx.Self.Dynamics.maxSpeed);

            return new ActIntent
            {
                isValid = true,
                velocityReference = vRef,
                hasTarget = true,
                target = new EnemyTarget
                {
                    kinematics = enemy,
                    dynamics = target.Dynamics,
                    source = target.transform,
                },
                aimAtTarget = true,
                projectileSpeed = projectileSpeed,
                enableFiring = true,
            };
        }
    }
}
