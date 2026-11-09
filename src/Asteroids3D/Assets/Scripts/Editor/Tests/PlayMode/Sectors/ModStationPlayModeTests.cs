#if UNITY_EDITOR
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

namespace Tests.PlayMode.Sectors
{
    /// <summary>The shipped mod station prefab with a scratch feel: a station shows its distance from the hero's start as a find, the hero's first fly-in counts one claim and raises the arrival events once, the station then goes dark, teardown clears the claims and finds, the alarm darkens unclaimed stations, a sector with no hunt keeps them live, and at setup the stations pick points spaced from the sector's repair stations, which pick first, and from each other.</summary>
    [TestFixture]
    [Category("Sectors")]
    [Category("RequiresLfsPng")]
    public class ModStationPlayModeTests : PlayModeWorldFixture
    {
        private const string ModStationPath = "Assets/Prefabs/Objectives/Mod Station.prefab";
        private const string RepairStationPath = "Assets/Prefabs/Objectives/Repair Station.prefab";
        private const float Distance = 40f;
        private const int SectorIndex = 2;
        private const float Spacing = 20f;

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
        public IEnumerator FlyingIn_ClaimsOnce_AndTheStationGoesDark()
        {
            var hero = SpawnHero();
            var module = NewModStation();
            Drive(module.Setup(Context(hero)));
            var station = module.Stations[0];
            Assert.IsFalse(station.Body.activeSelf, "The station hides until a tick places it.");
            yield return null;
            yield return null;

            Assert.IsTrue(station.Body.activeSelf);
            Assert.IsTrue(station.Lit.activeSelf);
            Assert.AreEqual(Distance, PlaneDistance(station.Position, hero.transform.position), .01f,
                "The station is placed its distance from the hero's start.");
            AssertFinds(module, station);
            Assert.AreEqual(0, module.Claims);

            ShipTestFactory.MoveKinematicShip(hero, station.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(new[] { $"arrived Mod {SectorIndex}", "claimed Mod" }, _beats);
            Assert.AreEqual(1, module.Claims, "The fly-in counts one claim.");
            Assert.IsTrue(station.Spent);
            Assert.IsFalse(station.Lit.activeSelf, "A claimed station goes dark.");
            AssertFinds(module);

            ShipTestFactory.MoveKinematicShip(hero, station.Position + GamePlane.Right * 100f);
            yield return new WaitForFixedUpdate();
            yield return null;
            ShipTestFactory.MoveKinematicShip(hero, station.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.AreEqual(1, module.Claims, "Coming back claims nothing more.");
            Assert.AreEqual(2, _beats.Count, "The claim events fire once.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator Teardown_ClearsTheFinds_AndTheNextSectorStartsUnclaimed()
        {
            var hero = SpawnHero();
            var module = NewModStation();
            Drive(module.Setup(Context(hero)));
            yield return null;
            yield return null;

            var body = module.Stations[0].Body;
            ShipTestFactory.MoveKinematicShip(hero, module.Stations[0].Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(1, module.Claims);

            Drive(module.Teardown(Context(hero)));
            Assert.IsEmpty(_objectives.Finds, "Teardown leaves no finds.");
            Assert.IsFalse(body.activeSelf, "Teardown hides the station.");

            Drive(module.Setup(Context(hero)));
            Assert.AreEqual(0, module.Claims, "A new sector counts its own claims.");
            yield return null;
            yield return null;
            Assert.IsFalse(module.Stations[0].Spent, "A new sector's stations start unclaimed.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TheAlarm_DarkensAnUnclaimedStation_AndKeepsTheClaimCount()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(hero);
            var module = NewModStation(hunt, count: 2, spacing: Spacing);
            Drive(module.Setup(Context(hero)));
            yield return null;
            yield return null;
            var claimed = module.Stations[0];
            var unclaimed = module.Stations[1];

            ShipTestFactory.MoveKinematicShip(hero, claimed.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(1, module.Claims);
            AssertFinds(module, unclaimed);

            RaiseAlarm(hunt);
            yield return null;
            Assert.IsTrue(unclaimed.Spent, "The alarm spends an unclaimed station.");
            Assert.IsFalse(unclaimed.Lit.activeSelf, "The alarm darkens an unclaimed station.");
            AssertFinds(module);

            ShipTestFactory.MoveKinematicShip(hero, unclaimed.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.AreEqual(1, module.Claims, "A station darkened by the alarm gives no claim.");
            Assert.AreEqual(2, _beats.Count, "Arriving after the alarm raises nothing.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator ASectorWithNoHunt_KeepsItsStationLive()
        {
            var hero = SpawnHero();
            var module = NewModStation();
            Drive(module.Setup(Context(hero)));
            var station = module.Stations[0];
            for (var i = 0; i < 10; i++)
                yield return null;
            Assert.IsTrue(station.Lit.activeSelf);
            Assert.IsFalse(station.Spent);

            ShipTestFactory.MoveKinematicShip(hero, station.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(1, module.Claims, "With no hunt the station can be claimed.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator AtSetup_ModStationsPickSpacedFromTheRepairStationsAndEachOther()
        {
            Random.InitState(1402);
            var hero = SpawnHero();
            var repairs = NewRepairStations(count: 2);
            var module = NewModStation(count: 2, spacing: Spacing, repairs: repairs);
            Drive(repairs.Setup(Context(hero)));
            Drive(module.Setup(Context(hero)));
            var picked = module.Stations.Select(station => station.Position).ToList();
            Assert.IsTrue(module.Stations.All(station => !station.Body.activeSelf),
                "The stations pick at setup and hide until a tick shows them.");

            yield return null;
            yield return null;
            Assert.IsTrue(repairs.Stations.All(station => station.Body.activeSelf));
            Assert.IsTrue(module.Stations.All(station => station.Body.activeSelf));
            CollectionAssert.AreEqual(picked, module.Stations.Select(station => station.Body.transform.position),
                "Each station shows at the point it picked at setup.");
            var points = repairs.Stations.Select(station => station.Position)
                .Concat(module.Stations.Select(station => station.Position)).ToList();
            for (var i = 0; i < points.Count; i++)
            for (var j = i + 1; j < points.Count; j++)
                Assert.GreaterOrEqual(PlaneDistance(points[i], points[j]), Spacing,
                    "No mod station lands within its spacing of a repair station or another mod station.");
            foreach (var station in module.Stations)
                Assert.AreEqual(Distance, PlaneDistance(station.Position, hero.transform.position), .01f);
            Assert.AreEqual(4, _objectives.Finds.Count, "Both repair stations and both mod stations are finds.");
        }

        private Ship SpawnHero() =>
            Track(ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero).gameObject).GetComponent<Ship>();

        private SectorHunt NewHunt(Ship hero)
        {
            var feel = Track(ScriptableObject.CreateInstance<HuntFeelProfile>());
            feel.searchSeconds = 1000f;
            feel.patrolsPerSector = new[] { 0, 0, 0 };
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

        private ModStation NewModStation(SectorHunt hunt = null, int count = 1, float spacing = 0f,
            RepairStation repairs = null)
        {
            var feel = Track(ScriptableObject.CreateInstance<ModStationFeelProfile>());
            feel.stationsPerSector = count;
            var prefab = AssetDatabase.LoadAssetAtPath<ModStation>(ModStationPath);
            var module = Track(Object.Instantiate(prefab.gameObject)).GetComponent<ModStation>();
            module.Configure(feel, Distance, hunt, spacing, repairs);
            module.Arrived += (kind, sector) => _beats.Add($"arrived {kind} {sector}");
            module.FindClaimed += kind => _beats.Add($"claimed {kind}");
            return module;
        }

        private RepairStation NewRepairStations(int count)
        {
            var feel = Track(ScriptableObject.CreateInstance<RepairStationFeelProfile>());
            feel.stationsPerSector = count;
            var prefab = AssetDatabase.LoadAssetAtPath<RepairStation>(RepairStationPath);
            var repairs = Track(Object.Instantiate(prefab.gameObject)).GetComponent<RepairStation>();
            repairs.Configure(feel, Distance, spacing: Spacing);
            return repairs;
        }

        private SectorBuildContext Context(Ship hero) =>
            new(null, _objectives, true, _sector, default, hero: hero, sectorIndex: SectorIndex);

        // Exactly the live stations are finds, with the module's icon and find-layer markers.
        private void AssertFinds(ModStation module, params ModStation.Station[] live)
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

        private T Track<T>(T item) where T : Object
        {
            _created.Add(item);
            return item;
        }
    }
}
#endif
