using System.Collections;
using Cameras;
using NUnit.Framework;
using Tests.PlayMode.Common;
using Tests.PlayMode.Common.SimulatedInput;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Cameras
{
    [Category("Camera")]
    public class ObserverCamInputHandlerPlayModeTests : PlayModeInputTestFixture
    {
        private const float SettleSec = 1.5f;
        private const float SizeTolerance = 0.05f;

        private Keyboard keyboard;
        private Mouse mouse;
        private ObserverCam cam;
        private GameObject subject;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            cam = TestAssets.NewObserverCam();
        }

        public override void TearDown()
        {
            if (subject) Object.DestroyImmediate(subject);
            if (cam) Object.DestroyImmediate(cam.gameObject);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator C_TogglesLockToSubject_OncePerPress()
        {
            var locked = cam.LockCameraToSubject;

            Press(keyboard.cKey);
            yield return null;
            Assert.AreEqual(!locked, cam.LockCameraToSubject, "pressing C flips the lock");

            yield return null;
            Assert.AreEqual(!locked, cam.LockCameraToSubject, "holding C does not flip it again");

            Release(keyboard.cKey);
            yield return null;
            Press(keyboard.cKey);
            yield return null;
            Assert.AreEqual(locked, cam.LockCameraToSubject, "a fresh press flips it back");
        }

        [UnityTest]
        public IEnumerator ScrollWheel_ZoomsOneEqualStepPerNotch()
        {
            yield return Scroll(1f);
            var oneNotchIn = cam.ZoomScale;
            Assert.Less(oneNotchIn, 1f, "scrolling up zooms in");

            yield return Scroll(1f);
            Assert.AreEqual(oneNotchIn * oneNotchIn, cam.ZoomScale, 1e-4f, "each notch is the same step");

            yield return Scroll(-2f);
            Assert.AreEqual(1f, cam.ZoomScale, 1e-4f, "scrolling back down undoes it");
        }

        [UnityTest]
        public IEnumerator ScrollWheel_StopsAtTheZoomLimits()
        {
            yield return Scroll(100f);
            var closest = cam.ZoomScale;
            yield return Scroll(1f);
            Assert.AreEqual(closest, cam.ZoomScale, "scrolling past the closest zoom holds it");

            yield return Scroll(-100f);
            var widest = cam.ZoomScale;
            yield return Scroll(-1f);
            Assert.AreEqual(widest, cam.ZoomScale, "scrolling past the widest zoom holds it");
            Assert.Less(closest, 1f);
            Assert.Greater(widest, 1f);
        }

        [UnityTest]
        public IEnumerator ScrollWheel_ScalesTheAutoFitFraming()
        {
            subject = new GameObject("Subject");
            cam.SetSubject(subject.transform);
            yield return new WaitForSecondsRealtime(SettleSec);
            var autoFit = cam.Cam.orthographicSize;

            yield return Scroll(-3f);
            var expected = autoFit * cam.ZoomScale;
            Assert.Greater(expected, autoFit, "scrolling down widens the framing");
            yield return AsyncAssert.WaitUntilThen(
                () => Mathf.Abs(cam.Cam.orthographicSize - expected) < SizeTolerance,
                SettleSec,
                () => Assert.AreEqual(expected, cam.Cam.orthographicSize, SizeTolerance));
        }

        [UnityTest]
        public IEnumerator ScrolledFraming_HoldsUnderAManualZoom()
        {
            subject = new GameObject("Subject");
            cam.SetSubject(subject.transform);
            yield return Scroll(100f);
            yield return new WaitForSecondsRealtime(SettleSec);
            var scrolledSize = cam.Cam.orthographicSize;

            cam.SetManualCenter(cam.CurrentCenter2D);
            cam.SetManualZoom(scrolledSize);
            yield return AsyncAssert.AssertRemainsFalseFor(
                () => Mathf.Abs(cam.Cam.orthographicSize - scrolledSize) > SizeTolerance,
                "pinning the live size as a manual zoom, as the defeat recap does, moved the framing",
                minFrames: 30,
                minUnscaledSeconds: 0.35f);
        }

        private IEnumerator Scroll(float notches)
        {
            Set(mouse.scroll.y, notches);
            yield return null;
        }
    }
}
