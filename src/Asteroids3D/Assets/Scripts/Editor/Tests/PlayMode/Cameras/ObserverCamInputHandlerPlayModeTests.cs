using System.Collections;
using Cameras;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Cameras
{
    [Category("Camera")]
    public class ObserverCamInputHandlerPlayModeTests : InputTestFixture
    {
        private Keyboard keyboard;
        private ObserverCam cam;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            cam = TestAssets.NewObserverCam();
        }

        public override void TearDown()
        {
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
    }
}
