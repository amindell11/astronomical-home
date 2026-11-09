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
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Substrate.Sessions;

namespace Tests.PlayMode.Sectors
{
    /// <summary>The shipped repair station prefab with a scratch feel: it shows on a point its distance from the hero's start, repairs hull at the feel's rate while the hero sits in it, draws on its supply only for what it heals, goes dark when the supply is spent or the sector's hunt raises its alarm, places a set of stations apart with a supply each, stays live in a sector with no hunt, raises its arrival events once, and is a find on the objective service from placement until it goes dark.</summary>
    [TestFixture]
    [Category("Sectors")]
    [Category("RequiresLfsPng")]
    public class RepairStationPlayModeTests : PlayModeWorldFixture
    {
        private const string StationPath = "Assets/Prefabs/Objectives/Repair Station.prefab";
        private const float Distance = 40f;
        private const int SectorIndex = 2;
        private const float Spacing = 40f;

        private readonly List<Object> _created = new();
        private readonly List<string> _beats = new();
        private ObjectiveService _objectives;
        private Sector _sector;

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

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator SittingInTheStation_RepairsAtTheRate_UntilTheSupplyIsSpent_ThenItGoesDark()
        {
            var hero = SpawnHero();
            var module = NewStation(repairPerSecond: .5f, supply: .4f);
            Drive(module.Setup(Context(hero)));
            var station = module.Stations[0];
            Assert.IsFalse(station.Body.activeSelf, "The station hides until its first tick places it.");
            yield return null;
            yield return null;

            Assert.IsTrue(station.Body.activeSelf);
            Assert.IsTrue(station.Lit.activeSelf);
            Assert.AreEqual(Distance, PlaneDistance(station.Position, hero.transform.position), .01f,
                "The station is placed its distance from the hero's start.");
            Assert.IsEmpty(_beats, "Nothing is raised before the hero arrives.");
            AssertFinds(module, station);

            hero.Damage.Health.SetFraction(.3f);
            ShipTestFactory.MoveKinematicShip(hero, station.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(new[] { $"arrived Repair {SectorIndex}", "claimed Repair" }, _beats);

            var before = hero.Damage.Health.Pct;
            yield return null;
            Assert.AreEqual(.5f * Time.deltaTime, hero.Damage.Health.Pct - before, 1e-4f,
                "Each frame inside heals the rate times the frame.");

            yield return WaitSeconds(() => station.Spent, 3f);
            Assert.IsTrue(station.Spent);
            Assert.AreEqual(.7f, hero.Damage.Health.Pct, 1e-4f, "The station gives exactly its supply.");
            Assert.IsFalse(station.Lit.activeSelf, "A spent station goes dark.");
            AssertFinds(module);

            hero.Damage.Health.SetFraction(.5f);
            yield return null;
            yield return null;
            Assert.AreEqual(.5f, hero.Damage.Health.Pct, 1e-4f, "A dark station repairs nothing.");
            Assert.AreEqual(2, _beats.Count, "The arrival events fire once.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator AFullHull_DrawsNoSupply_AndComingBack_RaisesNoSecondArrival()
        {
            var hero = SpawnHero();
            var module = NewStation(repairPerSecond: .5f, supply: 1f);
            Drive(module.Setup(Context(hero)));
            var station = module.Stations[0];
            yield return null;
            yield return null;

            ShipTestFactory.MoveKinematicShip(hero, station.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.AreEqual(1f, station.SupplyLeft, "A full hull draws nothing from the supply.");
            Assert.AreEqual(2, _beats.Count);

            ShipTestFactory.MoveKinematicShip(hero, station.Position + GamePlane.Right * 100f);
            yield return new WaitForFixedUpdate();
            yield return null;
            hero.Damage.Health.SetFraction(.5f);
            ShipTestFactory.MoveKinematicShip(hero, station.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.Greater(hero.Damage.Health.Pct, .5f, "Coming back repairs again.");
            Assert.Less(station.SupplyLeft, 1f);
            Assert.AreEqual(2, _beats.Count, "Coming back raises no second arrival.");

            AssertFinds(module, station);
            Drive(module.Teardown(Context(hero)));
            Assert.IsFalse(station.Body.activeSelf, "Teardown hides the station.");
            Assert.IsEmpty(_objectives.Finds, "Teardown leaves no finds.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TheAlarm_DarkensAnUnusedStation_AndItRaisesNoArrival()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(hero);
            var module = NewStation(repairPerSecond: .5f, supply: 1f, hunt);
            Drive(module.Setup(Context(hero)));
            var station = module.Stations[0];
            yield return null;
            yield return null;
            Assert.IsTrue(station.Lit.activeSelf, "The station is live before the alarm.");

            RaiseAlarm(hunt);
            yield return null;
            Assert.IsTrue(station.Spent, "The alarm takes the whole supply.");
            Assert.IsFalse(station.Lit.activeSelf, "The alarm darkens the station.");
            AssertFinds(module);

            hero.Damage.Health.SetFraction(.5f);
            ShipTestFactory.MoveKinematicShip(hero, station.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.AreEqual(.5f, hero.Damage.Health.Pct, 1e-4f, "A station darkened by the alarm repairs nothing.");
            Assert.IsEmpty(_beats, "Arriving after the alarm raises nothing.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TheAlarm_DarkensAHalfUsedStation_AndWhatWasLeftIsLost()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(hero);
            var module = NewStation(repairPerSecond: .5f, supply: 1f, hunt);
            Drive(module.Setup(Context(hero)));
            var station = module.Stations[0];
            yield return null;
            yield return null;

            hero.Damage.Health.SetFraction(.2f);
            ShipTestFactory.MoveKinematicShip(hero, station.Position);
            yield return new WaitForFixedUpdate();
            yield return WaitSeconds(() => station.SupplyLeft < .8f, 3f);
            Assert.Less(station.SupplyLeft, .8f, "The hero used part of the supply.");
            Assert.AreEqual(2, _beats.Count);

            RaiseAlarm(hunt);
            yield return null;
            Assert.IsTrue(station.Spent, "The alarm takes what was left of the supply.");
            Assert.IsFalse(station.Lit.activeSelf, "The alarm darkens a half-used station.");

            var hull = hero.Damage.Health.Pct;
            Assert.Less(hull, 1f);
            yield return null;
            yield return null;
            Assert.AreEqual(hull, hero.Damage.Health.Pct, 1e-4f, "No repair after the alarm.");
            Assert.AreEqual(2, _beats.Count, "The alarm raises no arrival.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator ASectorWithNoHunt_KeepsItsStationLive()
        {
            var hero = SpawnHero();
            var module = NewStation(repairPerSecond: .5f, supply: 1f);
            Drive(module.Setup(Context(hero)));
            var station = module.Stations[0];
            for (var i = 0; i < 10; i++)
                yield return null;
            Assert.IsTrue(station.Lit.activeSelf);
            Assert.AreEqual(1f, station.SupplyLeft);

            hero.Damage.Health.SetFraction(.5f);
            ShipTestFactory.MoveKinematicShip(hero, station.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.Greater(hero.Damage.Health.Pct, .5f, "With no hunt the station repairs.");
            Assert.AreEqual(2, _beats.Count);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TwoStations_PlaceApart_EachRepairsFromItsOwnSupply_AndTheAlarmDarkensBoth()
        {
            UnityEngine.Random.InitState(1319);
            var hero = SpawnHero();
            var hunt = NewHunt(hero);
            var module = NewStation(repairPerSecond: .5f, supply: .2f, hunt, count: 2, spacing: Spacing);
            Drive(module.Setup(Context(hero)));
            yield return null;
            yield return null;

            Assert.AreEqual(2, module.Stations.Count);
            var first = module.Stations[0];
            var second = module.Stations[1];
            Assert.AreNotSame(first.Body, second.Body, "Each station has its own body.");
            foreach (var station in module.Stations)
            {
                Assert.IsTrue(station.Body.activeSelf);
                Assert.IsTrue(station.Lit.activeSelf);
                Assert.AreEqual(Distance, PlaneDistance(station.Position, hero.transform.position), .01f,
                    "Each station is placed its distance from the hero's start.");
            }
            Assert.GreaterOrEqual(PlaneDistance(first.Position, second.Position), Spacing,
                "The stations place at least their spacing apart.");
            AssertFinds(module, first, second);

            hero.Damage.Health.SetFraction(.2f);
            ShipTestFactory.MoveKinematicShip(hero, first.Position);
            yield return new WaitForFixedUpdate();
            yield return WaitSeconds(() => first.Spent, 3f);
            Assert.IsTrue(first.Spent);
            Assert.IsFalse(first.Lit.activeSelf, "The spent station goes dark.");
            Assert.AreEqual(.2f, second.SupplyLeft, "The other station keeps its own supply.");
            Assert.IsTrue(second.Lit.activeSelf);
            AssertFinds(module, second);
            Assert.AreEqual(new[] { $"arrived Repair {SectorIndex}", "claimed Repair" }, _beats);

            ShipTestFactory.MoveKinematicShip(hero, second.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.Less(second.SupplyLeft, .2f, "The second station repairs from its own supply.");
            Assert.AreEqual(4, _beats.Count, "Each station raises its own first arrival.");

            RaiseAlarm(hunt);
            yield return null;
            foreach (var station in module.Stations)
            {
                Assert.IsTrue(station.Spent, "The alarm takes every station's supply.");
                Assert.IsFalse(station.Lit.activeSelf, "The alarm darkens every station.");
            }
            AssertFinds(module);
        }

        private Ship SpawnHero() =>
            Track(ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero).gameObject).GetComponent<Ship>();

        private SectorHunt NewHunt(Ship hero)
        {
            var feel = Track(ScriptableObject.CreateInstance<HuntFeelProfile>());
            feel.searchSeconds = 1000f;
            var hunt = Track(new GameObject("Hunt")).AddComponent<SectorHunt>();
            hunt.Configure(feel, null, null, 0f);
            Drive(hunt.Build(Context(hero)));
            return hunt;
        }

        private static void RaiseAlarm(SectorHunt hunt)
        {
            hunt.Tick(Time.time + 2000f);
            Assert.IsTrue(hunt.Alarmed, "The search clock ran out.");
        }

        private RepairStation NewStation(float repairPerSecond, float supply, SectorHunt hunt = null, int count = 1,
            float spacing = 0f)
        {
            var feel = Track(ScriptableObject.CreateInstance<RepairStationFeelProfile>());
            feel.repairPerSecond = repairPerSecond;
            feel.supply = supply;
            feel.stationsPerSector = count;
            var prefab = AssetDatabase.LoadAssetAtPath<RepairStation>(StationPath);
            var station = Track(Object.Instantiate(prefab.gameObject)).GetComponent<RepairStation>();
            station.Configure(feel, Distance, hunt, spacing);
            station.Arrived += (kind, sector) => _beats.Add($"arrived {kind} {sector}");
            station.FindClaimed += kind => _beats.Add($"claimed {kind}");
            return station;
        }

        private SectorBuildContext Context(Ship hero) =>
            new(null, _objectives, true, _sector, default, hero: hero, sectorIndex: SectorIndex, row: new SectorEntry());

        // Exactly the live stations are finds, with the module's icon and find-layer markers.
        private void AssertFinds(RepairStation module, params RepairStation.Station[] live)
        {
            using var serialized = new SerializedObject(module);
            var icon = (Sprite)serialized.FindProperty("icon").objectReferenceValue;
            Assert.IsTrue(icon, "The module carries its find icon.");
            CollectionAssert.AreEquivalent(live.Select(station => station.Body.transform),
                _objectives.Finds.Select(find => find.Target), "Exactly the live stations are finds.");
            foreach (var find in _objectives.Finds)
                Assert.AreSame(icon, find.Icon);
            foreach (var station in live)
            {
                var marker = station.Lit.GetComponentInChildren<MeshRenderer>(true);
                Assert.IsTrue(marker.enabled && marker.gameObject.activeInHierarchy, "A live find shows its minimap marker.");
                Assert.AreEqual(LayerMask.NameToLayer("Minimap_Find"), marker.gameObject.layer);
            }
        }

        private static float PlaneDistance(Vector3 a, Vector3 b) => Vector2.Distance(
            GamePlane.WorldPointToPlane(a), GamePlane.WorldPointToPlane(b));

        private static void Drive(IEnumerator routine)
        {
            while (routine.MoveNext()) { }
        }

        private static IEnumerator WaitSeconds(Func<bool> done, float seconds)
        {
            var deadline = Time.time + seconds;
            while (!done() && Time.time < deadline)
                yield return null;
        }

        private T Track<T>(T item) where T : Object
        {
            _created.Add(item);
            return item;
        }
    }
}
#endif
