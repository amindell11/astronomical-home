#if UNITY_EDITOR
using Game.RLHarness;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Pins the combat-telemetry probe's pure pieces: range-band binning, the engagement hysteresis machine, regen edge detection, and boost edge counting.</summary>
    [Category("AI")]
    public class RLCombatTelemetryEditModeTests
    {
        [Test]
        public void BandIndex_QuarterEnvelopeBinsToTwiceFireRangeThenOverflow()
        {
            Assert.AreEqual(0, CombatTelemetrySampler.BandIndex(0f));
            Assert.AreEqual(0, CombatTelemetrySampler.BandIndex(0.24f));
            Assert.AreEqual(1, CombatTelemetrySampler.BandIndex(0.25f));
            Assert.AreEqual(3, CombatTelemetrySampler.BandIndex(0.99f));
            Assert.AreEqual(4, CombatTelemetrySampler.BandIndex(1f), "the envelope edge starts the fifth bin");
            Assert.AreEqual(7, CombatTelemetrySampler.BandIndex(1.99f));
            Assert.AreEqual(8, CombatTelemetrySampler.BandIndex(2f), "2.0× and beyond land in the overflow bin");
            Assert.AreEqual(8, CombatTelemetrySampler.BandIndex(10f));
            Assert.AreEqual(CombatTelemetryRow.RangeBands - 1, CombatTelemetrySampler.BandIndex(float.MaxValue));
        }

        [Test]
        public void EngagementTracker_EntersOnEnvelopeAndExitsOnlyAfterTheFullHysteresisWindow()
        {
            var tracker = new EngagementTracker(exitSeconds: 3f);

            Assert.AreEqual(EngagementTracker.Transition.None, tracker.Step(0.1f, false), "no envelope yet");
            Assert.IsFalse(tracker.Engaged);

            Assert.AreEqual(EngagementTracker.Transition.Enter, tracker.Step(1f, true));
            Assert.IsTrue(tracker.Engaged);
            Assert.AreEqual(EngagementTracker.Transition.None, tracker.Step(2f, true), "staying valid re-enters nothing");

            Assert.AreEqual(EngagementTracker.Transition.None, tracker.Step(4.9f, false),
                "2.9s of neither envelope is inside the hysteresis window");
            Assert.IsTrue(tracker.Engaged);
            Assert.AreEqual(EngagementTracker.Transition.None, tracker.Step(5f, true),
                "re-validating inside the window continues the SAME engagement");

            Assert.AreEqual(EngagementTracker.Transition.None, tracker.Step(7.9f, false));
            Assert.AreEqual(EngagementTracker.Transition.Exit, tracker.Step(8f, false),
                "a full τ since the last valid envelope ends the engagement");
            Assert.IsFalse(tracker.Engaged);
        }

        [Test]
        public void EngagementTracker_ReEntryAfterExitOpensANewEngagement()
        {
            var tracker = new EngagementTracker(exitSeconds: 3f);

            Assert.AreEqual(EngagementTracker.Transition.Enter, tracker.Step(1f, true));
            Assert.AreEqual(EngagementTracker.Transition.Exit, tracker.Step(4f, false), "τ elapsed with no envelope");
            Assert.IsFalse(tracker.Engaged);

            Assert.AreEqual(EngagementTracker.Transition.Enter, tracker.Step(5f, true), "a second engagement");
        }

        [Test]
        public void EngagementTracker_CloseCountsOnlyAnOpenEngagement()
        {
            var tracker = new EngagementTracker();

            Assert.IsFalse(tracker.Close(), "nothing open before any envelope");
            tracker.Step(1f, true);
            Assert.IsTrue(tracker.Close(), "an engagement open at episode end closes and counts");
            Assert.IsFalse(tracker.Engaged);
            Assert.IsFalse(tracker.Close(), "already closed");
        }

        [Test]
        public void RegenSplit_CountsOnlyRisingTicksRoutedByEngagementState()
        {
            var regen = new RegenSplit();

            regen.Observe(value: 40f, previous: 50f, engaged: true);
            regen.Observe(value: 40f, previous: 40f, engaged: true);
            Assert.AreEqual(0, regen.EngagedEvents, "damage falls and flat ticks are not regen");

            regen.Observe(value: 45f, previous: 40f, engaged: true);
            regen.Observe(value: 50f, previous: 45f, engaged: true);
            regen.Observe(value: 60f, previous: 50f, engaged: false);

            Assert.AreEqual(2, regen.EngagedEvents);
            Assert.AreEqual(10f, regen.EngagedAmount, 1e-5f);
            Assert.AreEqual(1, regen.DisengagedEvents);
            Assert.AreEqual(10f, regen.DisengagedAmount, 1e-5f);
        }

        [Test]
        public void BoostEdgeDetector_CountsOnlyTheFallingAvailabilityEdge()
        {
            var edge = new BoostEdgeDetector(available: true);

            Assert.IsTrue(edge.Step(false), "available → unavailable is an activation");
            Assert.IsFalse(edge.Step(false), "staying on cooldown is not a new activation");
            Assert.IsFalse(edge.Step(true), "cooldown ending is not an activation");
            Assert.IsTrue(edge.Step(false), "a second activation after recovery");
        }

        [Test]
        public void BoostEdgeDetector_StartingOnCooldownDoesNotCount()
        {
            var edge = new BoostEdgeDetector(available: false);

            Assert.IsFalse(edge.Step(false));
            Assert.IsFalse(edge.Step(true));
            Assert.IsTrue(edge.Step(false));
        }
    }
}
#endif
