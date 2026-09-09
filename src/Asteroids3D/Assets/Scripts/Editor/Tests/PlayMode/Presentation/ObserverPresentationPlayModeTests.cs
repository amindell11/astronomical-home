#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using Cameras;
using Game;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;

namespace Tests.PlayMode
{
    /// <summary>
    /// The viewport the host builds is the only presentation the session itself spawns: with
    /// presentation off the observer camera's authored children (the starfield backdrop) go dark and
    /// the camera stops clearing to the skybox. Driven through
    /// <see cref="GameSessionHost.BuildObserver"/> on an inactive host, so no state machine runs.
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
        private bool savedPresentation;

        public override void SetUp()
        {
            base.SetUp();
            savedPresentation = GameSettings.PresentationEnabled;
        }

        public override void TearDown()
        {
            GameSettings.SetPresentationEnabled(savedPresentation);
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
        public IEnumerator PresentationOff_DarkensTheBackdrop_AndStopsCameraClearingToSkybox()
        {
            yield return BuildObserver(presentation: false);

            foreach (var renderer in BackdropRenderers())
                Assert.IsFalse(renderer.enabled,
                    $"observer-camera renderer '{renderer.name}' (starfield) still enabled with presentation off");

            Assert.AreEqual(CameraClearFlags.SolidColor, observer.Cam.clearFlags);
        }

        [UnityTest]
        public IEnumerator PresentationOn_LeavesTheBackdropAndCameraAsAuthored()
        {
            yield return BuildObserver(presentation: true);

            foreach (var renderer in BackdropRenderers())
                Assert.IsTrue(renderer.enabled, $"observer-camera renderer '{renderer.name}' darkened while presenting");

            Assert.AreEqual(CameraClearFlags.Skybox, observer.Cam.clearFlags,
                "test premise: the authored observer camera clears to the skybox");
        }

        private IEnumerator BuildObserver(bool presentation)
        {
            GameSettings.SetPresentationEnabled(presentation);

            servicesHost = new GameObject("[TestServices]");
            unitService = servicesHost.AddComponent<UnitService>();
            unitService.SetProjectiles(new ProjectileService(servicesHost.transform, presentation));

            // Inactive host: Awake and the state machine never run, so the camera build is exercised alone.
            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            var host = hostGo.AddComponent<GameSessionHost>();
            var prefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            Assert.IsNotNull(prefab, $"observer camera prefab loads from {ObserverCamPrefabPath}");
            typeof(GameSessionHost)
                .GetField("observerCamPrefab", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(host, prefab);

            observer = host.BuildObserver(unitService, presentation);
            yield return null;
        }

        private Renderer[] BackdropRenderers()
        {
            Assert.IsNotNull(observer, "test premise: the host built an observer camera");
            var renderers = observer.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers, "test premise: the observer camera prefab carries the starfield backdrop");
            return renderers;
        }
    }
}
#endif
