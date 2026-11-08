#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Combat.Weapons;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using Ships;
using Substrate;
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
    /// <summary>The shipped weapon cache prefab and boost mod with a scratch feel: a cache shows its distance from the hero's start as a find, the hero's first fly-in adds the boost to the run's mods once and shows it on the HUD, the cache then goes dark, teardown takes the boost back and leaves the run's own picks, the alarm darkens unclaimed caches but keeps a claimed boost, a sector with no hunt keeps them live, and caches wait for the sector's repair stations and place spaced from them and from each other.</summary>
    [TestFixture]
    [Category("Sectors")]
    [Category("RequiresLfsPng")]
    public class WeaponCachePlayModeTests : PlayModeWorldFixture
    {
        private const string CachePath = "Assets/Prefabs/Objectives/Weapon Cache.prefab";
        private const string StationPath = "Assets/Prefabs/Objectives/Repair Station.prefab";
        private const string BoostPath = "Assets/Settings/Game/WeaponCacheBoost.asset";
        private const string LasersPath = "Assets/Prefabs/Weapons/Lasers.prefab";
        private const float Distance = 40f;
        private const int SectorIndex = 2;
        private const float Spacing = 20f;

        private readonly List<Object> _created = new();
        private readonly List<string> _beats = new();
        private ObjectiveService _objectives;
        private WeaponComponent _lasers;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _objectives = Track(new GameObject("Objectives")).AddComponent<ObjectiveService>();
            _lasers = AssetDatabase.LoadAssetAtPath<WeaponComponent>(LasersPath);
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
        public IEnumerator FlyingIn_GrantsTheBoostOnce_ShowsItOnTheHud_AndTheCacheGoesDark()
        {
            var hero = SpawnHero();
            var module = NewCache();
            Drive(module.Setup(Context(hero)));
            var cache = module.Caches[0];
            Assert.IsFalse(cache.Body.activeSelf, "The cache hides until a tick places it.");
            yield return null;
            yield return null;

            Assert.IsTrue(cache.Body.activeSelf);
            Assert.IsTrue(cache.Lit.activeSelf);
            Assert.AreEqual(Distance, PlaneDistance(cache.Position, hero.transform.position), .01f,
                "The cache is placed its distance from the hero's start.");
            AssertFinds(module, cache);
            Assert.AreEqual(1f, DamageFactor(hero), 1e-5f, "No boost before the claim.");

            ShipTestFactory.MoveKinematicShip(hero, cache.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(new[] { $"arrived Cache {SectorIndex}", "claimed Cache" }, _beats);
            Assert.AreEqual(1.25f, DamageFactor(hero), 1e-5f, "The claim adds the boost mod to the run's mods.");
            Assert.AreEqual(new[] { "+25% damage" }, _objectives.Boosts.Select(boost => boost.Label),
                "The HUD shows the boost while it is in force.");
            Assert.IsTrue(cache.Spent);
            Assert.IsFalse(cache.Lit.activeSelf, "A claimed cache goes dark.");
            AssertFinds(module);

            ShipTestFactory.MoveKinematicShip(hero, cache.Position + GamePlane.Right * 100f);
            yield return new WaitForFixedUpdate();
            yield return null;
            ShipTestFactory.MoveKinematicShip(hero, cache.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.AreEqual(1.25f, DamageFactor(hero), 1e-5f, "Coming back grants nothing more.");
            Assert.AreEqual(2, _beats.Count, "The claim events fire once.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator Teardown_TakesTheBoostBack_AndLeavesTheRunsOwnPicks()
        {
            var hero = SpawnHero();
            var pick = Track(ScriptableObject.CreateInstance<WeaponMod>());
            pick.Configure("Hot rounds", WeaponModField.Damage, .25f);
            hero.Mods.Add(pick);
            var module = NewCache();
            Drive(module.Setup(Context(hero)));
            yield return null;
            yield return null;

            var body = module.Caches[0].Body;
            ShipTestFactory.MoveKinematicShip(hero, module.Caches[0].Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(1.5f, DamageFactor(hero), 1e-5f, "The boost adds to the run's pick.");

            Drive(module.Teardown(Context(hero)));
            Assert.AreEqual(1.25f, DamageFactor(hero), 1e-5f, "The next sector starts without the boost.");
            Assert.AreEqual(new[] { pick }, hero.Mods.Using(_lasers).Select(held => held.Mod),
                "The run's own pick stays held.");
            Assert.IsEmpty(_objectives.Boosts, "The HUD readout goes with the boost.");
            Assert.IsEmpty(_objectives.Finds, "Teardown leaves no finds.");
            Assert.IsFalse(body.activeSelf, "Teardown hides the cache.");

            Drive(module.Setup(Context(hero)));
            yield return null;
            yield return null;
            Assert.AreEqual(1.25f, DamageFactor(hero), 1e-5f, "A new sector's caches start unclaimed.");
            Assert.IsFalse(module.Caches[0].Spent);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TheAlarm_DarkensAnUnclaimedCache_AndKeepsAClaimedBoost()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(hero);
            var module = NewCache(hunt, count: 2, spacing: Spacing);
            Drive(module.Setup(Context(hero)));
            yield return null;
            yield return null;
            var claimed = module.Caches[0];
            var unclaimed = module.Caches[1];

            ShipTestFactory.MoveKinematicShip(hero, claimed.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(1.25f, DamageFactor(hero), 1e-5f);
            AssertFinds(module, unclaimed);

            RaiseAlarm(hunt);
            yield return null;
            Assert.IsTrue(unclaimed.Spent, "The alarm spends an unclaimed cache.");
            Assert.IsFalse(unclaimed.Lit.activeSelf, "The alarm darkens an unclaimed cache.");
            AssertFinds(module);
            Assert.AreEqual(1.25f, DamageFactor(hero), 1e-5f, "A claimed boost lasts until the sector ends.");
            Assert.AreEqual(1, _objectives.Boosts.Count);

            ShipTestFactory.MoveKinematicShip(hero, unclaimed.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.AreEqual(1.25f, DamageFactor(hero), 1e-5f, "A cache darkened by the alarm grants nothing.");
            Assert.AreEqual(2, _beats.Count, "Arriving after the alarm raises nothing.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator ASectorWithNoHunt_KeepsItsCacheLive()
        {
            var hero = SpawnHero();
            var module = NewCache();
            Drive(module.Setup(Context(hero)));
            var cache = module.Caches[0];
            for (var i = 0; i < 10; i++)
                yield return null;
            Assert.IsTrue(cache.Lit.activeSelf);
            Assert.IsFalse(cache.Spent);

            ShipTestFactory.MoveKinematicShip(hero, cache.Position);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(1.25f, DamageFactor(hero), 1e-5f, "With no hunt the cache grants its boost.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator Caches_WaitForTheRepairStations_ThenPlaceSpacedFromThemAndEachOther()
        {
            UnityEngine.Random.InitState(1334);
            var hero = SpawnHero();
            var stations = NewStations(count: 2);
            var module = NewCache(count: 2, spacing: Spacing, stations: stations);
            Drive(stations.Setup(Context(hero)));
            Drive(module.Setup(Context(hero)));
            stations.enabled = false;
            yield return null;
            yield return null;
            Assert.IsTrue(module.Caches.All(cache => !cache.Body.activeSelf),
                "The caches wait while the repair stations are unplaced.");
            Assert.IsEmpty(_objectives.Finds);

            stations.enabled = true;
            yield return null;
            yield return null;
            yield return null;
            Assert.IsTrue(stations.Stations.All(station => station.Body.activeSelf));
            Assert.IsTrue(module.Caches.All(cache => cache.Body.activeSelf), "The caches place once the stations show.");
            var points = stations.Stations.Select(station => station.Position)
                .Concat(module.Caches.Select(cache => cache.Position)).ToList();
            for (var i = 0; i < points.Count; i++)
            for (var j = i + 1; j < points.Count; j++)
                Assert.GreaterOrEqual(PlaneDistance(points[i], points[j]), Spacing,
                    "No cache lands within its spacing of a station or another cache.");
            foreach (var cache in module.Caches)
                Assert.AreEqual(Distance, PlaneDistance(cache.Position, hero.transform.position), .01f);
            Assert.AreEqual(4, _objectives.Finds.Count, "Both stations and both caches are finds.");
        }

        private Ship SpawnHero()
        {
            var hero = Track(ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero).gameObject).GetComponent<Ship>();
            hero.BindMods(new LullModStack());
            return hero;
        }

        private float DamageFactor(Ship hero) => hero.Mods.For(_lasers, _lasers).Factor(WeaponModField.Damage);

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

        private WeaponCache NewCache(SectorHunt hunt = null, int count = 1, float spacing = 0f,
            RepairStation stations = null)
        {
            var feel = Track(ScriptableObject.CreateInstance<WeaponCacheFeelProfile>());
            feel.cachesPerSector = count;
            var prefab = AssetDatabase.LoadAssetAtPath<WeaponCache>(CachePath);
            var cache = Track(Object.Instantiate(prefab.gameObject)).GetComponent<WeaponCache>();
            cache.Configure(feel, AssetDatabase.LoadAssetAtPath<WeaponMod>(BoostPath), Distance, hunt, spacing, stations);
            cache.Arrived += (kind, sector) => _beats.Add($"arrived {kind} {sector}");
            cache.FindClaimed += kind => _beats.Add($"claimed {kind}");
            return cache;
        }

        private RepairStation NewStations(int count)
        {
            var feel = Track(ScriptableObject.CreateInstance<RepairStationFeelProfile>());
            feel.stationsPerSector = count;
            var prefab = AssetDatabase.LoadAssetAtPath<RepairStation>(StationPath);
            var station = Track(Object.Instantiate(prefab.gameObject)).GetComponent<RepairStation>();
            station.Configure(feel, Distance, spacing: Spacing);
            return station;
        }

        private SectorBuildContext Context(Ship hero) =>
            new(null, _objectives, true, null, default, hero: hero, sectorIndex: SectorIndex);

        // Exactly the live caches are finds, with the module's icon and find-layer markers.
        private void AssertFinds(WeaponCache module, params WeaponCache.Cache[] live)
        {
            using var serialized = new SerializedObject(module);
            var icon = (Sprite)serialized.FindProperty("icon").objectReferenceValue;
            Assert.IsTrue(icon, "The module carries its find icon.");
            CollectionAssert.AreEquivalent(live.Select(cache => cache.Body.transform),
                _objectives.Finds.Select(find => find.Target), "Exactly the live caches are finds.");
            foreach (var find in _objectives.Finds)
                Assert.AreSame(icon, find.Icon);
            foreach (var cache in live)
            {
                var marker = cache.Lit.GetComponentInChildren<MeshRenderer>(true);
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
