using AI;
using AI.Context;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Velocity-interface hold-range-and-fire pursuit: closes to a jittered range on the LOS (<see cref="RangerChooser.HoldRangeVelocity"/> capped to the jittered speed) and fires with intercept-lead aim. Serves the Kiter (long stand-off) and the Aggressor (short brawl) archetypes.</summary>
    public class HoldRangeFireChooser : OpponentArchetypeChooser
    {
        private float desiredRange;

        public void Configure(Ship target, float desiredRange, float speedFraction,
            Vector2 arenaCenter, float borderRadius, ArchetypeDrive drive = ArchetypeDrive.Production)
        {
            this.desiredRange = desiredRange;
            Bind(target, speedFraction, arenaCenter, borderRadius, drive);
        }

        protected override BrainDecision BuildDecision(AIContext ctx)
        {
            var self = ctx.Self.Kinematics;
            var vRef = RangerChooser.HoldRangeVelocity(in self, target.Kinematics, desiredRange,
                speedFraction * ctx.Self.Dynamics.maxSpeed);

            return Pack(self.pos, vRef, engages: true);
        }
    }
}
