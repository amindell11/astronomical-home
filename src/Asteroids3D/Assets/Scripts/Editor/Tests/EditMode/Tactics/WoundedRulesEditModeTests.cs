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
        private WoundedDraw draw;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<TacticsSettings>();
            settings.wounded = WoundedResponse.BreakOff;
            settings.woundedBy = WoundGauge.Shield;
            settings.woundedEnds = WoundedEnds.WhenShieldIsBack;
            settings.hitWithin = 2f;
            settings.commits = false;
            draw = new WoundedDraw(below: 25f, shieldBack: 80f, seconds: 5f, retreats: 2);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        private static Situation State(float shield, float hull, float sinceHit) =>
            new(shield, hull, sinceHit, hasTarget: true);

        private bool Starts(float shield, float hull, float sinceHit, int retreatsTaken = 0) =>
            WoundedRules.Starts(State(shield, hull, sinceHit), settings, in draw, retreatsTaken);

        [Test]
        public void Starts_OnTheShieldGauge_AndOnTheHullGauge()
        {
            Assert.IsTrue(Starts(0.2f, 1f, 0f), "a shield under the draw");
            Assert.IsFalse(Starts(0.5f, 0.1f, 0f), "the shield gauge ignores a low hull");

            settings.woundedBy = WoundGauge.Hull;
            Assert.IsTrue(Starts(1f, 0.2f, 0f), "a hull under the draw");
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
        public void Commits_NoStartOnceRetreatsTakenReachesTheDraw()
        {
            settings.commits = true;

            Assert.IsTrue(Starts(0.1f, 1f, 0f, retreatsTaken: draw.retreats - 1));
            Assert.IsFalse(Starts(0.1f, 1f, 0f, retreatsTaken: draw.retreats));
        }

        [Test]
        public void WithoutCommits_StartsAgainPastTheDraw()
        {
            settings.commits = false;

            Assert.IsTrue(Starts(0.1f, 1f, 0f, retreatsTaken: draw.retreats + 3));
        }

        [Test]
        public void Ends_OnShieldBack()
        {
            settings.woundedEnds = WoundedEnds.WhenShieldIsBack;

            Assert.IsFalse(WoundedRules.Ends(State(0.5f, 1f, 0f), woundedSeconds: 100f, settings, in draw));
            Assert.IsTrue(WoundedRules.Ends(State(0.9f, 1f, 0f), woundedSeconds: 0f, settings, in draw));
        }

        [Test]
        public void Ends_OnSeconds()
        {
            settings.woundedEnds = WoundedEnds.AfterSeconds;

            Assert.IsFalse(WoundedRules.Ends(State(1f, 1f, 0f), draw.seconds - 0.1f, settings, in draw));
            Assert.IsTrue(WoundedRules.Ends(State(0.1f, 1f, 0f), draw.seconds, settings, in draw));
        }
    }
}
#endif
