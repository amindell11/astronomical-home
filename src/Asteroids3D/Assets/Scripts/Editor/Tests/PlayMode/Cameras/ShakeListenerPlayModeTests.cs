using System.Collections;
using System.Text.RegularExpressions;
using Cameras;
using Cameras.Shake;
using NUnit.Framework;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Cameras
{
    [Category("Camera")]
    public class ShakeListenerPlayModeTests : PlayModeWorldFixture
    {
        private ObserverCam cam;
        private ObserverCam second;
        private GameObject subject;
        private CameraShakeProfile profile;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            cam = TestAssets.NewObserverCam();
            // The editor's live mouse would move the lead.
            cam.GetComponent<ObserverCamInputHandler>().enabled = false;
            cam.transform.position = Vector3.zero;

            subject = new GameObject("Subject");
            subject.transform.position = Vector3.zero;
            cam.SetSubject(subject.transform);

            profile = ScriptableObject.CreateInstance<CameraShakeProfile>();
            profile.envelope = AnimationCurve.Constant(0f, 1f, 1f);
            profile.jitter = 0f;
            profile.roll = 0f;
            profile.kick = .1f;
            profile.innerRadius = 4f;
            profile.outerRadius = 20f;
            profile.fullSize = 0f;
        }

        [TearDown]
        public override void TearDown()
        {
            if (profile)
                Object.DestroyImmediate(profile);
            DestroyTestObject(second);
            DestroyTestObject(cam);
            DestroyTestObject(subject);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator BlastInsideInnerRadius_AddsALayer_AndShakesTheView()
        {
            ShakeListener.Hear(BlastAt(profile.innerRadius * .5f), profile);
            yield return null;
            Assert.AreEqual(1, cam.ShakeLayerCount);
            Assert.That(ViewDelta(cam.Cam.worldToCameraMatrix, UnshakenView()), Is.GreaterThan(1e-4f));
        }

        [UnityTest]
        public IEnumerator BlastBeyondOuterRadius_AddsNoLayer()
        {
            ShakeListener.Hear(BlastAt(profile.outerRadius + 1f), profile);
            yield return null;
            Assert.AreEqual(0, cam.ShakeLayerCount);
            Assert.That(ViewDelta(cam.Cam.worldToCameraMatrix, UnshakenView()), Is.LessThanOrEqualTo(1e-4f));
        }

        [Test]
        public void DisabledCamera_HearDoesNothing()
        {
            cam.enabled = false;
            Assert.DoesNotThrow(() => ShakeListener.Hear(BlastAt(profile.innerRadius * .5f), profile));
            Assert.AreEqual(0, cam.ShakeLayerCount);
        }

        [UnityTest]
        public IEnumerator SecondCamera_WhileOneIsEnabled_Throws()
        {
            LogAssert.Expect(LogType.Exception, new Regex("A shake listener is already registered"));
            second = TestAssets.NewObserverCam();

            ShakeListener.Hear(BlastAt(profile.innerRadius * .5f), profile);
            yield return null;
            Assert.AreEqual(1, cam.ShakeLayerCount);
            Assert.AreEqual(0, second.ShakeLayerCount);
        }

        private Vector3 BlastAt(float distance) => subject.transform.position + GamePlane.Right * distance;

        private Matrix4x4 UnshakenView() =>
            Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * cam.transform.worldToLocalMatrix;

        private static float ViewDelta(Matrix4x4 actual, Matrix4x4 expected)
        {
            var delta = 0f;
            for (var row = 0; row < 4; row++)
                for (var column = 0; column < 4; column++)
                    delta = Mathf.Max(delta, Mathf.Abs(actual[row, column] - expected[row, column]));
            return delta;
        }
    }
}
