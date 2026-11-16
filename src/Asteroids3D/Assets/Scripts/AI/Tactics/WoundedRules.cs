using System;

namespace AI.Tactics
{
    /// <summary>
    /// Whether a ship is hurt, from its temper. Pure: the brain keeps the latch.
    /// https://github.com/amindell11/astronomical-home/issues/1579
    /// </summary>
    public static class WoundedRules
    {
        public static bool Starts(in Situation s, TacticsSettings t, int retreatsTaken)
        {
            if (t.wounded == WoundedResponse.Never) return false;
            if (t.commits && retreatsTaken >= t.retreats) return false;
            var gauge = Gauge(s, t.woundedBy) * 100f;
            return gauge < t.woundedBelow && s.secondsSinceHit <= t.hitWithin;
        }

        public static bool Ends(in Situation s, float respondingSeconds, TacticsSettings t)
        {
            if (t.wounded == WoundedResponse.LastStand) return false;
            return t.woundedEnds switch
            {
                WoundedEnds.WhenShieldIsBack => s.shieldPct * 100f >= t.shieldBack,
                WoundedEnds.AfterSeconds => respondingSeconds >= t.woundedSeconds,
                _ => throw new ArgumentOutOfRangeException(nameof(t.woundedEnds), t.woundedEnds, null),
            };
        }

        public static bool Next(bool wounded, float respondingSeconds, in Situation s, TacticsSettings t,
            int retreatsTaken) =>
            wounded ? !Ends(in s, respondingSeconds, t) : Starts(in s, t, retreatsTaken);

        public static bool InEffect(TacticsSettings t, Tactic flown) => t.wounded switch
        {
            WoundedResponse.BreakOff => flown == Tactic.BreakOff,
            WoundedResponse.FallBack => flown == Tactic.FallBack,
            WoundedResponse.TurnCautious => true,
            WoundedResponse.LastStand => flown == Tactic.Charge,
            WoundedResponse.Never => false,
            _ => throw new ArgumentOutOfRangeException(nameof(t.wounded), t.wounded, null),
        };

        private static float Gauge(in Situation s, WoundGauge gauge) => gauge switch
        {
            WoundGauge.Shield => s.shieldPct,
            WoundGauge.Hull => s.hullPct,
            _ => throw new ArgumentOutOfRangeException(nameof(gauge), gauge, null),
        };
    }
}
