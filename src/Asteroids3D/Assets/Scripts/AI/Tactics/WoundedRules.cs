using System;

namespace AI.Tactics
{
    /// <summary>
    /// Whether a ship is hurt, from its temper and the numbers it drew. Pure: the brain keeps the latch.
    /// https://github.com/amindell11/astronomical-home/issues/1579
    /// </summary>
    public static class WoundedRules
    {
        public static bool Starts(in Situation s, TacticsSettings t, in WoundedDraw d, int retreatsTaken)
        {
            if (t.wounded == WoundedResponse.Never) return false;
            if (t.commits && retreatsTaken >= d.retreats) return false;
            var gauge = Gauge(s, t.woundedBy) * 100f;
            return gauge < d.below && s.secondsSinceHit <= t.hitWithin;
        }

        public static bool Ends(in Situation s, float woundedSeconds, TacticsSettings t, in WoundedDraw d) =>
            t.woundedEnds switch
            {
                WoundedEnds.WhenShieldIsBack => s.shieldPct * 100f >= d.shieldBack,
                WoundedEnds.AfterSeconds => woundedSeconds >= d.seconds,
                _ => throw new ArgumentOutOfRangeException(nameof(t.woundedEnds), t.woundedEnds, null),
            };

        public static bool Next(bool wounded, float woundedSeconds, in Situation s, TacticsSettings t,
            in WoundedDraw d, int retreatsTaken) =>
            wounded ? !Ends(in s, woundedSeconds, t, in d) : Starts(in s, t, in d, retreatsTaken);

        private static float Gauge(in Situation s, WoundGauge gauge) => gauge switch
        {
            WoundGauge.Shield => s.shieldPct,
            WoundGauge.Hull => s.hullPct,
            _ => throw new ArgumentOutOfRangeException(nameof(gauge), gauge, null),
        };
    }
}
