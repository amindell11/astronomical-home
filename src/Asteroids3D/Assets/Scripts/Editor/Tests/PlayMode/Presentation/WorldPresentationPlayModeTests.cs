#if UNITY_EDITOR
using System.Collections;
using Cameras;
using Game.Services;
using NUnit.Framework;
using Player;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>
    /// Covers the interactive presentation-off path only: an RL/headless host composes no rig, so it
    /// never spawns a world or an observer camera for the policy to apply to.
    /// </summary>
    [TestFixture]
    [Category("Presentation")]
    public class WorldPresentationPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";

        private GameObject servicesHost;
        private GameServices services;
        private SessionRig rig;
        private bool savedPresentation;

        public override void SetUp()
        {
            base.SetUp();
            savedPresentation = GameSettings.PresentationEnabled;
        }

        public override void TearDown()
        {
            GameSettings.SetPresentationEnabled(savedPresentation);
            services?.ClearAll();
            services = null;
            DestroyTestObject(rig);
            rig = null;
            DestroyTestObject(servicesHost);
            servicesHost = null;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator PresentationOff_DarkensWorld_AndStopsCameraClearingToSkybox()
        {
            yield return BuildRig(presentation: false);

            foreach (var renderer in WorldRenderers())
                Assert.IsFalse(renderer.enabled,
                    $"world renderer '{renderer.name}' (starfield) still enabled with presentation off");

            Assert.AreEqual(CameraClearFlags.SolidColor, ObserverCamera().clearFlags);
        }

        [UnityTest]
        public IEnumerator PresentationOn_LeavesWorldAndCameraAsAuthored()
        {
            yield return BuildRig(presentation: true);

            foreach (var renderer in WorldRenderers())
                Assert.IsTrue(renderer.enabled, $"world renderer '{renderer.name}' darkened while presenting");

            Assert.AreEqual(CameraClearFlags.Skybox, ObserverCamera().clearFlags,
                "test premise: the authored observer camera clears to the skybox");
        }

        private IEnumerator BuildRig(bool presentation)
        {
            GameSettings.SetPresentationEnabled(presentation);

            servicesHost = new GameObject("[TestServices]");
            var unitService = servicesHost.AddComponent<UnitService>();
            var objectiveService = servicesHost.AddComponent<ObjectiveService>();
            var world = Tests.Common.TestWorld.On(unitService.Registry);
            var projectiles = new ProjectileService(servicesHost.transform, presentation);
            unitService.SetProjectiles(projectiles);

            services = new GameServices(
                unitService: unitService,
                projectiles: projectiles,
                environmentService: new EnvironmentService(servicesHost.transform, presentation),
                objectiveService: objectiveService,
                cameraService: new CameraService(),
                uiService: new UIService(),
                presentationEnabled: presentation);

            var rigPrefab = AssetDatabase.LoadAssetAtPath<SessionRig>(RigPrefabPath);
            Assert.IsNotNull(rigPrefab, $"SessionRig prefab loads from {RigPrefabPath}");
            rig = Object.Instantiate(rigPrefab);

            // No player: the world and the observer camera are the whole subject here.
            yield return rig.Build(services, buildPlayer: false, world, onPlayerDeath: null);
        }

        private Renderer[] WorldRenderers()
        {
            var world = services.EnvironmentService.World;
            Assert.IsNotNull(world, "test premise: the rig spawned a world");
            var renderers = world.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers, "test premise: the world prefab carries renderers");
            return renderers;
        }

        private Camera ObserverCamera()
        {
            var observer = services.CameraService.GetCamera<ObserverCam>(CameraTag.Observer);
            Assert.IsNotNull(observer, "test premise: the rig built an observer camera");
            return observer.Cam;
        }
    }
}
#endif
