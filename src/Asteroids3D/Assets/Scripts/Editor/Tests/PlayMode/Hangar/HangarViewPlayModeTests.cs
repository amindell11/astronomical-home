#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Cameras;
using Game;
using NUnit.Framework;
using Ships.Loadout;
using Substrate.Sectors;
using Substrate.Services.Locales;
using Substrate.Sessions;
using Substrate.Services.Units;
using Tests.PlayMode.Common.Locales;
using UI.Hangar;
using UI.Hangar.Hardware;
using UI.Recap;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Hangar
{
    // Real PlayerRig cameras: URP render loop cannot create RTs under -nographics.
    [Category("UI")]
    [Category("RequiresGraphics")]
    public class HangarViewPlayModeTests
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string HangarCameraPrefabPath = "Assets/Prefabs/Cameras/Hangar Camera.prefab";
        private const string HangarConsolePath = "Assets/Prefabs/UI/HangarConsole.prefab";
        private const string OfferPath = "Assets/Settings/Ships/PlayerLoadout.asset";
        private const string RecapFeelPath = "Assets/Settings/UI/RecapFeel.asset";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/ArenaSector.prefab";
        private const string LocalePath = "Assets/Prefabs/Locales/IllustratedBlue.prefab";
        private const float TimeoutSec = 60f;

        private GameObject hostGo;
        private LightingSnapshot bootLighting;
        private bool savedAudioPause;

        [SetUp]
        public void SetUp()
        {
            bootLighting = LightingSnapshot.Capture();
            savedAudioPause = AudioListener.pause;
            AudioListener.pause = true;
        }

        // The host dies mid-sector without restoring the boot lighting, which later fixtures would inherit.
        [TearDown]
        public void TearDown()
        {
            if (hostGo)
            {
                hostGo.GetComponent<UnitService>().Clear();
                Object.DestroyImmediate(hostGo);
            }
            bootLighting.Restore();
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
                run = SectorRun.Create(new SectorEntry
                {
                    prefab = AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath),
                    locale = AssetDatabase.LoadAssetAtPath<LocaleSky>(LocalePath)
                }),
                presentation = true
            };
            host.playerRig = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            host.hangarCameraPrefab = AssetDatabase.LoadAssetAtPath<DriftCam>(HangarCameraPrefabPath);
            host.hangarConsolePrefab = AssetDatabase.LoadAssetAtPath<HangarConsole>(HangarConsolePath);
            host.hangarOffer = AssetDatabase.LoadAssetAtPath<ItemSubset>(OfferPath);
            host.recapFeel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>(RecapFeelPath);
            Assert.IsTrue(host.hangarCameraPrefab, $"hangar camera prefab loads from {HangarCameraPrefabPath}");
            hostGo.SetActive(true);

            HangarConsole console = null;
            var deadline = Time.realtimeSinceStartup + TimeoutSec;
            while (!console && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                console = Object.FindFirstObjectByType<HangarConsole>();
            }
            Assert.IsTrue(console, "the host reached the hangar");
            Assert.That(Object.FindObjectsByType<HangarConsole>(FindObjectsSortMode.None), Has.Length.EqualTo(1),
                "the hangar shows one console");

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
            var worn = hostGo.GetComponentInChildren<PlayerRig>().Loadout.Ship.name + "_Preview";
            Assert.That(console.Projector.Stage.GetComponentsInChildren<HangarPreviewFlight>().Select(flight => flight.name),
                Does.Contain(worn), "the projector shows the worn ship");

            console.Launch();
            deadline = Time.realtimeSinceStartup + TimeoutSec;
            while (!host.ActiveSector && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(host.ActiveSector, "Launch loaded the sector");

            Assert.IsFalse(hangarCamera.gameObject.activeSelf, "the hangar camera is off in the sector");
            Assert.IsTrue(observer.Cam.enabled, "the observer camera is back for the sector");
            // Destroy lands at the end of the frame the sector loads in.
            yield return null;
            Assert.That(Object.FindObjectsByType<HangarConsole>(FindObjectsSortMode.None), Is.Empty,
                "Launch destroys the console");
        }
    }
}
#endif
