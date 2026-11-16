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
        FallBack,
        Charge,
    }

    /// <summary>
    /// Situation in, tactic out, at each brain recompute. Wounded, last stand, break off and fall back come before
    /// any other rule, and turn cautious reads a full nerve. A tactic holds while the reason it was
    /// picked holds, a home tactic also while the rules still pick it, and always for the minimum hold;
    /// once it ends, the ordered rules pick again.
    /// https://github.com/amindell11/astronomical-home/issues/1349
    /// </summary>
    public static class TacticRules
    {
        public static Tactic Next(Tactic current, float heldSeconds, in Situation situation,
            TacticsSettings settings, bool wounded) =>
            heldSeconds < settings.minimumHold || Holds(current, situation, settings, wounded)
                ? current
                : Pick(situation, settings, wounded);

        private static Tactic Pick(in Situation situation, TacticsSettings settings, bool wounded)
        {
            if (wounded && settings.wounded == WoundedResponse.LastStand) return Tactic.Charge;
            if (wounded && settings.wounded == WoundedResponse.BreakOff) return Tactic.BreakOff;
            if (wounded && settings.wounded == WoundedResponse.FallBack) return Tactic.FallBack;
            var nerve = wounded && settings.wounded == WoundedResponse.TurnCautious ? 100f : settings.nerveShield;
            var shield = situation.shieldPct * 100f;
            if (situation.alliesInLane > 0 || (situation.exposed && Shies(shield, nerve)))
                return situation.openSide != Vector2.zero ? Tactic.Flank : Tactic.StandOff;
            return settings.home;
        }

        // Strictly below, so 0 always presses; 100 must shy at a full shield too.
        private static bool Shies(float shield, float nerve) => nerve >= 100f || shield < nerve;

        private static bool Holds(Tactic tactic, in Situation situation, TacticsSettings settings, bool wounded)
        {
            var reason = tactic switch
            {
                Tactic.Charge => wounded && settings.wounded == WoundedResponse.LastStand,
                Tactic.BreakOff => wounded && settings.wounded == WoundedResponse.BreakOff,
                Tactic.FallBack => wounded && settings.wounded == WoundedResponse.FallBack,
                Tactic.Flank => situation.openSide != Vector2.zero && (situation.alliesInLane > 0 || situation.exposed),
                Tactic.StandOff => situation.openSide == Vector2.zero
                    && (situation.alliesInLane > 0 || situation.inTargetsCone),
                // No reason of its own, so a non-home attack run ends with the minimum hold.
                Tactic.AttackRun => false,
                _ => throw new ArgumentOutOfRangeException(nameof(tactic), tactic, null),
            };
            return reason || (tactic == settings.home && Pick(situation, settings, wounded) == tactic);
        }
    }
}
