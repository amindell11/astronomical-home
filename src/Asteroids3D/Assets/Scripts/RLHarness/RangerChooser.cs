using AI;
using AI.Context;
using AI.States;
using Movement.MPC;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>
    /// Scripted PR-3 stand-in over the velocity interface: closes to weapon range on a live
    /// target, holds, and gates fire on (PR-2a's Range maneuver against a real opponent). The
    /// per-weapon Gunsight/ShouldFire still applies on top of the fire gate — that layering is
    /// deliberate.
    /// </summary>
    public class RangerChooser : IIntentChooser
    {
        private const int RecomputeIntervalTicks = 10;
        private const float RangeGain = 0.6f;
        private const float RangeDamping = 0.5f;

        private Ship target;
        private float desiredRange;
        private float projectileSpeed;

        private int tickCounter;
        private NavigationIntent cachedIntent = NavigationIntent.None;

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
            cachedIntent = NavigationIntent.None;
        }

        public NavigationIntent Decide(AIContext ctx, float dt)
        {
            if (!target || !target.gameObject.activeInHierarchy || ctx?.Self == null)
                return NavigationIntent.None;

            if (tickCounter % RecomputeIntervalTicks == 0)
                cachedIntent = BuildIntent(ctx);
            tickCounter++;
            return cachedIntent;
        }

        private NavigationIntent BuildIntent(AIContext ctx)
        {
            var self = ctx.Self.Kinematics;
            var enemy = target.Kinematics;

            var los = enemy.pos - self.pos;
            var r = los.magnitude;
            var losHat = r > 1e-4f ? los / r : Vector2.up;
            var closing = RangeGain * (r - desiredRange) * losHat;
            var damping = RangeDamping * Vector2.Dot(self.vel, losHat) * losHat;
            var vRef = Vector2.ClampMagnitude(closing - damping, ctx.Self.Dynamics.maxSpeed);

            return new NavigationIntent
            {
                isValid = true,
                goalMode = GoalMode.VelocityReference,
                velocityReference = vRef,
                hasTarget = true,
                target = new EnemyTarget
                {
                    kinematics = enemy,
                    dynamics = target.Dynamics,
                    source = target.transform,
                },
                // Solely to route SetEnemyState for intercept-yaw aim; the tactical cost block stays off (goalMode-derived tacticalEnabled).
                applyTacticalCosts = true,
                projectileSpeed = projectileSpeed,
                enableFiring = true,
            };
        }
    }
}
