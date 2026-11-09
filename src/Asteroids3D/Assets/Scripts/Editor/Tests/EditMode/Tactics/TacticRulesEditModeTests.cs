#if UNITY_EDITOR
using AI.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Tactics
{
    [Category("AI")]
    public class TacticRulesEditModeTests
    {
        private const float Fresh = float.PositiveInfinity;
        private const float Low = 0.1f;
        private const float Shaken = 0.4f;
        private const float Full = 1f;

        private TacticsSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<TacticsSettings>();
            settings.breakOffShield = 25f;
            settings.resumeShield = 80f;
            settings.hitWithin = 2f;
            settings.nerveShield = 50f;
            settings.minimumHold = 2f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        // In the target's cone at 15 u; its guns reach 20 u, so the cone alone decides exposure.
        private static Situation Fight(float shield = Full, float sinceHit = Fresh, int allies = 0,
            bool openSide = false, bool inCone = false) =>
            new(shield, 1f, sinceHit, hasTarget: true, distance: 15f, targetRange: 20f, inTargetsCone: inCone,
                alliesInLane: allies, openSide: openSide ? Vector2.right : Vector2.zero);

        private Tactic Pick(in Situation situation) => TacticRules.Next(Tactic.AttackRun, Fresh, situation, settings);

        [Test]
        public void Rule1_ALowShieldJustHit_BreaksOff_BeforeAnyOtherRule()
        {
            Assert.AreEqual(Tactic.BreakOff, Pick(Fight(Low, sinceHit: 1f, allies: 2, openSide: true, inCone: true)));
            Assert.AreEqual(Tactic.AttackRun, Pick(Fight(Low, sinceHit: 3f)), "a low shield not hit lately presses");
        }

        [TestCase(true, Tactic.Flank)]
        [TestCase(false, Tactic.StandOff)]
        public void Rule2_AnAllyInMyLane_FlanksToAnOpenSide_ElseStandsOff(bool openSide, Tactic expected)
        {
            Assert.AreEqual(expected, Pick(Fight(allies: 1, openSide: openSide)));
        }

        [TestCase(true, Tactic.Flank)]
        [TestCase(false, Tactic.StandOff)]
        public void Rule3_InTheTargetsGunsUnderNerve_FlanksOrStandsOff(bool openSide, Tactic expected)
        {
            Assert.AreEqual(expected, Pick(Fight(Shaken, openSide: openSide, inCone: true)));
            Assert.AreEqual(Tactic.AttackRun, Pick(Fight(Full, openSide: openSide, inCone: true)),
                "a ship with its nerve presses into the target's guns");
        }

        [Test]
        public void Rule3_InTheConeButBeyondItsGuns_IsNotExposed()
        {
            var outOfReach = new Situation(Shaken, 1f, Fresh, hasTarget: true, distance: 25f, targetRange: 20f,
                inTargetsCone: true);

            Assert.IsFalse(outOfReach.exposed);
            Assert.AreEqual(Tactic.AttackRun, Pick(outOfReach));
        }

        [Test]
        public void Rule4_OtherwiseAnAttackRun()
        {
            Assert.AreEqual(Tactic.AttackRun, Pick(Fight()));
        }

        [Test]
        public void BreakOff_HoldsUntilTheShieldIsBackToResume()
        {
            Assert.AreEqual(Tactic.BreakOff, TacticRules.Next(Tactic.BreakOff, 10f, Fight(0.79f), settings));
            Assert.AreEqual(Tactic.AttackRun, TacticRules.Next(Tactic.BreakOff, 10f, Fight(0.8f), settings));
        }

        [Test]
        public void Flank_HoldsWhileItsReasonHolds_EndsWhenTheLaneClearsOutOfTheGuns_OrTheSideCloses()
        {
            Assert.AreEqual(Tactic.Flank, TacticRules.Next(Tactic.Flank, 10f, Fight(allies: 1, openSide: true), settings));
            Assert.AreEqual(Tactic.Flank, TacticRules.Next(Tactic.Flank, 10f, Fight(openSide: true, inCone: true), settings),
                "a clear lane inside the guns is no reason to stop flanking");
            Assert.AreEqual(Tactic.AttackRun, TacticRules.Next(Tactic.Flank, 10f, Fight(openSide: true), settings));
            Assert.AreEqual(Tactic.StandOff, TacticRules.Next(Tactic.Flank, 10f, Fight(allies: 1), settings),
                "the side closed with the lane still blocked");
        }

        [Test]
        public void StandOff_HoldsWhileItsReasonHolds_EndsWhenTheLaneClearsAndTheTargetTurnsAway_OrASideOpens()
        {
            Assert.AreEqual(Tactic.StandOff, TacticRules.Next(Tactic.StandOff, 10f, Fight(allies: 1), settings));
            Assert.AreEqual(Tactic.StandOff, TacticRules.Next(Tactic.StandOff, 10f, Fight(inCone: true), settings),
                "the target still faces me");
            Assert.AreEqual(Tactic.AttackRun, TacticRules.Next(Tactic.StandOff, 10f, Fight(), settings));
            Assert.AreEqual(Tactic.Flank, TacticRules.Next(Tactic.StandOff, 10f, Fight(allies: 1, openSide: true), settings));
        }

        [Test]
        public void AttackRun_EndsWhenRule1Or2Or3WouldFire()
        {
            Assert.AreEqual(Tactic.AttackRun, TacticRules.Next(Tactic.AttackRun, 10f, Fight(), settings));
            Assert.AreEqual(Tactic.BreakOff, TacticRules.Next(Tactic.AttackRun, 10f, Fight(Low, sinceHit: 0f), settings));
            Assert.AreEqual(Tactic.StandOff, TacticRules.Next(Tactic.AttackRun, 10f, Fight(allies: 1), settings));
            Assert.AreEqual(Tactic.Flank, TacticRules.Next(Tactic.AttackRun, 10f, Fight(Shaken, openSide: true, inCone: true), settings));
        }

        [TestCase(Tactic.BreakOff)]
        [TestCase(Tactic.Flank)]
        [TestCase(Tactic.StandOff)]
        [TestCase(Tactic.AttackRun)]
        public void EveryTactic_HoldsForTheMinimumHold_EvenWithItsReasonGone(Tactic tactic)
        {
            // A full shield and a clear shot end the other three; an ally in the lane ends the attack run.
            var situation = tactic == Tactic.AttackRun ? Fight(allies: 1) : Fight();
            var otherwise = TacticRules.Next(tactic, 2f, situation, settings);

            Assert.AreEqual(tactic, TacticRules.Next(tactic, 1.9f, situation, settings));
            Assert.AreNotEqual(tactic, otherwise, "past the hold, the ended reason re-picks");
        }
    }
}
#endif
