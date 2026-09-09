#if UNITY_EDITOR
using AI;
using Game.RLHarness;
using NUnit.Framework;
using Ships;
using UnityEngine;
using AI.Strategy;

namespace Tests.EditMode
{
    /// <summary>Pins the Stage A3 session rows: every hand vector stays inside the signed-weight convention and binds the one anchor, drift-hold is an armed all-zero sentence (never NavObjective.Drift), and the brain replays its row verbatim with the row's trigger stance.</summary>
    [Category("AI")]
    public class RLSentenceEditModeTests
    {
        // ---- Row vectors ----

        [Test]
        public void Tokens_RoundTripThroughTryParse()
        {
            foreach (var row in SentenceRows.SessionRows)
            {
                Assert.IsTrue(SentenceRows.TryParse(SentenceRows.Token(row), out var parsed), SentenceRows.Token(row));
                Assert.AreEqual(row, parsed);
            }
            Assert.IsTrue(SentenceRows.TryParse("DRIFT-HOLD", out var upper), "tokens parse case-insensitively");
            Assert.AreEqual(SentenceRow.DriftHold, upper);
            Assert.IsFalse(SentenceRows.TryParse("brawl", out _), "the brawl row was rejected at ruling");
        }

        [Test]
        public void EveryRow_BindsTheAnchorAndStaysInsideTheWeightConvention()
        {
            foreach (var row in SentenceRows.SessionRows)
            {
                var objective = SentenceRows.Author(row, default);
                Assert.IsTrue(objective.TryGetAnchorId(out _), $"{row}: sessions are single-referent by ruling");
                var sentence = objective.sentence;
                Assert.IsTrue(sentence.AnyArmed, $"{row}: a session row is always a live sentence");
                if (sentence.aim.armed) Assert.LessOrEqual(Mathf.Abs(sentence.aim.weight), 1f, $"{row}: AIM");
                if (sentence.pos.armed) Assert.LessOrEqual(Mathf.Abs(sentence.pos.weight), 1f, $"{row}: POS");
                if (sentence.vel.armed) Assert.LessOrEqual(Mathf.Abs(sentence.vel.weight), 1f, $"{row}: VEL");
                if (sentence.field.armed) Assert.LessOrEqual(Mathf.Abs(sentence.field.weight), 1f, $"{row}: FIELD");
            }
        }

        [Test]
        public void DriftHold_ArmsEverySlotAtZeroAndStillSolves()
        {
            var objective = SentenceRows.Author(SentenceRow.DriftHold, default);
            var sentence = objective.sentence;

            Assert.IsTrue(sentence.aim.armed && sentence.pos.armed && sentence.vel.armed && sentence.field.armed,
                "\"nothing matters\" is a sentence — every slot armed");
            Assert.AreEqual(0f, sentence.aim.weight);
            Assert.AreEqual(0f, sentence.pos.weight);
            Assert.AreEqual(0f, sentence.vel.weight);
            Assert.AreEqual(0f, sentence.field.weight);
            Assert.IsFalse(objective.IsIdle, "the armed all-zero sentence must solve on the priors, not reset");
            Assert.IsTrue(NavObjective.Drift.IsIdle, "Drift stays the no-sentence idle this row must never author");
        }

        [Test]
        public void DummyCloseout_IsTheOnlyEngagingRow()
        {
            foreach (var row in SentenceRows.SessionRows)
                Assert.AreEqual(row == SentenceRow.DummyCloseout, SentenceRows.Engages(row), row.ToString());
        }

        // ---- Brain ----

        private GameObject targetGo;
        private GameObject brainGo;

        [SetUp]
        public void SetUp()
        {
            targetGo = new GameObject("SentenceTarget");
            brainGo = new GameObject("SentenceBrain");
        }

        [TearDown]
        public void TearDown()
        {
            if (targetGo) Object.DestroyImmediate(targetGo);
            if (brainGo) Object.DestroyImmediate(brainGo);
        }

        [Test]
        public void Brain_ReplaysItsRowWithTheRowTriggerStance()
        {
            var target = targetGo.AddComponent<Ship>();
            var brain = brainGo.AddComponent<SentenceBrain>();
            brain.Configure(target, SentenceRow.DummyCloseout);

            var decision = brain.Decide(null);

            Assert.IsTrue(decision.HasValue);
            Assert.IsTrue(decision.Value.nav.TryGetAnchorId(out var anchor));
            Assert.AreEqual(target.Id, anchor);
            Assert.AreEqual(SentenceRows.Author(SentenceRow.DummyCloseout, target.Id).sentence,
                decision.Value.nav.sentence, "the decision carries the row vector verbatim");
            Assert.IsTrue(decision.Value.engagePrimary, "closeout hands the trigger to the gunner");
            Assert.IsFalse(decision.Value.boost);

            brain.Configure(target, SentenceRow.Orbit);
            Assert.IsFalse(brain.Decide(null).Value.engagePrimary, "non-closeout rows hold fire");
        }

        [Test]
        public void Brain_DropsTheDecisionWhenTheTargetDies()
        {
            var target = targetGo.AddComponent<Ship>();
            var brain = brainGo.AddComponent<SentenceBrain>();
            brain.Configure(target, SentenceRow.DriftHold);
            Assert.IsTrue(brain.Decide(null).HasValue);

            targetGo.SetActive(false);
            Assert.IsFalse(brain.Decide(null).HasValue, "a dead referent is the standard no-decision");
        }
    }
}
#endif
