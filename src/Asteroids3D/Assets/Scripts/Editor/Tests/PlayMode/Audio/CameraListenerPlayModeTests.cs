#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections;
using Audio;
using Cameras;
using FMODUnity;
using NUnit.Framework;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Audio
{
    [Category("Audio")]
    public class CameraListenerPlayModeTests : FmodEvidenceFixture
    {
        private ObserverCam cam;
        private GameObject subject;
        private SimulationMode simulationMode;
        private bool previousManual;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            previousManual = CameraSettings.ManualCamera;
            CameraSettings.ManualCamera = false;
            simulationMode = Physics.simulationMode;
            cam = TestAssets.NewObserverCam();
            cam.GetComponent<ObserverCamInputHandler>().enabled = false;
            cam.transform.position = Vector3.zero;
        }

        [TearDown]
        public override void TearDown()
        {
            DestroyTestObject(cam);
            DestroyTestObject(subject);
            Physics.simulationMode = simulationMode;
            CameraSettings.ManualCamera = previousManual;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Follow_SendsTheCamerasVelocity()
        {
            Assert.IsNotNull(cam.GetComponentInChildren<CameraListener>(true),
                "the camera prefab carries a CameraListener");

            Physics.simulationMode = SimulationMode.Script;
            subject = new GameObject("Subject");
            subject.transform.position = Vector3.zero;
            var body = subject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = new Vector3(12f, -5f, 0f);
            cam.SetSubject(subject.transform, body);
            cam.ResumeFollow();

            for (var i = 0; i < 5; i++)
                yield return null;

            RuntimeManager.StudioSystem.flushCommands();
            RuntimeManager.StudioSystem.getListenerAttributes(0, out var attributes);
            var heard = new Vector3(attributes.velocity.x, attributes.velocity.y, attributes.velocity.z);
            var inPlane = GamePlane.PlaneDirToWorld(GamePlane.WorldDirToPlane(cam.Velocity));
            Debug.Log($"[FMOD] listener velocity={heard} camera velocity={cam.Velocity}");
            Assert.Greater(inPlane.sqrMagnitude, 0f, "the camera moves across the plane");
            Assert.Greater(Mathf.Abs(Vector3.Dot(cam.Velocity, GamePlane.Normal)), 0f,
                "the camera also climbs to its follow height, which the listener must not hear");
            Assert.AreEqual(inPlane.x, heard.x, 1e-3f, "listener velocity");
            Assert.AreEqual(inPlane.y, heard.y, 1e-3f, "listener velocity");
            Assert.AreEqual(inPlane.z, heard.z, 1e-3f, "listener velocity");
        }
    }
}
#endif
