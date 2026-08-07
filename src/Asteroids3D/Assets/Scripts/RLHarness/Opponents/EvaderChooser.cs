using AI;
using AI.Context;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The pursuit teacher: flees along the threat LOS with a seeded tangential juke that flips at a jittered cadence; never fires.</summary>
    public class EvaderChooser : OpponentArchetypeChooser
    {
        private const float JukeBlend = 0.6f;

        private float jukePeriodSeconds;
        private int jukeSeed;

        private System.Random rng;
        private int jukeSign = 1;
        private int recomputes;
        private int jukeEveryRecomputes = 1;

        public void Configure(Ship threat, float speedFraction, float jukePeriodSeconds, int jukeSeed,
            Vector2 arenaCenter, float borderRadius, ArchetypeDrive drive = ArchetypeDrive.Production)
        {
            this.jukePeriodSeconds = jukePeriodSeconds;
            this.jukeSeed = jukeSeed;
            Bind(threat, speedFraction, arenaCenter, borderRadius, drive);
        }

        public override void Reset()
        {
            base.Reset();
            rng = new System.Random(jukeSeed);
            jukeSign = 1;
            recomputes = 0;
            jukeEveryRecomputes = Mathf.Max(1, Mathf.RoundToInt(
                jukePeriodSeconds / (RecomputeIntervalTicks * Time.fixedDeltaTime)));
        }

        /// <summary>The pure flee law: away from the threat, blended with the seeded tangential juke.</summary>
        internal static Vector2 FleeVelocity(Vector2 selfPos, Vector2 threatPos, int jukeSign, float speed)
        {
            var away = selfPos - threatPos;
            var fleeHat = away.sqrMagnitude > 1e-8f ? away.normalized : Vector2.up;
            var dir = (fleeHat + JukeBlend * jukeSign * new Vector2(-fleeHat.y, fleeHat.x)).normalized;
            return speed * dir;
        }

        protected override ActIntent BuildIntent(AIContext ctx)
        {
            if (recomputes++ % jukeEveryRecomputes == 0)
                jukeSign = rng.Next(2) == 0 ? -1 : 1;

            var self = ctx.Self.Kinematics;
            var vRef = FleeVelocity(self.pos, target.Kinematics.pos, jukeSign,
                speedFraction * ctx.Self.Dynamics.maxSpeed);

            return Pack(new ActIntent { isValid = true }, self.pos, vRef);
        }
    }
}
