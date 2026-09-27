using NUnit.Framework;
using Substrate.Sectors.Elements;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Wave director pacing with injected time: the interval gates cadence, the alive cap gates count, and both ease linearly to their end values over the ramp.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class WaveDirectorEditModeTests
    {
        private const float StartInterval = 6f;
        private const float EndInterval = 2f;
        private const int StartCap = 2;
        private const int EndCap = 8;
        private const float Ramp = 100f;

        private GameObject _go;
        private WaveDirector _director;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("WaveDirector");
            _director = _go.AddComponent<WaveDirector>();
            _director.Configure(null, StartInterval, EndInterval, StartCap, EndCap, Ramp);
            _director.BeginSchedule(0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
        }

        [Test]
        public void Cadence_NextSpawnWaitsOneInterval()
        {
            Assert.IsFalse(_director.SpawnDue(StartInterval - 0.01f, 0), "The opening wave covers the first interval.");
            Assert.IsTrue(_director.SpawnDue(StartInterval, 0));

            _director.ScheduleNext(StartInterval);
            Assert.IsFalse(_director.SpawnDue(StartInterval + 0.01f, 0), "A spawn restarts the interval.");
            var next = StartInterval + _director.IntervalAt(StartInterval);
            Assert.IsTrue(_director.SpawnDue(next, 0));
        }

        [Test]
        public void Cap_HoldsSpawns_UntilOneDies()
        {
            Assert.IsFalse(_director.SpawnDue(StartInterval, StartCap), "At the cap, a due spawn must hold.");
            Assert.IsTrue(_director.SpawnDue(StartInterval + 1f, StartCap - 1), "A death under the cap releases the held spawn.");
        }

        [Test]
        public void Escalation_EasesLinearly_AndClampsAtTheRamp()
        {
            Assert.AreEqual(StartInterval, _director.IntervalAt(0f), 1e-4f);
            Assert.AreEqual(4f, _director.IntervalAt(Ramp / 2f), 1e-4f);
            Assert.AreEqual(EndInterval, _director.IntervalAt(Ramp), 1e-4f);
            Assert.AreEqual(EndInterval, _director.IntervalAt(Ramp * 3f), 1e-4f, "The interval floors at its end value.");

            Assert.AreEqual(StartCap, _director.CapAt(0f));
            Assert.AreEqual(5, _director.CapAt(Ramp / 2f));
            Assert.AreEqual(EndCap, _director.CapAt(Ramp * 3f), "The cap ceilings at its end value.");
        }
    }
}
