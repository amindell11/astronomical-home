#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Cameras;
using Game;
using Game.Runs;
using NUnit.Framework;
using Ships.Loadout;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Services.Locales;
using Substrate.Services.Units;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using Tests.PlayMode.Common.Locales;
using UI.Hangar;
using UI.Hangar.Hardware;
using UI.Recap;
using UI.Screens;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Bootstrap
{
    [Category("Bootstrap")]
    public class TitleFlowPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string HangarCameraPrefabPath = "Assets/Prefabs/Cameras/Hangar Camera.prefab";
        private const string HangarConsolePath = "Assets/Prefabs/UI/HangarConsole.prefab";
        private const string OfferPath = "Assets/Settings/Ships/PlayerLoadout.asset";
        private const string RecapFeelPath = "Assets/Settings/UI/RecapFeel.asset";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/ArenaSector.prefab";
        private const string LocalePath = "Assets/Prefabs/Locales/IllustratedBlue.prefab";

        private GameObject hostGo;
        private GameHost host;
        private GameObject[] templates;
        private string recordPath;
        private string exportFolder;
        private string revealedExport;
        private LightingSnapshot bootLighting;

        public override void SetUp()
        {
            bootLighting = LightingSnapshot.Capture();
            base.SetUp();
        }

        public override void TearDown()
        {
            if (hostGo)
            {
                hostGo.GetComponent<UnitService>().Clear();
                DestroyTestObject(hostGo);
            }
            if (templates != null)
                foreach (var template in templates) DestroyTestObject(template);
            if (recordPath != null && File.Exists(recordPath)) File.Delete(recordPath);
            if (exportFolder != null && Directory.Exists(exportFolder)) Directory.Delete(exportFolder, true);
            bootLighting.Restore();
            base.TearDown();
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator PresentedHost_OpensOnTheTitle_AndPlayOpensTheHangar()
        {
            BootPresented();

            yield return AsyncAssert.WaitUntil(() => hostGo.GetComponentInChildren<TitleScreen>(), 60f,
                "the host opens on the title");
            Assert.IsNull(Object.FindFirstObjectByType<HangarConsole>(), "the hangar has not opened");
            var hangarCamera = hostGo.GetComponentInChildren<DriftCam>();
            Assert.IsTrue(hangarCamera && hangarCamera.Cam.isActiveAndEnabled, "the hangar camera renders");
            Assert.IsNull(host.ActiveSector);

            hostGo.GetComponentInChildren<TitleScreen>().transform.Find("Play").GetComponent<Button>().onClick.Invoke();

            yield return AsyncAssert.WaitUntil(() => hostGo.GetComponentInChildren<HangarConsole>(), 60f,
                "Play opens the hangar");
            yield return null;
            Assert.IsNull(Object.FindFirstObjectByType<TitleScreen>(), "the title is gone");
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator ExportRuns_CopiesIntoTheInjectedFolder_AndPlayOpensTheHangar()
        {
            BootPresented();
            exportFolder = Path.Combine(Path.GetTempPath(), "title-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(exportFolder);
            const string body = "{\"schema\":\"run-record-v2\"}\n";
            File.WriteAllText(recordPath, body);
            host.runExportFolder = exportFolder;
            host.revealExportedRun = path => revealedExport = path;

            yield return AsyncAssert.WaitUntil(() => hostGo.GetComponentInChildren<TitleScreen>(), 60f,
                "the host opens on the title");
            hostGo.GetComponentInChildren<TitleScreen>().transform.Find("Export").GetComponent<Button>().onClick.Invoke();

            yield return AsyncAssert.WaitUntil(ExportResultShowing, 60f, "export returns to the title");
            var titles = hostGo.GetComponentsInChildren<TitleScreen>();
            Assert.AreEqual(1, titles.Length, "the title is still up");
            Assert.IsNull(Object.FindFirstObjectByType<HangarConsole>(), "export does not open the hangar");
            Assert.AreEqual(Path.GetFullPath(exportFolder), Path.GetFullPath(Path.GetDirectoryName(revealedExport)));
            Assert.AreEqual(body, File.ReadAllText(revealedExport));
            Assert.AreEqual(body, File.ReadAllText(recordPath));
            Assert.AreEqual("Saved to Desktop: " + Path.GetFileName(revealedExport),
                titles[0].transform.Find("ExportResult").GetComponent<Text>().text);

            titles[0].transform.Find("Play").GetComponent<Button>().onClick.Invoke();

            yield return AsyncAssert.WaitUntil(() => hostGo.GetComponentInChildren<HangarConsole>(), 60f,
                "Play opens the hangar");
            yield return null;
            Assert.IsNull(Object.FindFirstObjectByType<TitleScreen>(), "the title is gone");
        }

        [UnityTest]
        public IEnumerator HeadlessHost_NeverShowsTheTitle()
        {
            BootHeadless();

            var deadline = Time.realtimeSinceStartup + 30f;
            while (!host.ActiveSector && Time.realtimeSinceStartup < deadline)
            {
                Assert.IsNull(Object.FindFirstObjectByType<TitleScreen>());
                yield return null;
            }
            Assert.IsTrue(host.ActiveSector, "the headless host reached a sector");
            Assert.IsNull(Object.FindFirstObjectByType<TitleScreen>());
        }

        private void BootPresented()
        {
            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            host = hostGo.AddComponent<GameHost>();
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
            host.openOnTitle = true;
            StampIdentity();
            hostGo.SetActive(true);
        }

        private void BootHeadless()
        {
            var locale = AssetDatabase.LoadAssetAtPath<LocaleSky>(LocalePath);
            templates = new[] { NewSectorTemplate("SectorA"), NewSectorTemplate("SectorB") };

            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            host = hostGo.AddComponent<GameHost>();
            host.sessionProfile = new SessionProfile
            {
                run = SectorRun.Create(Array.ConvertAll(templates,
                    t => new SectorEntry { prefab = t.GetComponent<Sector>(), locale = locale })),
                presentation = false
            };
            host.playerRig = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            host.openOnTitle = true;
            StampIdentity();
            hostGo.SetActive(true);
        }

        private void StampIdentity()
        {
            recordPath = Path.Combine(Path.GetTempPath(), "title-flow-" + Guid.NewGuid().ToString("N") + ".jsonl");
            host.runRecordPath = recordPath;
            host.buildIdentity = (out BuildIdentity identity, out string failure) =>
            {
                identity = new BuildIdentity { commit = "test-commit" };
                failure = null;
                return true;
            };
        }

        private bool ExportResultShowing()
        {
            var titles = hostGo.GetComponentsInChildren<TitleScreen>();
            if (titles.Length != 1)
                return false;
            var result = titles[0].transform.Find("ExportResult");
            return result && result.GetComponent<Text>().text.StartsWith("Saved to Desktop:");
        }

        // Inactive, so the template never runs; the session instantiates it like a sector prefab.
        private static GameObject NewSectorTemplate(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var sector = go.AddComponent<Sector>();
            sector.SetManifest(null, null, new SectorModule[] { go.AddComponent<ExtractOnCueModule>() });
            return go;
        }
    }
}
#endif
