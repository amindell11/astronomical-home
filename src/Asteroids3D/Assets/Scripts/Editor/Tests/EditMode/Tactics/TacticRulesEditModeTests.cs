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
            settings.wounded = WoundedResponse.BreakOff;
            settings.woundedBelow = 25f;
            settings.woundedEnds = WoundedEnds.WhenShieldIsBack;
            settings.shieldBack = 80f;
            settings.hitWithin = 2f;
            settings.nerveShield = 50f;
            settings.minimumHold = 2f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        // 15 u sits inside the target's 20 u guns, so the cone alone decides exposure.
        private static Situation Fight(float shield = Full, float sinceHit = Fresh, int allies = 0,
            bool openSide = false, bool inCone = false) =>
            new(shield, 1f, sinceHit, hasTarget: true, distance: 15f, targetRange: 20f, inTargetsCone: inCone,
                alliesInLane: allies, openSide: openSide ? Vector2.right : Vector2.zero);

        private Tactic Pick(in Situation situation, bool wounded) =>
            TacticRules.Next(Tactic.AttackRun, Fresh, situation, settings, wounded);

        [Test]
        public void WoundedBreakOff_BreaksOff_BeforeAnyOtherRule()
        {
            Assert.AreEqual(Tactic.BreakOff,
                Pick(Fight(allies: 2, openSide: true, inCone: true), wounded: true));
        }

        [Test]
        public void WoundedFallBack_FallsBack_BeforeAnyOtherRule()
        {
            settings.wounded = WoundedResponse.FallBack;

            Assert.AreEqual(Tactic.FallBack,
                Pick(Fight(allies: 2, openSide: true, inCone: true), wounded: true));
        }

        [Test]
        public void WoundedLastStand_Charges_BeforeFlankOrStandOff_EvenExposedWithAnEmptyShield()
        {
            settings.wounded = WoundedResponse.LastStand;

            Assert.AreEqual(Tactic.Charge,
                Pick(Fight(shield: 0f, sinceHit: 0f, allies: 2, openSide: true, inCone: true), wounded: true),
                "an open side would flank");
            Assert.AreEqual(Tactic.Charge,
                Pick(Fight(shield: 0f, sinceHit: 0f, allies: 2, inCone: true), wounded: true),
                "no open side would stand off");
        }

        [Test]
        public void LastStand_HoldsPastTheMinimumHold_WhileWounded()
        {
            settings.wounded = WoundedResponse.LastStand;

            Assert.AreEqual(Tactic.Charge,
                TacticRules.Next(Tactic.Charge, 10f, Fight(shield: 0f), settings, wounded: true));
        }

        [Test]
        public void WoundedJumpOut_BreaksOff_EvenExposedWithAnOpenSide_AndHoldsPastTheMinimumHold()
        {
            settings.wounded = WoundedResponse.JumpOut;

            Assert.AreEqual(Tactic.BreakOff,
                Pick(Fight(allies: 2, openSide: true, inCone: true), wounded: true));
            Assert.AreEqual(Tactic.BreakOff,
                TacticRules.Next(Tactic.BreakOff, 10f, Fight(), settings, wounded: true));
        }

        [Test]
        public void TurnCautious_WhenWounded_FlanksOrStandsOffWhileExposed_AndHomesWhenNot()
        {
            settings.wounded = WoundedResponse.TurnCautious;
            settings.nerveShield = 0f;

            Assert.AreEqual(Tactic.Flank, Pick(Fight(Full, openSide: true, inCone: true), wounded: true),
                "exposed at a full shield, a full nerve still shies");
            Assert.AreEqual(Tactic.StandOff, Pick(Fight(Full, inCone: true), wounded: true));
            Assert.AreEqual(Tactic.AttackRun, Pick(Fight(Full), wounded: true),
                "not exposed, and no ally in the lane, so it flies home");
        }

        [TestCase(WoundedResponse.BreakOff)]
        [TestCase(WoundedResponse.FallBack)]
        [TestCase(WoundedResponse.TurnCautious)]
        [TestCase(WoundedResponse.JumpOut)]
        public void NotWounded_NeverBreaksOffOrFallsBack(WoundedResponse response)
        {
            settings.wounded = response;
            var situation = Fight(Low, sinceHit: 0f, allies: 2, openSide: true, inCone: true);

            Assert.AreNotEqual(Tactic.BreakOff, Pick(situation, wounded: false));
            Assert.AreNotEqual(Tactic.FallBack, Pick(situation, wounded: false));
        }

        [Test]
        public void Never_WhenToldItIsWounded_FliesAsIfItWereNot()
        {
            settings.wounded = WoundedResponse.Never;
            var exposed = Fight(Shaken, openSide: true, inCone: true);
            var blocked = Fight(Low, sinceHit: 0f, allies: 2, openSide: true, inCone: true);

            Assert.AreEqual(Pick(exposed, wounded: false), Pick(exposed, wounded: true));
            Assert.AreEqual(Pick(blocked, wounded: false), Pick(blocked, wounded: true));
            Assert.AreNotEqual(Tactic.BreakOff, Pick(blocked, wounded: true));
            Assert.AreNotEqual(Tactic.FallBack, Pick(blocked, wounded: true));
        }

        [TestCase(true, Tactic.Flank)]
        [TestCase(false, Tactic.StandOff)]
        public void Rule2_AnAllyInMyLane_FlanksToAnOpenSide_ElseStandsOff(bool openSide, Tactic expected)
        {
            Assert.AreEqual(expected, Pick(Fight(allies: 1, openSide: openSide), wounded: false));
        }

        [TestCase(true, Tactic.Flank)]
        [TestCase(false, Tactic.StandOff)]
        public void Rule3_InTheTargetsGunsUnderNerve_FlanksOrStandsOff(bool openSide, Tactic expected)
        {
            Assert.AreEqual(expected, Pick(Fight(Shaken, openSide: openSide, inCone: true), wounded: false));
            Assert.AreEqual(Tactic.AttackRun, Pick(Fight(Full, openSide: openSide, inCone: true), wounded: false),
                "a ship with its nerve presses into the target's guns");
        }

        [Test]
        public void Rule3_InTheConeButBeyondItsGuns_IsNotExposed()
        {
            var outOfReach = new Situation(Shaken, 1f, Fresh, hasTarget: true, distance: 25f, targetRange: 20f,
                inTargetsCone: true);

            Assert.IsFalse(outOfReach.exposed);
            Assert.AreEqual(Tactic.AttackRun, Pick(outOfReach, wounded: false));
        }

        [Test]
        public void Rule4_OtherwiseAnAttackRun()
        {
            Assert.AreEqual(Tactic.AttackRun, Pick(Fight(), wounded: false));
        }

        [Test]
        public void BreakOff_HoldsWhileWounded_AndEndsWhenNot_AfterTheMinimumHold()
        {
            Assert.AreEqual(Tactic.BreakOff, TacticRules.Next(Tactic.BreakOff, 10f, Fight(), settings, wounded: true));
            Assert.AreEqual(Tactic.BreakOff, TacticRules.Next(Tactic.BreakOff, 1.9f, Fight(), settings, wounded: false));
            Assert.AreEqual(Tactic.AttackRun, TacticRules.Next(Tactic.BreakOff, 10f, Fight(), settings, wounded: false));
        }

        [Test]
        public void FallBack_HoldsWhileWounded_AndEndsWhenNot_AfterTheMinimumHold()
        {
            settings.wounded = WoundedResponse.FallBack;

            Assert.AreEqual(Tactic.FallBack, TacticRules.Next(Tactic.FallBack, 10f, Fight(), settings, wounded: true));
            Assert.AreEqual(Tactic.FallBack, TacticRules.Next(Tactic.FallBack, 1.9f, Fight(), settings, wounded: false));
            Assert.AreEqual(Tactic.AttackRun, TacticRules.Next(Tactic.FallBack, 10f, Fight(), settings, wounded: false));
        }

        [Test]
        public void Flank_HoldsWhileItsReasonHolds_EndsWhenTheLaneClearsOutOfTheGuns_OrTheSideCloses()
        {
            Assert.AreEqual(Tactic.Flank, TacticRules.Next(Tactic.Flank, 10f, Fight(allies: 1, openSide: true), settings, wounded: false));
            Assert.AreEqual(Tactic.Flank, TacticRules.Next(Tactic.Flank, 10f, Fight(openSide: true, inCone: true), settings, wounded: false),
                "a clear lane inside the guns is no reason to stop flanking");
            Assert.AreEqual(Tactic.AttackRun, TacticRules.Next(Tactic.Flank, 10f, Fight(openSide: true), settings, wounded: false));
            Assert.AreEqual(Tactic.StandOff, TacticRules.Next(Tactic.Flank, 10f, Fight(allies: 1), settings, wounded: false),
                "the side closed with the lane still blocked");
        }

        [Test]
        public void StandOff_HoldsWhileItsReasonHolds_EndsWhenTheLaneClearsAndTheTargetTurnsAway_OrASideOpens()
        {
            Assert.AreEqual(Tactic.StandOff, TacticRules.Next(Tactic.StandOff, 10f, Fight(allies: 1), settings, wounded: false));
            Assert.AreEqual(Tactic.StandOff, TacticRules.Next(Tactic.StandOff, 10f, Fight(inCone: true), settings, wounded: false),
                "the target still faces me");
            Assert.AreEqual(Tactic.AttackRun, TacticRules.Next(Tactic.StandOff, 10f, Fight(), settings, wounded: false));
            Assert.AreEqual(Tactic.Flank, TacticRules.Next(Tactic.StandOff, 10f, Fight(allies: 1, openSide: true), settings, wounded: false));
        }

        [Test]
        public void AttackRun_EndsWhenAWoundedBreakOffOrTheLaneOrTheGunsWouldFire()
        {
            Assert.AreEqual(Tactic.AttackRun, TacticRules.Next(Tactic.AttackRun, 10f, Fight(), settings, wounded: false));
            Assert.AreEqual(Tactic.BreakOff, TacticRules.Next(Tactic.AttackRun, 10f, Fight(), settings, wounded: true));
            Assert.AreEqual(Tactic.StandOff, TacticRules.Next(Tactic.AttackRun, 10f, Fight(allies: 1), settings, wounded: false));
            Assert.AreEqual(Tactic.Flank, TacticRules.Next(Tactic.AttackRun, 10f, Fight(Shaken, openSide: true, inCone: true), settings, wounded: false));
        }

        [TestCase(Tactic.BreakOff)]
        [TestCase(Tactic.FallBack)]
        [TestCase(Tactic.Flank)]
        [TestCase(Tactic.StandOff)]
        [TestCase(Tactic.AttackRun)]
        public void EveryTactic_HoldsForTheMinimumHold_EvenWithItsReasonGone(Tactic tactic)
        {
            // A full shield and clear shot end the other four; a lane ally ends the attack run.
            var situation = tactic == Tactic.AttackRun ? Fight(allies: 1) : Fight();
            var otherwise = TacticRules.Next(tactic, 2f, situation, settings, wounded: false);

            Assert.AreEqual(tactic, TacticRules.Next(tactic, 1.9f, situation, settings, wounded: false));
            Assert.AreNotEqual(tactic, otherwise, "past the hold, the ended reason re-picks");
        }

        [TestCase(0f)]
        [TestCase(Shaken)]
        [TestCase(Full)]
        public void NerveAt100_InTheTargetsGuns_FlanksOrStandsOff_AtEveryShield(float shield)
        {
            settings.nerveShield = 100f;

            Assert.AreEqual(Tactic.Flank, Pick(Fight(shield, openSide: true, inCone: true), wounded: false));
            Assert.AreEqual(Tactic.StandOff, Pick(Fight(shield, inCone: true), wounded: false));
        }

        [TestCase(0f)]
        [TestCase(Low)]
        [TestCase(Full)]
        public void NotWounded_NerveAt0_Presses_AtEveryShield(float shield)
        {
            settings.nerveShield = 0f;

            Assert.AreEqual(Tactic.AttackRun,
                Pick(Fight(shield, sinceHit: 0f, openSide: true, inCone: true), wounded: false));
        }

        [TestCase(Tactic.AttackRun)]
        [TestCase(Tactic.Flank)]
        [TestCase(Tactic.StandOff)]
        public void PlainFight_PicksTheHome(Tactic home)
        {
            settings.home = home;

            Assert.AreEqual(home, Pick(Fight(), wounded: false));
        }

        [Test]
        public void HomeFlankAndHomeStandOff_HoldPastTheMinimumHold_WithNothingButBeingHome()
        {
            settings.home = Tactic.Flank;
            Assert.AreEqual(Tactic.Flank, TacticRules.Next(Tactic.Flank, 10f, Fight(), settings, wounded: false));

            settings.home = Tactic.StandOff;
            Assert.AreEqual(Tactic.StandOff, TacticRules.Next(Tactic.StandOff, 10f, Fight(), settings, wounded: false));
        }

        [Test]
        public void ARuleOverridesTheHome()
        {
            settings.home = Tactic.StandOff;

            Assert.AreEqual(Tactic.BreakOff, Pick(Fight(allies: 1, openSide: true), wounded: true),
                "wounded on a break-off temper breaks off");
            Assert.AreEqual(Tactic.Flank, Pick(Fight(allies: 1, openSide: true), wounded: false),
                "an ally in the lane with an open side flanks");
        }
    }
}
#endif
