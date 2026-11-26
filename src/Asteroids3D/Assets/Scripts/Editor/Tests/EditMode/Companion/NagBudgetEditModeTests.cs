using Game.Companion;
using NUnit.Framework;
using UI.Comms;
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

        private static CommMessage M(string text) => new CommMessage("Companion", Color.white, text);

        private string Speak(float now, bool canSpeak = true) =>
            budget.TryTake(now, canSpeak, out var line) ? line.Text : null;

        [Test]
        public void LineInsideTheInterval_IsDropped()
        {
            budget.Offer(M("first"), mustSay: false, now: 0f, null);
            Assert.AreEqual("first", Speak(0f), "test premise: the first line speaks");

            budget.Offer(M("second"), mustSay: false, now: feel.MinLineIntervalSeconds - 1f, null);
            Assert.IsNull(Speak(feel.MinLineIntervalSeconds - 1f));
            Assert.IsNull(Speak(feel.MinLineIntervalSeconds + 1f), "a dropped line never comes back");
        }

        [Test]
        public void LinePastTheIntervalButInsideALongHold_IsDropped()
        {
            var longLine = new string('x', 1000);
            Assert.Greater(feel.HoldFor(M(longLine).Plain), feel.MinLineIntervalSeconds + 1f, "test premise: the hold outlasts the gap");
            budget.Offer(M(longLine), mustSay: false, now: 0f, null);
            Assert.AreEqual(longLine, Speak(0f));

            var pastTheGap = feel.MinLineIntervalSeconds + .5f;
            budget.Offer(M("second"), mustSay: false, now: pastTheGap, null);
            Assert.IsNull(Speak(pastTheGap), "a plain line never cuts one still held");
        }

        [Test]
        public void MustSayLineInsideTheInterval_Speaks()
        {
            budget.Offer(M("first"), mustSay: false, now: 0f, null);
            Speak(0f);

            budget.Offer(M("must"), mustSay: true, now: 1f, null);
            Assert.AreEqual("must", Speak(1f));
        }

        [Test]
        public void WaitingLinePastStale_IsDropped()
        {
            budget.Offer(M("late"), mustSay: true, now: 0f, null);
            Assert.IsNull(Speak(1f, canSpeak: false), "test premise: it cannot speak yet");

            Assert.IsNull(Speak(feel.StaleSeconds + .1f), "a line that waited past stale is gone");
        }

        [Test]
        public void WaitingLine_SpeaksOnceItCanWithinTheStaleWindow()
        {
            budget.Offer(M("waiting"), mustSay: false, now: 0f, null);
            Assert.IsNull(Speak(.5f, canSpeak: false));

            Assert.AreEqual("waiting", Speak(feel.StaleSeconds - .1f));
            Assert.IsNull(Speak(feel.StaleSeconds), "a spoken line is taken once");
        }

        [Test]
        public void PlainOffer_DoesNotReplaceAWaitingMustSayLine()
        {
            budget.Offer(M("must"), mustSay: true, now: 0f, null);
            budget.Offer(M("plain"), mustSay: false, now: .1f, null);

            Assert.AreEqual("must", Speak(.2f));
        }

        [Test]
        public void FollowUp_IsTakenWhenTheOpenersHoldEnds_ThoughTheGapHasNotPassed()
        {
            Assert.Less(feel.HoldFor(M("open").Plain), feel.MinLineIntervalSeconds, "test premise: the hold ends inside the gap");
            budget.Offer(M("open"), mustSay: false, now: 0f, M("reply"));
            Assert.AreEqual("open", Speak(0f));

            Assert.IsNull(Speak(feel.HoldFor(M("open").Plain) - .1f), "the follow-up waits out the hold");
            Assert.AreEqual("reply", Speak(feel.HoldFor(M("open").Plain)));
        }

        [Test]
        public void FollowUp_OfALongOpener_WaitsUntilTheOpenerCanBeRead()
        {
            var longOpener = new string('x', 120);
            Assert.Greater(feel.HoldFor(M(longOpener).Plain), feel.HoldFor(M("open").Plain), "test premise: a long line holds longer");
            budget.Offer(M(longOpener), mustSay: false, now: 0f, M("reply"));
            Assert.AreEqual(longOpener, Speak(0f));

            Assert.IsNull(Speak(feel.HoldFor(M(longOpener).Plain) - .1f), "the follow-up waits out the long hold");
            Assert.AreEqual("reply", Speak(feel.HoldFor(M(longOpener).Plain)));
        }

        [Test]
        public void FollowUp_IsTakenWhileTheHudIsHidden()
        {
            budget.Offer(M("open"), mustSay: false, now: 0f, M("reply"));
            Assert.AreEqual("open", Speak(0f));

            Assert.AreEqual("reply", Speak(feel.HoldFor(M("open").Plain), canSpeak: false));
        }

        [Test]
        public void FollowUp_ResetsTheGap()
        {
            budget.Offer(M("open"), mustSay: false, now: 0f, M("reply"));
            Assert.AreEqual("open", Speak(0f));
            Assert.AreEqual("reply", Speak(feel.HoldFor(M("open").Plain)));

            var stillInsideTheReplyGap = feel.HoldFor(M("open").Plain) + feel.MinLineIntervalSeconds - 1f;
            budget.Offer(M("plain"), mustSay: false, now: stillInsideTheReplyGap, null);
            Assert.IsNull(Speak(stillInsideTheReplyGap), "the gap counts from the reply");
        }

        [Test]
        public void FollowUp_OfARefusedOffer_IsNeverTaken()
        {
            budget.Offer(M("first"), mustSay: false, now: 0f, null);
            Assert.AreEqual("first", Speak(0f));

            budget.Offer(M("second"), mustSay: false, now: 1f, M("reply"));
            Assert.IsNull(Speak(1f), "the offer is refused inside the gap");
            Assert.IsNull(Speak(1f + feel.HoldFor(M("open").Plain)));
            Assert.IsNull(Speak(feel.MinLineIntervalSeconds + 1f));
        }

        [Test]
        public void FollowUp_OfAStaleLine_IsNeverTaken()
        {
            budget.Offer(M("late"), mustSay: true, now: 0f, M("reply"));
            Assert.IsNull(Speak(1f, canSpeak: false), "test premise: it cannot speak yet");

            Assert.IsNull(Speak(feel.StaleSeconds + .1f), "a line that waited past stale is gone");
            Assert.IsNull(Speak(feel.StaleSeconds + feel.HoldFor(M("open").Plain) + 1f), "its follow-up never speaks");
        }

        [Test]
        public void MustSayTakenBeforeTheFollowUpIsDue_DropsTheFollowUp()
        {
            budget.Offer(M("open"), mustSay: false, now: 0f, M("reply"));
            Assert.AreEqual("open", Speak(0f));

            budget.Offer(M("must"), mustSay: true, now: 1f, null);
            Assert.AreEqual("must", Speak(1f));
            Assert.IsNull(Speak(feel.HoldFor(M("open").Plain)), "taking the must-say drops the armed follow-up");
        }
    }
}
