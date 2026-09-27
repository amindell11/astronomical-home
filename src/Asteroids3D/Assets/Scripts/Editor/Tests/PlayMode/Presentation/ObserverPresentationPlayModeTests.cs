#if UNITY_EDITOR
using System.Collections;
using Cameras;
using Game;
using NUnit.Framework;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Substrate.Services;
using Substrate.Services.Units;

namespace Tests.PlayMode
{
    /// <summary>
    /// The viewport the host builds is the only presentation the session itself spawns: with
    /// presentation off the observer camera stops seeing the locale's <c>Sky</c> layer and stops
    /// clearing to the skybox. Driven through
    /// <see cref="GameHost.BuildObserver"/> on an inactive host, so the host's flow never runs.
    /// </summary>
    [TestFixture]
    [Category("Presentation")]
    public class ObserverPresentationPlayModeTests : PlayModeWorldFixture
    {
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";

        private GameObject servicesHost;
        private GameObject hostGo;
        private UnitService unitService;
        private ObserverCam observer;

        public override void TearDown()
        {
            if (unitService) unitService.Clear();
            unitService = null;
            DestroyTestObject(observer ? observer.gameObject : null);
            observer = null;
            DestroyTestObject(hostGo);
            hostGo = null;
            DestroyTestObject(servicesHost);
            servicesHost = null;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator PresentationOff_ExcludesTheSkyLayer_AndStopsCameraClearingToSkybox()
        {
            yield return BuildObserver(presentation: false);

            Assert.IsFalse(SeesSky(), "observer camera still renders the Sky layer with presentation off");
            Assert.AreEqual(CameraClearFlags.SolidColor, observer.Cam.clearFlags);
        }

        [UnityTest]
        public IEnumerator PresentationOn_LeavesTheSkyLayerAndCameraAsAuthored()
        {
            yield return BuildObserver(presentation: true);

            Assert.IsTrue(SeesSky(), "test premise: the authored observer camera renders the Sky layer");
            Assert.AreEqual(CameraClearFlags.Skybox, observer.Cam.clearFlags,
                "test premise: the authored observer camera clears to the skybox");
        }

        private IEnumerator BuildObserver(bool presentation)
        {
            servicesHost = new GameObject("[TestServices]");
            unitService = servicesHost.AddComponent<UnitService>();
            ShipServices.Compose(unitService, servicesHost.transform, presentation);

            // Inactive host: Awake and its flow never run, so the camera build is exercised alone.
            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            var host = hostGo.AddComponent<GameHost>();
            var prefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            Assert.IsNotNull(prefab, $"observer camera prefab loads from {ObserverCamPrefabPath}");
            host.observerCamPrefab = prefab;

            observer = host.BuildObserver(unitService, presentation, servicesHost.transform);
            yield return null;
        }

        private bool SeesSky()
        {
            Assert.IsNotNull(observer, "test premise: the host built an observer camera");
            Assert.That(LayerIds.Sky, Is.GreaterThanOrEqualTo(0), "test premise: the Sky layer exists");
            return (observer.Cam.cullingMask & (1 << LayerIds.Sky)) != 0;
        }
    }
}
#endif
