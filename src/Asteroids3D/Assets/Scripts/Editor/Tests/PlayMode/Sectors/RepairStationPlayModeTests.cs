#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ships;
using Substrate;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Stations;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Sectors
{
    /// <summary>The shipped repair station prefab with a scratch feel: it shows on a point its distance from the hero's start, repairs hull at the feel's rate while the hero sits in it, draws on its supply only for what it heals, goes dark when the supply is spent, and raises its arrival events once.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class RepairStationPlayModeTests : PlayModeWorldFixture
    {
        private const string StationPath = "Assets/Prefabs/Objectives/Repair Station.prefab";
        private const float Distance = 40f;
        private const int SectorIndex = 2;

        private readonly List<Object> _created = new();
        private readonly List<string> _beats = new();

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
            var station = NewStation(repairPerSecond: .5f, supply: .4f);
            Drive(station.Setup(Context(hero)));
            Assert.IsFalse(Body(station).activeSelf, "The station hides until its first tick places it.");
            yield return null;
            yield return null;

            Assert.IsTrue(Body(station).activeSelf);
            Assert.IsTrue(Lit(station).activeSelf);
            Assert.AreEqual(Distance, PlaneDistance(station.transform, hero.transform), .01f,
                "The station is placed its distance from the hero's start.");
            Assert.IsEmpty(_beats, "Nothing is raised before the hero arrives.");

            hero.Damage.Health.SetFraction(.3f);
            ShipTestFactory.MoveKinematicShip(hero, station.transform.position);
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
            Assert.IsFalse(Lit(station).activeSelf, "A spent station goes dark.");

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
            var station = NewStation(repairPerSecond: .5f, supply: 1f);
            Drive(station.Setup(Context(hero)));
            yield return null;
            yield return null;

            ShipTestFactory.MoveKinematicShip(hero, station.transform.position);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.AreEqual(1f, station.SupplyLeft, "A full hull draws nothing from the supply.");
            Assert.AreEqual(2, _beats.Count);

            ShipTestFactory.MoveKinematicShip(hero, station.transform.position + GamePlane.Right * 100f);
            yield return new WaitForFixedUpdate();
            yield return null;
            hero.Damage.Health.SetFraction(.5f);
            ShipTestFactory.MoveKinematicShip(hero, station.transform.position);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.Greater(hero.Damage.Health.Pct, .5f, "Coming back repairs again.");
            Assert.Less(station.SupplyLeft, 1f);
            Assert.AreEqual(2, _beats.Count, "Coming back raises no second arrival.");

            Drive(station.Teardown(Context(hero)));
            Assert.IsFalse(Body(station).activeSelf, "Teardown hides the station.");
        }

        private Ship SpawnHero() =>
            Track(ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero).gameObject).GetComponent<Ship>();

        private RepairStation NewStation(float repairPerSecond, float supply)
        {
            var feel = Track(ScriptableObject.CreateInstance<RepairStationFeelProfile>());
            feel.repairPerSecond = repairPerSecond;
            feel.supply = supply;
            var prefab = AssetDatabase.LoadAssetAtPath<RepairStation>(StationPath);
            var station = Track(Object.Instantiate(prefab.gameObject)).GetComponent<RepairStation>();
            station.Configure(feel, Distance);
            station.Arrived += (kind, sector) => _beats.Add($"arrived {kind} {sector}");
            station.FindClaimed += kind => _beats.Add($"claimed {kind}");
            return station;
        }

        private static SectorBuildContext Context(Ship hero) =>
            new(null, null, true, null, default, hero: hero, sectorIndex: SectorIndex);

        private static GameObject Body(RepairStation station) => Field<GameObject>(station, "body");

        private static GameObject Lit(RepairStation station) => Field<GameObject>(station, "lit");

        private static T Field<T>(Object owner, string field) where T : Object
        {
            using var serialized = new SerializedObject(owner);
            return (T)serialized.FindProperty(field).objectReferenceValue;
        }

        private static float PlaneDistance(Transform a, Transform b) => Vector2.Distance(
            GamePlane.WorldPointToPlane(a.position), GamePlane.WorldPointToPlane(b.position));

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
