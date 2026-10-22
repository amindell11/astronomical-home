#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Cameras;
using Game;
using Game.Runs;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sessions;
using Substrate.Services.Units;
using UI.Screens;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode.Bootstrap
{
    /// <summary>
    /// A real host over two sectors checks run records, carried hull and the return to play.
    /// Graphics cases cover recap timing, death effects, HUD visibility and camera restoration.
    /// </summary>
    [Category("Bootstrap")]
    public class MultiSectorRunPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string SectorConfigPath = "Assets/Settings/Game/DefaultSectorConfig.asset";
        private const float StepTimeoutSec = 30f;

        private GameObject hostGo;
        private GameHost host;
        private GameObject[] templates;
        private string dir;
        private string recordPath;

        public override void SetUp()
        {
            base.SetUp();
            dir = Path.Combine(Path.GetTempPath(), "multi-sector-run-" + Guid.NewGuid().ToString("N"));
            recordPath = Path.Combine(dir, "run-records.jsonl");
        }

        public override void TearDown()
        {
            if (hostGo) hostGo.GetComponent<UnitService>().Clear();
            DestroyTestObject(hostGo);
            if (templates != null)
                foreach (var template in templates) DestroyTestObject(template);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator ExtractingChainsTheSectors_KeepingClockAndHull_AndTheLastOneWins()
        {
            PlayerRig rig = null;
            yield return BootToTheFirstSector(built => rig = built);

            rig.Player.Damage.Health.SetFraction(0.4f);
            var clockBefore = rig.Tally.SecondsSurvived;
            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();

            yield return WaitFor(() => InSector("SectorB"), "extracting from the first sector loads the second");
            // The load resets the ship first; the host puts the run's hull back once the load returns.
            yield return WaitFor(() => Mathf.Abs(rig.Player.Damage.Health.Pct - 0.4f) < 1e-3f,
                "hull carries into the next sector");
            Assert.IsTrue(rig.Player.gameObject.activeSelf, "the player flies on without a hangar stop");
            Assert.Greater(rig.Tally.SecondsSurvived, clockBefore, "the run clock keeps running across sectors");
            Assert.IsFalse(File.Exists(recordPath), "no record until the run ends");

            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return WaitFor(() => File.Exists(recordPath), "extracting from the last sector records the run");
            var record = JsonUtility.FromJson<RunRecord>(File.ReadAllLines(recordPath)[0]);
            Assert.AreEqual(RunRecord.Extracted, record.outcome);
            Assert.AreEqual(2, record.sectorsCleared);
            Assert.AreEqual("SectorB", record.sector);
            Assert.AreEqual(RunRecord.NoSpawn, record.killingBlow.spawn);

            yield return WaitFor(() => InSector("SectorA"), "the next run starts at the first sector");
            Assert.AreEqual(1f, rig.Player.Damage.Health.Pct, 1e-3f, "a new run starts at full hull");
        }

        [UnityTest]
        public IEnumerator DyingInALaterSector_RecordsTheSectorsCleared()
        {
            PlayerRig rig = null;
            yield return BootToTheFirstSector(built => rig = built);

            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return WaitFor(() => InSector("SectorB"), "extracting from the first sector loads the second");

            TestDamage.Kill(rig.Player);
            yield return WaitFor(() => File.Exists(recordPath), "the death records the run");
            var record = JsonUtility.FromJson<RunRecord>(File.ReadAllLines(recordPath)[0]);
            Assert.AreEqual(RunRecord.Died, record.outcome);
            Assert.AreEqual(1, record.sectorsCleared);
            Assert.AreEqual("SectorB", record.sector);
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator Recaps_PauseBeforeUnloading_AndFinalExtractionShowsVictoryDirectly()
        {
            PlayerRig rig = null;
            yield return BootToTheFirstSector(built => rig = built, presentation: true);
            rig.Player.Damage.Health.SetFraction(.4f);
            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return WaitFor(() => hostGo.GetComponentInChildren<RunRecapScreen>(), "sector recap appears");
            var recap = hostGo.GetComponentInChildren<RunRecapScreen>();
            Assert.AreEqual("EXTRACTED", recap.transform.Find("Panels/Recap/Outcome").GetComponent<Text>().text);
            Assert.IsTrue(InSector("SectorA"));
            Assert.AreEqual(0f, Time.timeScale);
            var elapsed = rig.Tally.SecondsSurvived;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(elapsed, rig.Tally.SecondsSurvived);
            Assert.IsFalse(File.Exists(recordPath));
            recap.transform.Find("Panels/Recap/Continue").GetComponent<Button>().onClick.Invoke();

            yield return WaitFor(() => InSector("SectorB") && Mathf.Abs(rig.Player.Damage.Health.Pct - .4f) < 1e-3f,
                "next sector finishes loading after dismissal");
            Assert.AreEqual(1f, Time.timeScale);
            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return WaitFor(() => File.Exists(recordPath), "victory is recorded");
            Assert.AreEqual(0f, Time.timeScale, "victory freezes combat before fading the HUD");
            yield return WaitFor(() => hostGo.GetComponentInChildren<RunRecapScreen>(), "final recap appears");
            recap = hostGo.GetComponentInChildren<RunRecapScreen>();
            Assert.AreEqual("VICTORY", recap.transform.Find("Panels/Recap/Outcome").GetComponent<Text>().text);
            Assert.IsNotNull(recap.transform.Find("Panels/Recap/Scoreboard/CurrentRun"));
            Assert.AreEqual(1, File.ReadAllLines(recordPath).Length);
            UnityEngine.Object.Destroy(hostGo);
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, "tearing down a paused host restores the clock");
        }

        private bool InSector(string templateName) =>
            host.ActiveSector && host.ActiveSector.name.StartsWith(templateName, StringComparison.Ordinal);

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator Death_FadesHudAndFinishesEffectsBeforePausing_ThenRestoresTheNextRun()
        {
            PlayerRig rig = null;
            yield return BootToTheFirstSector(built => rig = built, presentation: true);
            var observer = hostGo.GetComponentInChildren<ObserverCam>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                TestDamage.Kill(rig.Player);
                yield return null;
                var cameraPosition = observer.transform.position;
                Assert.IsTrue(rig.Player.VisualRig.DeathEffectsPlaying, "the real death effects started");
                yield return new WaitForSecondsRealtime(.4f);
                Assert.AreEqual(0f, rig.Overlay.GetComponent<CanvasGroup>().alpha);
                Assert.IsFalse(observer.enabled);
                Assert.AreEqual(cameraPosition, observer.transform.position);
                Assert.AreEqual(1f, Time.timeScale, "the explosion keeps simulating");
                Assert.IsNull(hostGo.GetComponentInChildren<RunRecapScreen>());
                var deadline = Time.realtimeSinceStartup + 15f;
                while (rig.Player.VisualRig.DeathEffectsPlaying && Time.realtimeSinceStartup < deadline)
                {
                    Assert.AreEqual(1f, Time.timeScale);
                    Assert.IsNull(hostGo.GetComponentInChildren<RunRecapScreen>());
                    yield return null;
                }
                Assert.IsFalse(rig.Player.VisualRig.DeathEffectsPlaying);
                yield return WaitFor(() => hostGo.GetComponentInChildren<RunRecapScreen>(), "death recap appears");
                var recap = hostGo.GetComponentInChildren<RunRecapScreen>();
                Assert.AreEqual(0f, Time.timeScale);
                yield return new WaitForSecondsRealtime(1.2f);
                var details = recap.transform.Find("Panels/Recap").GetComponent<CanvasGroup>();
                Assert.AreEqual(0f, details.alpha);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitFor(() => details.interactable, "keypress reveals the breakdown");
                recap.transform.Find("Panels/Recap/Continue").GetComponent<Button>().onClick.Invoke();
                yield return WaitFor(() => rig.Player.gameObject.activeInHierarchy && rig.Tally.SecondsSurvived > 0f,
                    "the next run starts");
                Assert.AreEqual(1f, Time.timeScale);
                Assert.IsTrue(observer.enabled);
                Assert.AreEqual(1f, rig.Overlay.GetComponent<CanvasGroup>().alpha);
                Assert.IsTrue(rig.Overlay.GetComponent<Canvas>().enabled);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        private IEnumerator BootToTheFirstSector(Action<PlayerRig> onBuilt, bool presentation = false)
        {
            var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(SectorConfigPath);
            templates = new[] { NewSectorTemplate("SectorA"), NewSectorTemplate("SectorB") };

            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            foreach (var template in templates) template.transform.SetParent(hostGo.transform);
            host = hostGo.AddComponent<GameHost>();
            host.sessionProfile = new SessionProfile
            {
                sectors = Array.ConvertAll(templates,
                    t => new SectorEntry { prefab = t.GetComponent<Sector>(), config = config }),
                presentation = presentation
            };
            host.playerRig = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            host.runRecordPath = recordPath;
            host.buildIdentity = (out BuildIdentity identity, out string failure) =>
            {
                identity = new BuildIdentity { commit = "test-commit" };
                failure = null;
                return true;
            };
            hostGo.SetActive(true);

            PlayerRig rig = null;
            yield return WaitFor(() =>
            {
                rig = hostGo.GetComponentInChildren<PlayerRig>();
                return rig && rig.Tally.SecondsSurvived > 0f && InSector("SectorA");
            }, "the host started a run in the first sector");
            onBuilt(rig);
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

        private static IEnumerator WaitFor(Func<bool> condition, string what) =>
            AsyncAssert.WaitUntil(condition, StepTimeoutSec, what);
    }
}
#endif
