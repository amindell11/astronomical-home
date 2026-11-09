using System;
using UnityEngine;

namespace AI.Tactics
{
    public enum Tactic
    {
        AttackRun,
        Flank,
        StandOff,
        BreakOff,
    }

    /// <summary>
    /// Situation in, tactic out, at each brain recompute. A tactic holds while the reason it was picked
    /// holds, and always for the minimum hold; once it ends, the ordered rules pick again.
    /// https://github.com/amindell11/astronomical-home/issues/1349
    /// </summary>
    public static class TacticRules
    {
        public static Tactic Next(Tactic current, float heldSeconds, in Situation situation,
            TacticsSettings settings) =>
            heldSeconds < settings.minimumHold || Holds(current, situation, settings)
                ? current
                : Pick(situation, settings);

        private static Tactic Pick(in Situation situation, TacticsSettings settings)
        {
            var shield = situation.shieldPct * 100f;
            if (shield < settings.breakOffShield && situation.secondsSinceHit <= settings.hitWithin)
                return Tactic.BreakOff;
            if (situation.alliesInLane > 0 || (situation.exposed && shield < settings.nerveShield))
                return situation.openSide != Vector2.zero ? Tactic.Flank : Tactic.StandOff;
            return Tactic.AttackRun;
        }

        private static bool Holds(Tactic tactic, in Situation situation, TacticsSettings settings) => tactic switch
        {
            Tactic.BreakOff => situation.shieldPct * 100f < settings.resumeShield,
            Tactic.Flank => situation.openSide != Vector2.zero && (situation.alliesInLane > 0 || situation.exposed),
            Tactic.StandOff => situation.openSide == Vector2.zero
                && (situation.alliesInLane > 0 || situation.inTargetsCone),
            Tactic.AttackRun => Pick(situation, settings) == Tactic.AttackRun,
            _ => throw new ArgumentOutOfRangeException(nameof(tactic), tactic, null),
        };
    }
}
