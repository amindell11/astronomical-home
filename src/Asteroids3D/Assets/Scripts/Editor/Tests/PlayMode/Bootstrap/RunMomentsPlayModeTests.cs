#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Cameras;
using Damage;
using Game;
using Game.Moments;
using Game.Runs;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Services.Locales;
using Substrate.Sessions;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Bootstrap
{
    /// <summary>
    /// A real host with presentation off: each run gets its own moment stream, which opens on a run start
    /// read from the run-record store, then the first sector's entry, and carries a close call and the
    /// player's death.
    /// </summary>
    [Category("Bootstrap")]
    public class RunMomentsPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/ArenaSector.prefab";
        private const string LocalePath = "Assets/Prefabs/Locales/IllustratedBlue.prefab";
        private const float StepTimeoutSec = 30f;

        private static readonly FieldInfo MomentsField =
            typeof(GameHost).GetField("moments", BindingFlags.Instance | BindingFlags.NonPublic);

        private GameObject hostGo;
        private string dir;
        private string recordPath;

        public override void SetUp()
        {
            base.SetUp();
            dir = Path.Combine(Path.GetTempPath(), "run-moments-host-" + Guid.NewGuid().ToString("N"));
            recordPath = Path.Combine(dir, "run-records.jsonl");
        }

        public override void TearDown()
        {
            if (hostGo) hostGo.GetComponent<UnitService>().Clear();
            DestroyTestObject(hostGo);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            base.TearDown();
        }

        private static string Died(int kills, string killedBy) => new RunRecord
        {
            schema = RunRecord.SchemaId,
            endedUtc = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc)
                .ToString(RunRecord.EndedUtcFormat, CultureInfo.InvariantCulture),
            outcome = RunRecord.Died,
            sectorsCleared = 0,
            kills = kills,
            killingBlow = new KillingBlowRow { kind = killedBy, spawn = RunRecord.NoSpawn },
        }.ToJsonLine();

        [UnityTest]
        public IEnumerator EachRun_StreamsItsStartEntryCloseCallAndThePlayersDeath()
        {
            Directory.CreateDirectory(dir);
            File.WriteAllLines(recordPath, new[] { Died(1, "Laser"), Died(5, "Missile") });

            PlayerRig rig = null;
            yield return BootToARunningSector(built => rig = built);
            var host = hostGo.GetComponent<GameHost>();
            var first = (RunMomentStream)MomentsField.GetValue(host);

            var start = (RunStarted)first.Moments[0];
            Assert.AreEqual(3, start.CloneNumber, "two past runs make this the third clone");
            var last = start.Last.Value;
            Assert.IsTrue(last.Died);
            Assert.AreEqual("Missile", last.KillingBlowKind);
            Assert.AreEqual(1, last.Sector);
            Assert.IsTrue(last.WasBest, "the seeded last run outranks the one before it");
            Assert.AreEqual(1, ((SectorEntered)first.Moments[1]).Sector);

            var hull = rig.Player.Damage.Health;
            hull.ApplyDamage(hull.CurrentValue - .1f * hull.MaxValue);
            hull.ApplyDamage(.05f * hull.MaxValue);
            Assert.AreEqual(1, first.Moments.OfType<CloseCall>().Single().Sector, "one dip, one close call");

            var ships = hostGo.GetComponent<UnitService>().ActiveRegistry.ActiveShips;
            TestDamage.Kill(rig.Player, ships.First(ship => ship.teamNumber != rig.Player.teamNumber));
            var death = first.Moments.OfType<PlayerDied>().Single();
            Assert.AreEqual(1, death.Sector);
            Assert.AreEqual(DamageKind.Laser, death.KilledBy);

            RunMomentStream next = null;
            yield return WaitFor(() =>
            {
                next = (RunMomentStream)MomentsField.GetValue(host);
                return next != first && next.Moments.Count > 0;
            }, "the next run opened its own moment stream");
            var nextStart = (RunStarted)next.Moments[0];
            Assert.AreEqual(4, nextStart.CloneNumber, "the death's record counts as a past run");
            Assert.IsTrue(nextStart.Last.Value.Died);
            Assert.AreEqual("Laser", nextStart.Last.Value.KillingBlowKind);
            Assert.AreEqual(1, nextStart.Last.Value.Sector);
        }

        private IEnumerator BootToARunningSector(Action<PlayerRig> onBuilt)
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
