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
        public void Slow_RunsAtItsScaleOfTheRunningSpeed_AndFollowsItsChanges()
        {
            Time.timeScale = .5f;
            var clock = new ClockStops();
            var slow = clock.Slow(.2f);

            Assert.AreEqual(.1f, Time.timeScale, 1e-6f);
            Assert.IsFalse(clock.Stopped, "a slow is not a stop");
            slow.Scale = .6f;
            Assert.AreEqual(.3f, Time.timeScale, 1e-6f);
            slow.Dispose();
            Assert.AreEqual(.5f, Time.timeScale);
        }

        [Test]
        public void StopOverASlow_HoldsTimeStill_AndTheSlowEndingCannotRestartIt()
        {
            Time.timeScale = 1f;
            var clock = new ClockStops();
            var slow = clock.Slow(.2f);
            var pause = clock.Hold();

            Assert.AreEqual(0f, Time.timeScale);
            slow.Scale = .8f;
            slow.Dispose();
            Assert.AreEqual(0f, Time.timeScale, "the pause still holds the clock");
            pause.Dispose();
            Assert.AreEqual(1f, Time.timeScale);
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
