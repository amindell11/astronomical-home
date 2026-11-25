using System.Collections;
using Cameras;
using NUnit.Framework;
using Substrate;
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
        private const float LeadSettleSec = 3f;
        private const float FollowThreshold = 0.1f;
        private const string FlightCameraPath = "Assets/Settings/Cameras/FlightCamera.asset";

        private Keyboard keyboard;
        private Mouse mouse;
        private ObserverCam cam;
        private GameObject subject;
        private bool previousManual;

        public override void Setup()
        {
            base.Setup();
            previousManual = CameraSettings.ManualCamera;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            cam = TestAssets.NewObserverCam();
        }

        public override void TearDown()
        {
            if (subject) Object.DestroyImmediate(subject);
            if (cam) Object.DestroyImmediate(cam.gameObject);
            CameraSettings.ManualCamera = previousManual;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator C_NoLongerSwitchesTheCameraMode()
        {
            var locked = cam.LockCameraToSubject;
            var manual = CameraSettings.ManualCamera;

            Press(keyboard.cKey);
            yield return null;
            yield return null;
            Release(keyboard.cKey);
            Press(keyboard.cKey);
            yield return null;

            Assert.AreEqual(locked, cam.LockCameraToSubject, "C no longer switches the lock");
            Assert.AreEqual(manual, CameraSettings.ManualCamera, "C no longer switches the manual camera");
        }

        [UnityTest]
        public IEnumerator ManualCamera_ScrollWheel_ZoomsOneEqualStepPerNotch()
        {
            CameraSettings.ManualCamera = true;
            yield return Scroll(1f);
            var oneNotchIn = cam.ZoomScale;
            Assert.Less(oneNotchIn, 1f, "scrolling up zooms in");

            yield return Scroll(1f);
            Assert.AreEqual(oneNotchIn * oneNotchIn, cam.ZoomScale, 1e-4f, "each notch is the same step");

            yield return Scroll(-2f);
            Assert.AreEqual(1f, cam.ZoomScale, 1e-4f, "scrolling back down undoes it");
        }

        [UnityTest]
        public IEnumerator ManualCamera_ScrollWheel_StopsAtTheZoomLimits()
        {
            CameraSettings.ManualCamera = true;
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
        public IEnumerator ManualCamera_ScrollWheel_ScalesTheAutoFitFraming()
        {
            CameraSettings.ManualCamera = true;
            subject = new GameObject("Subject");
            cam.SetSubject(subject.transform);
            cam.SetLockCameraToSubject(false);
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
        public IEnumerator ManualCamera_ScrolledFraming_HoldsUnderAManualZoom()
        {
            CameraSettings.ManualCamera = true;
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

        [UnityTest]
        public IEnumerator AutoCamera_ScrollWheel_LeavesTheZoomAlone()
        {
            CameraSettings.ManualCamera = false;
            subject = new GameObject("Subject");
            cam.SetSubject(subject.transform);
            yield return new WaitForSecondsRealtime(SettleSec);
            var settled = cam.Cam.orthographicSize;

            yield return Scroll(-3f);
            Assert.AreEqual(1f, cam.ZoomScale);
            yield return AsyncAssert.AssertRemainsFalseFor(
                () => Mathf.Abs(cam.Cam.orthographicSize - settled) > SizeTolerance,
                "the wheel moved the auto camera's zoom",
                minFrames: 30,
                minUnscaledSeconds: 0.35f);
        }

        [UnityTest]
        public IEnumerator Cursor_LeadsTheLockedCameraTowardIt_AndNothingOnceOutsideTheView()
        {
            subject = new GameObject("Subject");
            cam.SetSubject(subject.transform);
            cam.SetLockCameraToSubject(true);
            var feel = TestAssets.Load<FlightCameraProfile>(FlightCameraPath);
            var subjectCenter = GamePlane.WorldPointToPlane(subject.transform.position);

            Set(mouse.position, (Vector2)cam.Cam.ViewportToScreenPoint(new Vector3(1f, .5f)));
            yield return CameraSettlesOn(subjectCenter + GamePlane.WorldDirToPlane(GamePlane.Right) * feel.cursorLead);

            Set(mouse.position, (Vector2)cam.Cam.ViewportToScreenPoint(new Vector3(-.2f, .5f)));
            yield return CameraSettlesOn(subjectCenter);
        }

        private IEnumerator Scroll(float notches)
        {
            Set(mouse.scroll.y, notches);
            yield return null;
        }

        private IEnumerator CameraSettlesOn(Vector2 center)
        {
            float Miss() => Vector2.Distance(GamePlane.WorldPointToPlane(cam.transform.position), center);
            yield return AsyncAssert.WaitUntilThen(() => Miss() < FollowThreshold, LeadSettleSec,
                () => Assert.That(Miss(), Is.LessThan(FollowThreshold)));
        }
    }
}
