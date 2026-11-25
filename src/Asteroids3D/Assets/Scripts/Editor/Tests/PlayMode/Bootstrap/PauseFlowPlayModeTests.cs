#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Cameras;
using Game;
using Game.Player;
using Game.Runs;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Services.Locales;
using Substrate.Services.Units;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using Tests.PlayMode.Common.Locales;
using Tests.PlayMode.Common.SimulatedInput;
using UI.Recap;
using UI.Screens;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode.Bootstrap
{
    /// <summary>
    /// A presenting host over two sectors, opened from the title with a HUD-less rig: pause holds the
    /// flight, quit run returns to the title with an abandoned record, and pause stays out of the recap.
    /// </summary>
    [Category("Bootstrap")]
    public class PauseFlowPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string LocalePath = "Assets/Prefabs/Locales/IllustratedBlue.prefab";
        private const string RecapFeelPath = "Assets/Settings/UI/RecapFeel.asset";
        private const float StepTimeoutSec = 30f;

        private readonly PlayModeInputTestFixture input = new();
        private GameObject hostGo;
        private GameHost host;
        private GameObject[] templates;
        private GameObject rigTemplateRoot;
        private PlayerRig rig;
        private Keyboard keyboard;
        private string dir;
        private string recordPath;
        private LightingSnapshot bootLighting;
        private bool assistHad;
        private int assistStored;

        public override void SetUp()
        {
            assistHad = PlayerPrefs.HasKey("flight.assist");
            if (assistHad) assistStored = PlayerPrefs.GetInt("flight.assist");
            input.Setup();
            base.SetUp();
            bootLighting = LightingSnapshot.Capture();
            dir = Path.Combine(Path.GetTempPath(), "pause-flow-" + Guid.NewGuid().ToString("N"));
            recordPath = Path.Combine(dir, "run-records.jsonl");
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            if (assistHad) PlayerPrefs.SetInt("flight.assist", assistStored);
            else PlayerPrefs.DeleteKey("flight.assist");
            PlayerPrefs.Save();
            if (hostGo) hostGo.GetComponent<UnitService>().Clear();
            DestroyTestObject(hostGo);
            if (templates != null)
                foreach (var template in templates) DestroyTestObject(template);
            DestroyTestObject(rigTemplateRoot);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            bootLighting.Restore();
            Time.timeScale = 1f;
            base.TearDown();
            input.TearDown();
        }

        [UnityTest]
        public IEnumerator Escape_PausesFlight_AndResumeRestoresIt()
        {
            yield return BootToFlight();
            var sector = host.ActiveSector;
            Assert.IsTrue(rig.Player.Commander.enabled, "flight starts under the player's control");

            TapEscape();
            yield return WaitFor(() => hostGo.GetComponentInChildren<PauseScreen>(), "escape opens pause");
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(rig.Player.Commander.enabled);
            AssertWorldPaused(true);

            hostGo.GetComponentInChildren<PauseScreen>().transform.Find("Resume").GetComponent<Button>().onClick.Invoke();
            yield return WaitFor(() => !hostGo.GetComponentInChildren<PauseScreen>(), "resume closes pause");
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(rig.Player.Commander.enabled);
            Assert.AreSame(sector, host.ActiveSector);
            AssertWorldPaused(false);
        }

        [UnityTest]
        public IEnumerator QuitRun_LandsOnTheTitle_AndAppendsAnAbandonedRecord()
        {
            yield return BootToFlight();

            TapEscape();
            yield return WaitFor(() => hostGo.GetComponentInChildren<PauseScreen>(), "escape opens pause");
            hostGo.GetComponentInChildren<PauseScreen>().transform.Find("QuitRun").GetComponent<Button>().onClick.Invoke();

            yield return WaitFor(() => hostGo.GetComponentInChildren<TitleScreen>(), "quit run returns to the title");
            Assert.IsNull(host.ActiveSector);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(hostGo.GetComponentInChildren<RunRecapScreen>(), "an abandoned run has no recap");
            var lines = File.ReadAllLines(recordPath);
            Assert.AreEqual(1, lines.Length);
            var record = JsonUtility.FromJson<RunRecord>(lines[0]);
            Assert.AreEqual(RunRecord.Abandoned, record.outcome);
            Assert.AreEqual(string.Empty, record.killingBlow.kind);
            Assert.AreEqual(RunRecord.NoSpawn, record.killingBlow.spawn);
        }

        [UnityTest]
        public IEnumerator Pause_DoesNotOpenOverTheRecap()
        {
            yield return BootToFlight();
            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return WaitFor(() => hostGo.GetComponentInChildren<RunRecapScreen>(), "extraction opens the recap");

            TapEscape();
            for (var frame = 0; frame < 5; frame++) yield return null;
            Assert.IsFalse(hostGo.GetComponentInChildren<PauseScreen>());
        }

        [UnityTest]
        public IEnumerator Settings_FromPause_ReturnsToPause()
        {
            yield return BootToFlight();

            TapEscape();
            yield return WaitFor(() => hostGo.GetComponentInChildren<PauseScreen>(), "escape opens pause");
            hostGo.GetComponentInChildren<PauseScreen>().transform.Find("Settings").GetComponent<Button>().onClick.Invoke();

            yield return WaitFor(() => hostGo.GetComponentInChildren<SettingsScreen>()
                && !hostGo.GetComponentInChildren<PauseScreen>(), "settings replaces pause");
            Assert.AreEqual(0f, Time.timeScale);
            hostGo.GetComponentInChildren<SettingsScreen>().Close();

            yield return WaitFor(() => hostGo.GetComponentInChildren<PauseScreen>(), "closing settings returns to pause");
            Assert.AreEqual(0f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator AssistToggle_InSettings_SetsTheLiveShip()
        {
            AssistSetting.Set(true);
            yield return BootToFlight();

            TapEscape();
            yield return WaitFor(() => hostGo.GetComponentInChildren<PauseScreen>(), "escape opens pause");
            hostGo.GetComponentInChildren<PauseScreen>().transform.Find("Settings").GetComponent<Button>().onClick.Invoke();
            yield return WaitFor(() => hostGo.GetComponentInChildren<SettingsScreen>()
                && !hostGo.GetComponentInChildren<PauseScreen>(), "settings replaces pause");

            var row = FlightAssistRow(hostGo.GetComponentInChildren<SettingsScreen>());
            Assert.IsNotNull(row);
            Assert.AreEqual("ON", row.Find("Value").GetComponent<Text>().text);
            Assert.IsTrue(AssistOn(rig));
            row.Find("Control").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("OFF", row.Find("Value").GetComponent<Text>().text);
            Assert.IsFalse(AssistOn(rig));
        }

        [UnityTest]
        public IEnumerator AssistOff_BeforeBoot_StartsTheFirstSectorOff()
        {
            AssistSetting.Set(false);
            yield return BootToFlight();
            Assert.IsFalse(AssistOn(rig));
        }

        private static Transform FlightAssistRow(SettingsScreen settings)
        {
            for (var i = 0; ; i++)
            {
                var row = settings.transform.Find("Row" + i);
                if (!row) return null;
                if (row.Find("Label").GetComponent<Text>().text == "FLIGHT ASSIST") return row;
            }
        }

        private static bool AssistOn(PlayerRig playerRig)
        {
            var commander = playerRig.Player.Commander as PlayerCommander;
            Assert.IsNotNull(commander);
            return (bool)typeof(PlayerCommander)
                .GetField("assistOn", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(commander);
        }

        private void TapEscape()
        {
            input.Press(keyboard.escapeKey);
            InputSystem.Update();
            input.Release(keyboard.escapeKey);
            InputSystem.Update();
        }

        private static void AssertWorldPaused(bool expected)
        {
#if ASTRO_FMOD
            FMODUnity.RuntimeManager.StudioSystem.flushCommands();
            FMODUnity.RuntimeManager.GetBus(Game.Audio.MixBuses.World).getPaused(out var paused);
            Assert.AreEqual(expected, paused);
#else
            _ = expected;
#endif
        }

        private IEnumerator BootToFlight()
        {
            var locale = AssetDatabase.LoadAssetAtPath<LocaleSky>(LocalePath);
            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            templates = new[] { NewSectorTemplate("SectorA"), NewSectorTemplate("SectorB") };
            foreach (var template in templates) template.transform.SetParent(hostGo.transform);
            host = hostGo.AddComponent<GameHost>();
            host.sessionProfile = new SessionProfile
            {
                run = SectorRun.Create(Array.ConvertAll(templates, t => new SectorEntry
                {
                    prefab = t.GetComponent<Sector>(),
                    locale = locale
                })),
                presentation = true
            };
            host.playerRig = HudlessRig();
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            host.recapFeel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>(RecapFeelPath);
            host.runRecordPath = recordPath;
            host.openOnTitle = true;
            host.titleFont = TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/Michroma.ttf");
            host.titleLabelFont = TestAssets.Load<Font>("Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf");
            host.buildIdentity = (out BuildIdentity identity, out string failure) =>
            {
                identity = new BuildIdentity { commit = "test-commit" };
                failure = null;
                return true;
            };
            hostGo.SetActive(true);

            yield return WaitFor(() => hostGo.GetComponentInChildren<TitleScreen>(), "the host opens on the title");
            hostGo.GetComponentInChildren<TitleScreen>().transform.Find("Play").GetComponent<Button>().onClick.Invoke();
            yield return WaitFor(() =>
            {
                rig = hostGo.GetComponentInChildren<PlayerRig>();
                return rig && rig.Player && rig.Tally.SecondsSurvived > 0f && InSector("SectorA");
            }, "the host started a run in the first sector");
        }

        // Under an inactive root, so the template never wakes; with no overlay the rig builds no HUD cameras.
        private PlayerRig HudlessRig()
        {
            rigTemplateRoot = new GameObject("RigTemplate");
            rigTemplateRoot.SetActive(false);
            var template = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath), rigTemplateRoot.transform);
            using var serialized = new SerializedObject(template);
            serialized.FindProperty("overlayPrefab").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return template;
        }

        private static GameObject NewSectorTemplate(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var sector = go.AddComponent<Sector>();
            sector.SetManifest(null, null, new SectorModule[] { go.AddComponent<ExtractOnCueModule>() });
            return go;
        }

        private bool InSector(string templateName) =>
            host.ActiveSector && host.ActiveSector.name.StartsWith(templateName, StringComparison.Ordinal);

        private static IEnumerator WaitFor(Func<bool> condition, string what) =>
            AsyncAssert.WaitUntil(condition, StepTimeoutSec, what);
    }
}
#endif
