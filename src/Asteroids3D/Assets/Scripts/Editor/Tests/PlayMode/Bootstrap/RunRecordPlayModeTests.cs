#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Balance;
using Cameras;
using Game;
using Game.Runs;
using NUnit.Framework;
using Ships;
using Substrate.Sectors;
using Substrate.Sessions;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Bootstrap
{
    /// <summary>
    /// A real host with presentation off, so no recap runs: the player's death appends exactly one
    /// run record to the injected path, and an unreadable build identity leaves the file unwritten
    /// while the run loop carries on.
    /// </summary>
    [Category("Bootstrap")]
    public class RunRecordPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/ArenaSector.prefab";
        private const string SectorConfigPath = "Assets/Settings/Game/DefaultSectorConfig.asset";
        private const string TestCommit = "test-commit";
        private const float StepTimeoutSec = 30f;
        private const int SettleFrames = 5;

        private static readonly Regex StatHashShape = new("^[0-9a-f]{16}$");

        private GameObject hostGo;
        private string dir;
        private string recordPath;

        public override void SetUp()
        {
            base.SetUp();
            dir = Path.Combine(Path.GetTempPath(), "run-record-host-" + Guid.NewGuid().ToString("N"));
            recordPath = Path.Combine(dir, "run-records.jsonl");
        }

        public override void TearDown()
        {
            if (hostGo) hostGo.GetComponent<UnitService>().Clear();
            DestroyTestObject(hostGo);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator PlayerDeath_AppendsExactlyOneRecord()
        {
            var sectorPrefab = AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath);
            PlayerRig rig = null;
            yield return BootToARunningSector(sectorPrefab,
                (out BuildIdentity identity, out string failure) =>
                {
                    identity = new BuildIdentity { commit = TestCommit };
                    failure = null;
                    return true;
                },
                built => rig = built);

            var ships = hostGo.GetComponent<UnitService>().ActiveRegistry.ActiveShips;
            var otherShips = ships.Count - 1;
            var killer = ships.First(ship => ship.teamNumber != rig.Player.teamNumber);
            var killerChassis = killer.name.Replace("(Clone)", string.Empty);
            var playerHash = StatHash.OfLoadout(rig.Player);

            TestDamage.Kill(rig.Player, killer);
            yield return WaitFor(() => File.Exists(recordPath), "the host appended a run record");
            for (var i = 0; i < SettleFrames; i++)
                yield return null;

            var lines = File.ReadAllLines(recordPath);
            Assert.AreEqual(1, lines.Length, "one death, one record");

            var record = JsonUtility.FromJson<RunRecord>(lines[0]);
            Assert.AreEqual(RunRecord.SchemaId, record.schema);
            Assert.AreEqual(sectorPrefab.name, record.sector);
            Assert.AreEqual(TestCommit, record.buildIdentity.commit);
            StringAssert.IsMatch(StatHashShape.ToString(), record.statFingerprint);
            Assert.AreEqual(playerHash, record.player.statHash, "the player's hash is the one taken at run begin");
            Assert.AreEqual(rig.Loadout.Ship.name, record.player.chassis);
            Assert.AreEqual(0, record.kills);

            Assert.AreEqual(otherShips, record.spawns.Count, "every sector ship is an entry and the player is not");
            foreach (var spawn in record.spawns)
                StringAssert.IsMatch(StatHashShape.ToString(), spawn.loadout.statHash);

            Assert.AreEqual("Laser", record.killingBlow.kind);
            Assert.That(record.killingBlow.spawn, Is.InRange(0, record.spawns.Count - 1), "the killing blow links to a spawn entry");
            Assert.AreEqual(killerChassis, record.spawns[record.killingBlow.spawn].loadout.chassis);
            Assert.AreEqual(1, record.damage.Count, "the one lethal source is the ledger's one row");
            Assert.AreEqual(record.killingBlow.spawn, record.damage[0].spawn);
        }

        [UnityTest]
        public IEnumerator UnreadableBuildIdentity_SkipsTheRecord_AndTheRunLoopGoesOn()
        {
            PlayerRig rig = null;
            yield return BootToARunningSector(AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath),
                (out BuildIdentity identity, out string failure) =>
                {
                    identity = default;
                    failure = "no git here";
                    return false;
                },
                built => rig = built);

            LogAssert.Expect(LogType.Error, new Regex("Run record skipped, build identity unreadable: no git here"));
            TestDamage.Kill(rig.Player);

            // The next run's hangar step revives the player, so a live player means the loop went on.
            yield return WaitFor(() => !rig.Player.gameObject.activeSelf, "the player died");
            yield return WaitFor(() => rig.Player.gameObject.activeSelf, "the host reached the next run's hangar step");
            Assert.IsFalse(File.Exists(recordPath), "no record is written without a build identity");
        }

        private IEnumerator BootToARunningSector(Sector sectorPrefab, BuildIdentitySource buildIdentity,
            Action<PlayerRig> onBuilt)
        {
            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            var host = hostGo.AddComponent<GameHost>();
            host.sessionProfile = new SessionProfile
            {
                sectorEntry = new SectorEntry
                {
                    prefab = sectorPrefab,
                    config = AssetDatabase.LoadAssetAtPath<SectorSettings>(SectorConfigPath)
                },
                presentation = false
            };
            host.playerRig = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            host.runRecordPath = recordPath;
            host.buildIdentity = buildIdentity;
            hostGo.SetActive(true);

            // The tally's clock starts once the sector load has returned, which is when a run is live.
            PlayerRig rig = null;
            yield return WaitFor(() =>
            {
                rig = hostGo.GetComponentInChildren<PlayerRig>();
                return rig && rig.Tally.SecondsSurvived > 0f;
            }, "the host started a run");
            onBuilt(rig);
        }

        private static IEnumerator WaitFor(Func<bool> condition, string what)
        {
            var deadline = Time.realtimeSinceStartup + StepTimeoutSec;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(condition(), what);
        }
    }
}
#endif
