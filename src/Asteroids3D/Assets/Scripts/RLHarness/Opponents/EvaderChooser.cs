using AI.Context;
using AI.States;
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
            Vector2 arenaCenter, float borderRadius)
        {
            this.jukePeriodSeconds = jukePeriodSeconds;
            this.jukeSeed = jukeSeed;
            Bind(threat, speedFraction, arenaCenter, borderRadius);
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

        protected override NavigationIntent BuildIntent(AIContext ctx)
        {
            if (recomputes++ % jukeEveryRecomputes == 0)
                jukeSign = rng.Next(2) == 0 ? -1 : 1;

            var self = ctx.Self.Kinematics;
            var away = self.pos - target.Kinematics.pos;
            var fleeHat = away.sqrMagnitude > 1e-8f ? away.normalized : Vector2.up;
            var dir = (fleeHat + JukeBlend * jukeSign * new Vector2(-fleeHat.y, fleeHat.x)).normalized;

            return new NavigationIntent
            {
                isValid = true,
                velocityReference = Steered(self.pos, speedFraction * ctx.Self.Dynamics.maxSpeed * dir),
            };
        }
    }
}
