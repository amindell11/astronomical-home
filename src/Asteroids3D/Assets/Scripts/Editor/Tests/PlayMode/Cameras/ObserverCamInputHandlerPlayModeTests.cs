using System.Collections;
using Cameras;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Substrate;

namespace Tests.PlayMode.Cameras
{
    [Category("Camera")]
    public class ObserverCamInputHandlerPlayModeTests : InputTestFixture
    {
        private Keyboard keyboard;
        private ObserverCam cam;
        private GameObject subject;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            cam = TestAssets.NewObserverCam();
            subject = new GameObject("Flight subject");
            subject.transform.position = new Vector3(12f, 8f, 0f);
            cam.SetSubject(subject.transform);
        }

        public override void TearDown()
        {
            if (cam) Object.DestroyImmediate(cam.gameObject);
            if (subject) Object.DestroyImmediate(subject);
            base.TearDown();
        }

        [UnityTest, Category("RequiresGraphics")]
        public IEnumerator F6_CyclesViews_WithAlignedAimAndIndependentMinimap()
        {
            var mapPrefab = TestAssets.Load<Camera>("Assets/Prefabs/Cameras/Minimap Cam.prefab");
            var minimap = Object.Instantiate(mapPrefab, cam.transform);
            minimap.gameObject.AddComponent<MinimapCam>();
            var uiPrefab = TestAssets.Load<Camera>("Assets/Prefabs/Cameras/UI Cam.prefab");
            var ui = Object.Instantiate(uiPrefab, cam.transform);
            var locked = cam.LockCameraToSubject;
            Assert.AreEqual(FlightView.Original, cam.View);

            foreach (var mode in new[] { FlightView.TiltedOrthographic, FlightView.TiltedPerspective, FlightView.Original })
            {
                Press(keyboard.f6Key);
                // Camera input precedes the next pose update; dependent cameras settle in LateUpdate.
                yield return null;
                yield return null;
                yield return null;
                Assert.AreEqual(mode, cam.View);
                Assert.AreEqual(mode != FlightView.TiltedPerspective, cam.Cam.orthographic);
                Assert.That(Vector3.Angle(cam.transform.forward, GamePlane.Normal),
                    Is.EqualTo(mode == FlightView.Original ? 0f : 25f).Within(0.01f));
                var playerViewport = cam.Cam.WorldToViewportPoint(subject.transform.position);
                Assert.That(Vector2.Distance(playerViewport, new Vector2(0.5f, 0.5f)), Is.LessThan(0.001f));

                foreach (var point in new[] { subject.transform.position, new Vector3(4f, 17f), new Vector3(24f, -2f) })
                {
                    var pixel = cam.Cam.WorldToScreenPoint(point);
                    pixel.z = 0f;
                    var aim = CameraUtils.GamePlanePoint(cam.Cam.ScreenPointToRay(pixel));
                    Assert.That(Vector3.Distance(aim, point), Is.LessThan(0.002f), "cursor hits the intended plane point");
                }
                Assert.That(Vector3.Angle(minimap.transform.forward, GamePlane.Normal), Is.LessThan(0.01f));
                Assert.IsTrue(minimap.orthographic);
                var mapPlayer = minimap.WorldToViewportPoint(subject.transform.position);
                Assert.That(Vector2.Distance(mapPlayer, new Vector2(0.5f, 0.5f)), Is.LessThan(0.001f));
                Assert.AreEqual(cam.Cam.orthographic, ui.orthographic);
                if (!ui.orthographic) Assert.AreEqual(cam.Cam.fieldOfView, ui.fieldOfView);
                Assert.AreEqual(locked, cam.LockCameraToSubject, "the original focus preference survives switching");
                yield return null;
                Assert.AreEqual(mode, cam.View, "holding F6 does not cycle twice");
                Release(keyboard.f6Key);
                yield return null;
            }
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
    }
}
