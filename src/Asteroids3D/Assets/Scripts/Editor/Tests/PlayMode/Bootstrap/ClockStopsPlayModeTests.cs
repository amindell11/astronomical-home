#if UNITY_EDITOR
using NUnit.Framework;
using Game;
using UnityEngine;

namespace Tests.PlayMode.Bootstrap
{
    public class ClockStopsPlayModeTests
    {
        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test]
        public void ReleasingPause_KeepsTimeStoppedUntilThePickReleases()
        {
            Time.timeScale = .5f;
            var clock = new ClockStops();
            var pick = clock.Hold();
            var pause = clock.Hold();

            Assert.AreEqual(0f, Time.timeScale);
            pause.Dispose();
            Assert.AreEqual(0f, Time.timeScale, "the pick still holds the clock");
            Assert.IsTrue(clock.Stopped);
            pick.Dispose();
            Assert.AreEqual(.5f, Time.timeScale);
            Assert.IsFalse(clock.Stopped);
        }

        [Test]
        public void DisposingAHoldTwice_ChangesNothing()
        {
            Time.timeScale = .5f;
            var clock = new ClockStops();
            var pick = clock.Hold();
            var pause = clock.Hold();

            pause.Dispose();
            pause.Dispose();
            Assert.AreEqual(0f, Time.timeScale, "a second dispose does not release the pick");
            pick.Dispose();
            pick.Dispose();
            Assert.AreEqual(.5f, Time.timeScale);
            Assert.IsFalse(clock.Stopped);
        }

        [Test]
        public void ReleaseAll_RestoresTheSavedScale()
        {
            Time.timeScale = .5f;
            var clock = new ClockStops();
            var hold = clock.Hold();
            clock.Hold();

            clock.ReleaseAll();
            Assert.AreEqual(.5f, Time.timeScale);
            Assert.IsFalse(clock.Stopped);
            hold.Dispose();
            Assert.AreEqual(.5f, Time.timeScale, "a leftover dispose does not move the clock");
        }
    }
}
#endif
