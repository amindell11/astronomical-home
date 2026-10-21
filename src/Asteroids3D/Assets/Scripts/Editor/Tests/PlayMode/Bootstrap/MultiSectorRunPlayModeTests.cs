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
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Bootstrap
{
    /// <summary>
    /// A real host with presentation off over a two-sector run: extracting from the first sector loads
    /// the second without the hangar, keeping the run clock and the hull; extracting from the last
    /// records a won run and starts the next run at the first sector; dying in a later sector records
    /// the sectors cleared.
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

        private bool InSector(string templateName) =>
            host.ActiveSector && host.ActiveSector.name.StartsWith(templateName, StringComparison.Ordinal);

        private IEnumerator BootToTheFirstSector(Action<PlayerRig> onBuilt)
        {
            var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(SectorConfigPath);
            templates = new[] { NewSectorTemplate("SectorA"), NewSectorTemplate("SectorB") };

            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            host = hostGo.AddComponent<GameHost>();
            host.sessionProfile = new SessionProfile
            {
                sectors = Array.ConvertAll(templates,
                    t => new SectorEntry { prefab = t.GetComponent<Sector>(), config = config }),
                presentation = false
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
