#if UNITY_EDITOR
using AI.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Tactics
{
    [Category("AI")]
    public class WoundedRulesEditModeTests
    {
        private TacticsSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<TacticsSettings>();
            settings.wounded = WoundedResponse.BreakOff;
            settings.woundedBy = WoundGauge.Shield;
            settings.woundedEnds = WoundedEnds.WhenShieldIsBack;
            settings.hitWithin = 2f;
            settings.commits = false;
            settings.woundedBelow = 25f;
            settings.shieldBack = 80f;
            settings.woundedSeconds = 5f;
            settings.retreats = 2;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        private static Situation State(float shield, float hull, float sinceHit) =>
            new(shield, hull, sinceHit, hasTarget: true);

        private bool Starts(float shield, float hull, float sinceHit, int retreatsTaken = 0) =>
            WoundedRules.Starts(State(shield, hull, sinceHit), settings, retreatsTaken);

        [Test]
        public void Starts_OnTheShieldGauge_AndOnTheHullGauge()
        {
            Assert.IsTrue(Starts(0.2f, 1f, 0f), "a shield under the breaking point");
            Assert.IsFalse(Starts(0.5f, 0.1f, 0f), "the shield gauge ignores a low hull");

            settings.woundedBy = WoundGauge.Hull;
            Assert.IsTrue(Starts(1f, 0.2f, 0f), "a hull under the breaking point");
            Assert.IsFalse(Starts(0.1f, 0.5f, 0f), "the hull gauge ignores a low shield");
        }

        [Test]
        public void Starts_NeedsAHitWithinTheWindow()
        {
            Assert.IsTrue(Starts(0.1f, 1f, settings.hitWithin));
            Assert.IsFalse(Starts(0.1f, 1f, settings.hitWithin + 0.01f), "a hit older than the window");
        }

        [Test]
        public void Never_NeverStarts()
        {
            settings.wounded = WoundedResponse.Never;

            Assert.IsFalse(Starts(0.1f, 0.1f, 0f));
        }

        [Test]
        public void Commits_NoStartOnceRetreatsTakenReachesItsRetreats()
        {
            settings.commits = true;

            Assert.IsTrue(Starts(0.1f, 1f, 0f, retreatsTaken: settings.retreats - 1));
            Assert.IsFalse(Starts(0.1f, 1f, 0f, retreatsTaken: settings.retreats));
        }

        [Test]
        public void WithoutCommits_StartsAgainPastItsRetreats()
        {
            settings.commits = false;

            Assert.IsTrue(Starts(0.1f, 1f, 0f, retreatsTaken: settings.retreats + 3));
        }

        [Test]
        public void Ends_OnShieldBack()
        {
            settings.woundedEnds = WoundedEnds.WhenShieldIsBack;

            Assert.IsFalse(WoundedRules.Ends(State(0.5f, 1f, 0f), respondingSeconds: 100f, settings));
            Assert.IsTrue(WoundedRules.Ends(State(0.9f, 1f, 0f), respondingSeconds: 0f, settings));
        }

        [Test]
        public void Ends_OnSeconds()
        {
            settings.woundedEnds = WoundedEnds.AfterSeconds;

            Assert.IsFalse(WoundedRules.Ends(State(1f, 1f, 0f), settings.woundedSeconds - 0.1f, settings));
            Assert.IsTrue(WoundedRules.Ends(State(0.1f, 1f, 0f), settings.woundedSeconds, settings));
        }

        [Test]
        public void LastStand_NeverEnds()
        {
            settings.wounded = WoundedResponse.LastStand;
            settings.woundedEnds = WoundedEnds.WhenShieldIsBack;

            Assert.IsFalse(WoundedRules.Ends(State(1f, 1f, 0f), respondingSeconds: 0f, settings),
                "a full shield does not end a last stand");

            settings.woundedEnds = WoundedEnds.AfterSeconds;
            Assert.IsFalse(WoundedRules.Ends(State(0.1f, 1f, 0f), respondingSeconds: 100f, settings),
                "seconds passed do not end a last stand");
        }

        [Test]
        public void InEffect_OnlyWhileThatResponseIsFlown()
        {
            settings.wounded = WoundedResponse.BreakOff;
            Assert.IsTrue(WoundedRules.InEffect(settings, Tactic.BreakOff));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.FallBack));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.AttackRun));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.Flank));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.StandOff));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.Charge));

            settings.wounded = WoundedResponse.FallBack;
            Assert.IsTrue(WoundedRules.InEffect(settings, Tactic.FallBack));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.BreakOff));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.AttackRun));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.Flank));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.StandOff));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.Charge));

            settings.wounded = WoundedResponse.TurnCautious;
            Assert.IsTrue(WoundedRules.InEffect(settings, Tactic.AttackRun), "any tactic");
            Assert.IsTrue(WoundedRules.InEffect(settings, Tactic.Flank), "any tactic");
            Assert.IsTrue(WoundedRules.InEffect(settings, Tactic.StandOff), "any tactic");
            Assert.IsTrue(WoundedRules.InEffect(settings, Tactic.BreakOff), "any tactic");
            Assert.IsTrue(WoundedRules.InEffect(settings, Tactic.FallBack), "any tactic");
            Assert.IsTrue(WoundedRules.InEffect(settings, Tactic.Charge), "any tactic");

            settings.wounded = WoundedResponse.Never;
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.AttackRun));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.Flank));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.StandOff));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.BreakOff));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.FallBack));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.Charge));
        }

        [Test]
        public void JumpOut_NeverEnds_AndIsInEffectOnlyWhileBreakingOff()
        {
            settings.wounded = WoundedResponse.JumpOut;
            settings.woundedEnds = WoundedEnds.WhenShieldIsBack;

            Assert.IsFalse(WoundedRules.Ends(State(1f, 1f, 0f), respondingSeconds: 0f, settings),
                "a full shield does not end a jump out");

            settings.woundedEnds = WoundedEnds.AfterSeconds;
            Assert.IsFalse(WoundedRules.Ends(State(0.1f, 1f, 0f), respondingSeconds: 100f, settings),
                "seconds passed do not end a jump out");

            Assert.IsTrue(WoundedRules.InEffect(settings, Tactic.BreakOff));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.Charge));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.FallBack));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.AttackRun));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.Flank));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.StandOff));
        }

        [Test]
        public void LastStand_IsInEffectOnlyWhileCharging()
        {
            settings.wounded = WoundedResponse.LastStand;

            Assert.IsTrue(WoundedRules.InEffect(settings, Tactic.Charge));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.BreakOff));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.FallBack));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.AttackRun));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.Flank));
            Assert.IsFalse(WoundedRules.InEffect(settings, Tactic.StandOff));
        }
    }
}
#endif
