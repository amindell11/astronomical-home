using AI.Context;
using AI.States;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Velocity-interface hold-range-and-fire pursuit: closes to a jittered range on the LOS (<see cref="RangerChooser.HoldRangeVelocity"/> capped to the jittered speed) and fires with intercept-lead aim. Serves the Kiter (long stand-off) and the Aggressor (short brawl) archetypes.</summary>
    public class HoldRangeFireChooser : OpponentArchetypeChooser
    {
        private float desiredRange;
        private float projectileSpeed;

        public void Configure(Ship target, float desiredRange, float speedFraction, float projectileSpeed,
            Vector2 arenaCenter, float borderRadius)
        {
            this.desiredRange = desiredRange;
            this.projectileSpeed = projectileSpeed;
            Bind(target, speedFraction, arenaCenter, borderRadius);
        }

        protected override NavigationIntent BuildIntent(AIContext ctx)
        {
            var self = ctx.Self.Kinematics;
            var enemy = target.Kinematics;
            var vRef = RangerChooser.HoldRangeVelocity(in self, in enemy, desiredRange,
                speedFraction * ctx.Self.Dynamics.maxSpeed);

            return new NavigationIntent
            {
                isValid = true,
                velocityReference = Steered(self.pos, vRef),
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
