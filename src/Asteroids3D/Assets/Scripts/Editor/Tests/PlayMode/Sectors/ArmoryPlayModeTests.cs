#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ships;
using Substrate;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Stations;
using Substrate.Services.Objectives;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Tests.PlayMode.Sectors
{
    /// <summary>The shipped armory prefab: a one-row run at sector index 0 places the one armory its row asks for; every fly-in counts a claim, only a take spends the armory and darkens it, the hunt's release darkens an untaken one, and it picks its point spaced from the repair and mod stations.</summary>
    [TestFixture]
    [Category("Sectors")]
    [Category("RequiresLfsPng")]
    public class ArmoryPlayModeTests : PlayModeWorldFixture
    {
        private const string ArmoryPath = "Assets/Prefabs/Objectives/Armory.prefab";
        private const string RepairStationPath = "Assets/Prefabs/Objectives/Repair Station.prefab";
        private const string ModStationPath = "Assets/Prefabs/Objectives/Mod Station.prefab";
        private const float Distance = 40f;
        private const float Spacing = 20f;
        private const float AlarmGrace = 5f;

        private readonly List<Object> _created = new();
        private readonly List<string> _beats = new();
        private ObjectiveService _objectives;
        private Sector _sector;
        private float alarmAt;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _objectives = Track(new GameObject("Objectives")).AddComponent<ObjectiveService>();
            _sector = Track(new GameObject("Sector")).AddComponent<Sector>();
        }

        [TearDown]
        public override void TearDown()
        {
            foreach (var item in _created)
                if (item) Object.DestroyImmediate(item);
            _created.Clear();
            _beats.Clear();
            base.TearDown();
        }

        [Test]
        public void AOneRowRun_AtSectorIndexZero_PlacesTheOneArmoryItsRowAsksFor()
        {
            var hero = SpawnHero();
            var run = Track(SectorRun.Create(new SectorEntry { armories = 1 }));
            var armory = NewArmory();
            Drive(armory.Setup(Context(hero, 0, run.Sectors[0])));
            Assert.AreEqual(1, run.Sectors.Count);
            Assert.AreEqual(1, armory.Stations.Count, "Sector index 0 places the row's one armory.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator EachFlyIn_Claims_AndOnlyATakeSpendsTheArmory()
        {
            var hero = SpawnHero();
            var armory = NewArmory();
            Drive(armory.Setup(Context(hero, 1)));
            yield return null;
            yield return null;
            var station = armory.Stations.Single();
            AssertFinds(armory, station);

            yield return MoveTo(hero, station.Position);
            Assert.AreEqual(1, armory.Claims, "A fly-in counts a claim.");
            Assert.AreEqual(new[] { "arrived Armory", "claimed Armory" }, _beats);
            yield return null;
            Assert.AreEqual(1, armory.Claims, "Sitting in the armory claims nothing more.");
            Assert.IsFalse(station.Spent, "A claim alone leaves the armory live.");
            AssertFinds(armory, station);

            yield return MoveTo(hero, station.Position + GamePlane.Right * 100f);
            yield return MoveTo(hero, station.Position);
            Assert.AreEqual(2, armory.Claims, "Flying back in after a skip claims again.");
            Assert.AreEqual(2, _beats.Count, "The arrival events fire only on the first arrival.");

            armory.Take();
            Assert.IsTrue(station.Spent);
            Assert.IsFalse(station.Lit.activeSelf, "A taken armory goes dark.");
            AssertFinds(armory);

            yield return MoveTo(hero, station.Position + GamePlane.Right * 100f);
            yield return MoveTo(hero, station.Position);
            Assert.AreEqual(2, armory.Claims, "A spent armory gives no claim.");
            Assert.Throws<InvalidOperationException>(armory.Take, "A take needs an open claim.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TheAlarm_DarkensAnUntakenArmory()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(hero);
            var armory = NewArmory(hunt);
            Drive(armory.Setup(Context(hero, 1)));
            yield return null;
            yield return null;
            var station = armory.Stations.Single();
            yield return MoveTo(hero, station.Position);
            Assert.AreEqual(1, armory.Claims);

            alarmAt = Time.time + 2000f;
            hunt.Tick(alarmAt);
            Assert.IsTrue(hunt.Alarmed, "The search clock ran out.");
            Assert.IsFalse(hunt.Released, "The gap after the alarm has not elapsed.");
            yield return null;
            Assert.IsFalse(station.Spent, "The gap leaves a claimed but untaken armory.");
            Assert.IsTrue(station.Lit.activeSelf, "The armory stays lit through the gap.");
            hunt.Tick(alarmAt + AlarmGrace);
            Assert.IsTrue(hunt.Released, "The gap after the alarm has elapsed.");
            yield return null;
            Assert.IsTrue(station.Spent, "The release spends a claimed but untaken armory.");
            Assert.IsFalse(station.Lit.activeSelf);
            AssertFinds(armory);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator AtSetup_TheArmoryPicksSpacedFromTheRepairAndModStations()
        {
            Random.InitState(1333);
            var hero = SpawnHero();
            var repairs = NewRepairStations();
            var mods = NewModStations(repairs);
            var armory = NewArmory(spacing: Spacing, spacedFrom: new FindStation[] { repairs, mods });
            Drive(repairs.Setup(Context(hero, 1)));
            Drive(mods.Setup(Context(hero, 1)));
            Drive(armory.Setup(Context(hero, 1)));
            yield return null;
            yield return null;

            var point = armory.Stations.Single().Position;
            foreach (var other in repairs.Stations.Concat(mods.Stations))
                Assert.GreaterOrEqual(PlaneDistance(point, other.Position), Spacing,
                    "The armory lands clear of every repair and mod station.");
            Assert.AreEqual(Distance, PlaneDistance(point, hero.transform.position), .01f);
            Assert.AreEqual(5, _objectives.Finds.Count, "Two repair stations, two mod stations and the armory are finds.");
        }

        private Ship SpawnHero() =>
            Track(ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero).gameObject).GetComponent<Ship>();

        private static IEnumerator MoveTo(Ship hero, Vector3 position)
        {
            ShipTestFactory.MoveKinematicShip(hero, position);
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        private SectorHunt NewHunt(Ship hero)
        {
            var feel = Track(ScriptableObject.CreateInstance<HuntFeelProfile>());
            feel.searchSeconds = 1000f;
            feel.alarmGraceSeconds = AlarmGrace;
            var hunt = Track(new GameObject("Hunt")).AddComponent<SectorHunt>();
            hunt.Configure(feel, null, null, 0f);
            Drive(hunt.Build(Context(hero, 1)));
            return hunt;
        }

        private Armory NewArmory(SectorHunt hunt = null, float spacing = 0f, params FindStation[] spacedFrom)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Armory>(ArmoryPath);
            var armory = Track(Object.Instantiate(prefab.gameObject)).GetComponent<Armory>();
            armory.Configure(Distance, hunt, spacing, spacedFrom);
            armory.Arrived += kind => _beats.Add($"arrived {kind}");
            armory.FindClaimed += kind => _beats.Add($"claimed {kind}");
            return armory;
        }

        private RepairStation NewRepairStations()
        {
            var feel = Track(ScriptableObject.CreateInstance<RepairStationFeelProfile>());
            feel.stationsPerSector = 2;
            var prefab = AssetDatabase.LoadAssetAtPath<RepairStation>(RepairStationPath);
            var repairs = Track(Object.Instantiate(prefab.gameObject)).GetComponent<RepairStation>();
            repairs.Configure(feel, Distance, spacing: Spacing);
            return repairs;
        }

        private ModStation NewModStations(RepairStation repairs)
        {
            var feel = Track(ScriptableObject.CreateInstance<ModStationFeelProfile>());
            feel.stationsPerSector = 2;
            var prefab = AssetDatabase.LoadAssetAtPath<ModStation>(ModStationPath);
            var mods = Track(Object.Instantiate(prefab.gameObject)).GetComponent<ModStation>();
            mods.Configure(feel, Distance, spacing: Spacing, repairs: repairs);
            return mods;
        }

        private SectorBuildContext Context(Ship hero, int sectorIndex = 0, SectorEntry row = null) =>
            new(null, _objectives, true, _sector, default, hero: hero, sectorIndex: sectorIndex,
                row: row ?? new SectorEntry { armories = 1 });

        // Exactly the live armories are finds, in the armory colour, with the armory's own icon.
        private void AssertFinds(Armory armory, params FindStation.Station[] live)
        {
            using var serialized = new SerializedObject(armory);
            var icon = (Sprite)serialized.FindProperty("icon").objectReferenceValue;
            Assert.IsTrue(icon, "The armory carries its find icon.");
            CollectionAssert.AreEquivalent(live.Select(station => station.Body.transform),
                _objectives.Finds.Select(find => find.Target), "Exactly the live armories are finds.");
            var colour = FindColours.Of(armory.Kind);
            foreach (var find in _objectives.Finds)
            {
                Assert.AreSame(icon, find.Icon);
                Assert.AreEqual(colour, find.Colour);
            }
        }

        private static float PlaneDistance(Vector3 a, Vector3 b) => Vector2.Distance(
            GamePlane.WorldPointToPlane(a), GamePlane.WorldPointToPlane(b));

        private static void Drive(IEnumerator routine)
        {
            while (routine.MoveNext()) { }
        }

        private T Track<T>(T item) where T : Object
        {
            _created.Add(item);
            return item;
        }
    }
}
#endif
