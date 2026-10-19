#if UNITY_EDITOR
using System.Collections;
using Cameras;
using Game;
using NUnit.Framework;
using Ships.Loadout;
using Substrate.Sectors;
using Substrate.Sessions;
using Substrate.Services.Units;
using UI.Hangar;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Hangar
{
    /// <summary>
    /// The booted host shows the hangar through its drifting hangar camera with the observer camera
    /// off, and Launch hands the view back to the observer before the sector loads.
    /// </summary>
    // Real PlayerRig cameras: URP render loop cannot create RTs under -nographics.
    [Category("RequiresGraphics")]
    public class HangarViewPlayModeTests
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string HangarCameraPrefabPath = "Assets/Prefabs/Cameras/Hangar Camera.prefab";
        private const string HangarScreenPath = "Assets/Prefabs/UI/HangarScreen.prefab";
        private const string OfferPath = "Assets/Settings/Ships/PlayerLoadout.asset";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/ArenaSector.prefab";
        private const string SectorConfigPath = "Assets/Settings/Game/DefaultSectorConfig.asset";
        private const string SectorLocale = "Locale_3";
        private const float TimeoutSec = 60f;

        private GameObject hostGo;
        private Scene boot;
        private bool savedAudioPause;

        [SetUp]
        public void SetUp()
        {
            boot = SceneManager.GetActiveScene();
            savedAudioPause = AudioListener.pause;
            AudioListener.pause = true;
        }

        // The sector load applies its locale; a failed run must not leave it active for later fixtures.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (hostGo)
            {
                hostGo.GetComponent<UnitService>().Clear();
                Object.DestroyImmediate(hostGo);
            }
            SceneManager.SetActiveScene(boot);
            if (SceneManager.GetSceneByName(SectorLocale).isLoaded)
                yield return SceneManager.UnloadSceneAsync(SectorLocale);
            AudioListener.pause = savedAudioPause;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Hangar_ShowsTheDriftingHangarCamera_InPlaceOfTheObserver_UntilLaunch()
        {
            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            var host = hostGo.AddComponent<GameHost>();
            host.sessionProfile = new SessionProfile
            {
                sectorEntry = new SectorEntry
                {
                    prefab = AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath),
                    config = AssetDatabase.LoadAssetAtPath<SectorSettings>(SectorConfigPath)
                },
                presentation = true
            };
            host.playerRig = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            host.hangarCameraPrefab = AssetDatabase.LoadAssetAtPath<DriftCam>(HangarCameraPrefabPath);
            host.hangarScreenPrefab = AssetDatabase.LoadAssetAtPath<HangarScreen>(HangarScreenPath);
            host.hangarOffer = AssetDatabase.LoadAssetAtPath<ItemSubset>(OfferPath);
            Assert.IsTrue(host.hangarCameraPrefab, $"hangar camera prefab loads from {HangarCameraPrefabPath}");
            hostGo.SetActive(true);

            HangarScreen screen = null;
            var deadline = Time.realtimeSinceStartup + TimeoutSec;
            while (!screen && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                screen = Object.FindFirstObjectByType<HangarScreen>();
            }
            Assert.IsTrue(screen, "the host reached the hangar");

            var hangarCamera = hostGo.GetComponentInChildren<DriftCam>();
            var observer = hostGo.GetComponentInChildren<ObserverCam>();
            Assert.IsTrue(hangarCamera && hangarCamera.Cam.isActiveAndEnabled, "the hangar camera renders");
            Assert.IsFalse(observer.Cam.enabled, "the observer camera is off behind the hangar");

            var start = hangarCamera.transform.position;
            var driftUntil = Time.realtimeSinceStartup + 0.25f;
            while (Time.realtimeSinceStartup < driftUntil)
                yield return null;
            var moved = hangarCamera.transform.position - start;
            Assert.Greater(moved.magnitude, 0f, "the hangar camera drifts");
            Assert.AreEqual(0f, Vector3.Dot(moved, Substrate.GamePlane.Normal), 1e-4f,
                "the drift stays parallel to the game plane");

            screen.launchButton.onClick.Invoke();
            deadline = Time.realtimeSinceStartup + TimeoutSec;
            while (!host.ActiveSector && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(host.ActiveSector, "Launch loaded the sector");

            Assert.IsFalse(hangarCamera.gameObject.activeSelf, "the hangar camera is off in the sector");
            Assert.IsTrue(observer.Cam.enabled, "the observer camera is back for the sector");
        }
    }
}
#endif
