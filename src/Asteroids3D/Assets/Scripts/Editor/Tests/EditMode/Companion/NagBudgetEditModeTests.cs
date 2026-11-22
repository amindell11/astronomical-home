using Game.Companion;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Companion
{
    [Category("UI")]
    public class NagBudgetEditModeTests
    {
        private CompanionFeelProfile feel;
        private NagBudget budget;

        [SetUp]
        public void SetUp()
        {
            feel = ScriptableObject.CreateInstance<CompanionFeelProfile>();
            budget = new NagBudget(feel);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(feel);

        private string Speak(float now, bool canSpeak = true) =>
            budget.TryTake(now, canSpeak, out var line) ? line : null;

        [Test]
        public void LineInsideTheInterval_IsDropped()
        {
            budget.Offer("first", mustSay: false, now: 0f, null);
            Assert.AreEqual("first", Speak(0f), "test premise: the first line speaks");

            budget.Offer("second", mustSay: false, now: feel.MinLineIntervalSeconds - 1f, null);
            Assert.IsNull(Speak(feel.MinLineIntervalSeconds - 1f));
            Assert.IsNull(Speak(feel.MinLineIntervalSeconds + 1f), "a dropped line never comes back");
        }

        [Test]
        public void MustSayLineInsideTheInterval_Speaks()
        {
            budget.Offer("first", mustSay: false, now: 0f, null);
            Speak(0f);

            budget.Offer("must", mustSay: true, now: 1f, null);
            Assert.AreEqual("must", Speak(1f));
        }

        [Test]
        public void WaitingLinePastStale_IsDropped()
        {
            budget.Offer("late", mustSay: true, now: 0f, null);
            Assert.IsNull(Speak(1f, canSpeak: false), "test premise: it cannot speak yet");

            Assert.IsNull(Speak(feel.StaleSeconds + .1f), "a line that waited past stale is gone");
        }

        [Test]
        public void WaitingLine_SpeaksOnceItCanWithinTheStaleWindow()
        {
            budget.Offer("waiting", mustSay: false, now: 0f, null);
            Assert.IsNull(Speak(.5f, canSpeak: false));

            Assert.AreEqual("waiting", Speak(feel.StaleSeconds - .1f));
            Assert.IsNull(Speak(feel.StaleSeconds), "a spoken line is taken once");
        }

        [Test]
        public void PlainOffer_DoesNotReplaceAWaitingMustSayLine()
        {
            budget.Offer("must", mustSay: true, now: 0f, null);
            budget.Offer("plain", mustSay: false, now: .1f, null);

            Assert.AreEqual("must", Speak(.2f));
        }

        [Test]
        public void FollowUp_IsTakenWhenTheOpenersHoldEnds_ThoughTheGapHasNotPassed()
        {
            Assert.Less(feel.HoldSeconds, feel.MinLineIntervalSeconds, "test premise: the hold ends inside the gap");
            budget.Offer("open", mustSay: false, now: 0f, "reply");
            Assert.AreEqual("open", Speak(0f));

            Assert.IsNull(Speak(feel.HoldSeconds - .1f), "the follow-up waits out the hold");
            Assert.AreEqual("reply", Speak(feel.HoldSeconds));
        }

        [Test]
        public void FollowUp_IsTakenWhileTheHudIsHidden()
        {
            budget.Offer("open", mustSay: false, now: 0f, "reply");
            Assert.AreEqual("open", Speak(0f));

            Assert.AreEqual("reply", Speak(feel.HoldSeconds, canSpeak: false));
        }

        [Test]
        public void FollowUp_ResetsTheGap()
        {
            budget.Offer("open", mustSay: false, now: 0f, "reply");
            Assert.AreEqual("open", Speak(0f));
            Assert.AreEqual("reply", Speak(feel.HoldSeconds));

            var stillInsideTheReplyGap = feel.HoldSeconds + feel.MinLineIntervalSeconds - 1f;
            budget.Offer("plain", mustSay: false, now: stillInsideTheReplyGap, null);
            Assert.IsNull(Speak(stillInsideTheReplyGap), "the gap counts from the reply");
        }

        [Test]
        public void FollowUp_OfARefusedOffer_IsNeverTaken()
        {
            budget.Offer("first", mustSay: false, now: 0f, null);
            Assert.AreEqual("first", Speak(0f));

            budget.Offer("second", mustSay: false, now: 1f, "reply");
            Assert.IsNull(Speak(1f), "the offer is refused inside the gap");
            Assert.IsNull(Speak(1f + feel.HoldSeconds));
            Assert.IsNull(Speak(feel.MinLineIntervalSeconds + 1f));
        }

        [Test]
        public void FollowUp_OfAStaleLine_IsNeverTaken()
        {
            budget.Offer("late", mustSay: true, now: 0f, "reply");
            Assert.IsNull(Speak(1f, canSpeak: false), "test premise: it cannot speak yet");

            Assert.IsNull(Speak(feel.StaleSeconds + .1f), "a line that waited past stale is gone");
            Assert.IsNull(Speak(feel.StaleSeconds + feel.HoldSeconds + 1f), "its follow-up never speaks");
        }

        [Test]
        public void MustSayTakenBeforeTheFollowUpIsDue_DropsTheFollowUp()
        {
            budget.Offer("open", mustSay: false, now: 0f, "reply");
            Assert.AreEqual("open", Speak(0f));

            budget.Offer("must", mustSay: true, now: 1f, null);
            Assert.AreEqual("must", Speak(1f));
            Assert.IsNull(Speak(feel.HoldSeconds), "taking the must-say drops the armed follow-up");
        }
    }
}
