using System;
using System.Linq;
using NUnit.Framework;
using Substrate.Sectors.Elements.Phases;

namespace Tests.EditMode
{
    /// <summary>Sector phase table time math: phases run back to back with a lull before each later phase, and the table ends done on its last phase with no trailing lull.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorPhaseScheduleEditModeTests
    {
        private static SectorPhaseSchedule Schedule(float lull, params float[] seconds) =>
            new(seconds.Select(s => new SectorPhase { seconds = s, interval = 1f, cap = 1 }).ToArray(), lull);

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
            Assert.AreEqual(1f, lull.ElapsedSeconds, 1e-4f);

            var second = schedule.At(45f);
            Assert.IsFalse(second.InLull);
            Assert.AreEqual(1, second.Index);
            Assert.AreEqual(30f, second.SecondsLeft, 1e-4f);
            Assert.AreEqual(0f, second.ElapsedSeconds, 1e-4f);
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
            Assert.AreEqual(5f, schedule.At(80f).ElapsedSeconds, 1e-4f, "Once done, the mark counts from the table's end.");
        }

        [Test]
        public void EmptyTable_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SectorPhaseSchedule(Array.Empty<SectorPhase>(), 5f));
            Assert.Throws<ArgumentException>(() => new SectorPhaseSchedule(null, 5f));
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
