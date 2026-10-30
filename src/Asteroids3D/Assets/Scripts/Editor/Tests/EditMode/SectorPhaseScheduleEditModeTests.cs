using System;
using System.Linq;
using NUnit.Framework;
using Substrate.Sectors.Elements.Phases;

namespace Tests.EditMode
{
    /// <summary>Sector phase table time math: an opening lull leads into the first phase, phases run back to back with a lull before each later phase, and the table ends done on its last phase with no trailing lull.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorPhaseScheduleEditModeTests
    {
        private static SectorPhaseSchedule Schedule(float lull, params float[] seconds) =>
            ScheduleWithOpening(0f, lull, seconds);

        private static SectorPhaseSchedule ScheduleWithOpening(float openingLull, float lull, params float[] seconds) =>
            new(seconds.Select(s => new SectorPhase { seconds = s, interval = 1f, cap = 1 }).ToArray(), lull, openingLull);

        [Test]
        public void FirstPhase_RunsFromZero_WithNoLeadingLull()
        {
            var at = Schedule(5f, 40f, 30f).At(0f);

            Assert.AreEqual(0, at.Index);
            Assert.IsFalse(at.InLull);
            Assert.IsFalse(at.Done);
            Assert.AreEqual(40f, at.SecondsLeft, 1e-4f);
        }

        [Test]
        public void Lull_SitsBetweenPhases_AndNamesThePhaseItLeadsInto()
        {
            var schedule = Schedule(5f, 40f, 30f);

            var lull = schedule.At(41f);
            Assert.IsTrue(lull.InLull);
            Assert.AreEqual(1, lull.Index);
            Assert.AreEqual(4f, lull.SecondsLeft, 1e-4f);

            var second = schedule.At(45f);
            Assert.IsFalse(second.InLull);
            Assert.AreEqual(1, second.Index);
            Assert.AreEqual(30f, second.SecondsLeft, 1e-4f);
        }

        [Test]
        public void AfterTheLastPhase_TheTableIsDone_OnItsLastPhase()
        {
            var schedule = Schedule(5f, 40f, 30f);

            Assert.IsFalse(schedule.At(74.9f).Done);
            var done = schedule.At(75f);
            Assert.IsTrue(done.Done, "No lull follows the last phase.");
            Assert.IsFalse(done.InLull);
            Assert.AreEqual(1, done.Index);
            Assert.AreEqual(0f, done.SecondsLeft);
        }

        [Test]
        public void OpeningLull_LeadsIntoTheFirstPhase_AndShiftsEveryLaterMark()
        {
            var schedule = ScheduleWithOpening(5f, 5f, 40f, 30f);

            var opening = schedule.At(1f);
            Assert.IsTrue(opening.InLull);
            Assert.IsTrue(opening.InOpeningLull);
            Assert.IsFalse(opening.Done);
            Assert.AreEqual(0, opening.Index);
            Assert.AreEqual(4f, opening.SecondsLeft, 1e-4f);
            Assert.AreEqual(4f, opening.CountdownSeconds, 1e-4f);

            var first = schedule.At(5f);
            Assert.IsFalse(first.InLull, "The first phase opens as the opening lull ends.");
            Assert.AreEqual(0, first.Index);
            Assert.AreEqual(40f, first.SecondsLeft, 1e-4f);

            var lull = schedule.At(46f);
            Assert.IsTrue(lull.InLull);
            Assert.IsFalse(lull.InOpeningLull);
            Assert.AreEqual(1, lull.Index);
            Assert.AreEqual(4f, lull.SecondsLeft, 1e-4f);

            Assert.IsFalse(schedule.At(79.9f).Done);
            Assert.IsTrue(schedule.At(80f).Done);
        }

        [Test]
        public void EmptyTable_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SectorPhaseSchedule(Array.Empty<SectorPhase>(), 5f, 0f));
            Assert.Throws<ArgumentException>(() => new SectorPhaseSchedule(null, 5f, 0f));
        }

        [TestCase(0f, 45f)]
        [TestCase(39.5f, 5.5f)]
        [TestCase(40f, 5f)]
        [TestCase(44.5f, .5f)]
        [TestCase(45f, 35f)]
        [TestCase(80f, 20f)]
        [TestCase(100f, 0f)]
        public void Countdown_IncludesTheLull_AndEndsAtExtraction(float elapsed, float expected)
        {
            Assert.AreEqual(expected, Schedule(5f, 40f, 30f, 20f).At(elapsed).CountdownSeconds, .001f);
        }
    }
}
