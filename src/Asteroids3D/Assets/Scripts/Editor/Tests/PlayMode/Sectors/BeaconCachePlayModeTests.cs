#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Loot;
using NUnit.Framework;
using Objectives;
using Ships;
using Ships.Mods;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;
using Substrate.Services;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Sectors
{
    /// <summary>A phased sector with a beacon cache beside its spine: each beacon claim adds one rolled bonus to the run's mods and shows it with its seconds left, each bonus runs out on its own clock and is taken back, teardown takes back a running one and leaves the run's picks, the hull patch restores hull at once without touching the mods and wastes what passes full, a sector whose beacons don't run grants nothing, and a shipped shield bonus applies to a live ship and comes off again.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class BeaconCachePlayModeTests : PlayModeWorldFixture
    {
        private const string ShieldSurgePath = "Assets/Settings/Game/BeaconCache/ShieldSurge.asset";

        private UnitService _unitService;
        private ObjectiveService _objectives;
        private readonly List<Object> _created = new();
        private Ship _shielded;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            var unitServiceGO = Track(new GameObject("UnitService"));
            _unitService = unitServiceGO.AddComponent<UnitService>();
            _objectives = Track(new GameObject("ObjectiveService")).AddComponent<ObjectiveService>();
            ShipServices.Compose(_unitService, unitServiceGO.transform, presentationEnabled: true);
        }

        [TearDown]
        public override void TearDown()
        {
            if (_unitService) _unitService.Clear();
            ShipTestFactory.DestroyShip(_shielded);
            foreach (var item in _created)
                if (item) Object.DestroyImmediate(item);
            _created.Clear();
            base.TearDown();
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator EachClaim_RollsABonusOnItsOwnClock_AndEachRunsOutAlone()
        {
            var hero = SpawnHero();
            var hull = HullBonus(.5f);
            var world = BuildSector(hero, Feel(cacheSeconds: 4f, hull), presentation: true, phases: 3);
            yield return world.Sector.Setup();

            yield return ClaimNextBeacon(world, hero);
            var firstClaimedAt = Time.time;
            Assert.AreEqual(1.5f, hero.Mods.ForShip().Factor(ShipModField.Hull), 1e-5f, "A claim adds the rolled bonus.");
            Assert.AreEqual(1, _objectives.Boosts.Count);
            Assert.AreEqual("Hull bonus +50% max hull", _objectives.Boosts[0].Label, "The HUD names the bonus and its effect.");
            Assert.AreEqual(firstClaimedAt + 4f, _objectives.Boosts[0].EndsAt, .1f, "It shows when it runs out.");

            yield return ClaimNextBeacon(world, hero);
            Assert.Less(Time.time, firstClaimedAt + 4f, "The second claim lands while the first bonus runs.");
            Assert.AreEqual(2f, hero.Mods.ForShip().Factor(ShipModField.Hull), 1e-5f, "A second claim rolls its own bonus.");
            Assert.AreEqual(2, _objectives.Boosts.Count);

            yield return WaitSeconds(() => _objectives.Boosts.Count < 2, 6f);
            Assert.AreEqual(1.5f, hero.Mods.ForShip().Factor(ShipModField.Hull), 1e-5f,
                "The first bonus runs out on its own clock and the second keeps running.");
            Assert.AreEqual(firstClaimedAt + 4f, Time.time, .3f);

            yield return WaitSeconds(() => _objectives.Boosts.Count == 0, 6f);
            Assert.AreSame(ShipMods.None, hero.Mods.ForShip(), "Both bonuses come off the run's mods.");

            yield return world.Sector.Teardown();
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Teardown_TakesBackARunningBonus_AndLeavesTheRunsPicks()
        {
            var hero = SpawnHero();
            var pick = Track(ScriptableObject.CreateInstance<ShipMod>());
            pick.Configure("Hull plating", ShipModField.Hull, .25f);
            hero.Mods.Add(pick);
            var world = BuildSector(hero, Feel(cacheSeconds: 1000f, HullBonus(.5f)), presentation: true, phases: 2);
            yield return world.Sector.Setup();

            yield return ClaimNextBeacon(world, hero);
            Assert.AreEqual(1.75f, hero.Mods.ForShip().Factor(ShipModField.Hull), 1e-5f, "The bonus adds to the run's pick.");

            yield return world.Sector.Teardown();
            Assert.AreEqual(1.25f, hero.Mods.ForShip().Factor(ShipModField.Hull), 1e-5f, "The next sector starts without it.");
            Assert.IsEmpty(_objectives.Boosts, "The HUD line goes with it.");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheHullPatch_RestoresHullAtOnce_LeavesTheModsAlone_AndWastesWhatPassesFull()
        {
            var hero = SpawnHero();
            var changes = 0;
            hero.Mods.Changed += () => changes++;
            var feel = Feel(cacheSeconds: 1000f);
            feel.hullRestore = .3f;
            feel.hullWeight = 1f;
            var world = BuildSector(hero, feel, presentation: true, phases: 3);
            yield return world.Sector.Setup();

            hero.Damage.Health.SetFraction(.5f);
            yield return ClaimNextBeacon(world, hero);
            var claimedAt = Time.time;
            Assert.AreEqual(.8f, hero.Damage.Health.Pct, 1e-4f, "The patch restores 30% of a full hull at once.");
            Assert.AreEqual(0, changes, "The patch never touches the run's mods.");
            Assert.AreEqual(new[] { "Hull patch +30% hull" }, _objectives.Boosts.Select(boost => boost.Label));
            Assert.AreEqual(claimedAt + 3f, _objectives.Boosts[0].EndsAt, .1f, "The HUD shows it briefly.");

            hero.Damage.Health.SetFraction(1f);
            yield return ClaimNextBeacon(world, hero);
            Assert.AreEqual(1f, hero.Damage.Health.Pct, 1e-6f, "At full hull the patch is wasted.");
            Assert.AreEqual(0, changes);

            yield return WaitSeconds(() => _objectives.Boosts.Count == 0, 6f);
            Assert.IsEmpty(_objectives.Boosts, "Each patch's readout closes on its own.");
            yield return world.Sector.Teardown();
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ASectorWhoseBeaconsDontRun_GrantsNothing()
        {
            var hero = SpawnHero();
            var world = BuildSector(hero, Feel(cacheSeconds: 1000f, HullBonus(.5f)), presentation: false, phases: 2);
            yield return world.Sector.Setup();
            for (var i = 0; i < 60; i++) yield return null;

            Assert.IsFalse(world.Beacon.gameObject.activeSelf, "No beacon opens without presentation.");
            Assert.IsFalse(world.Spine.BeaconClaimed);
            Assert.AreSame(ShipMods.None, hero.Mods.ForShip());
            Assert.IsEmpty(_objectives.Boosts);
            yield return world.Sector.Teardown();
        }

        [UnityTest]
        public IEnumerator TheShieldBonus_AppliesToALiveShip_AndComesOffAgain()
        {
            _shielded = ShipTestFactory.CreateDefaultShip(Projectiles);
            yield return null;
            var authored = _shielded.Stats.shieldRegenRate;
            Assert.Greater(authored, 0f, "The default ship regenerates its shield.");
            var stack = new LullModStack();
            _shielded.BindMods(stack);
            var bonus = AssetDatabase.LoadAssetAtPath<ShipMod>(ShieldSurgePath);
            Assert.IsTrue(bonus.Fits(_shielded));

            stack.Add(bonus);
            Assert.AreEqual(authored * 2f, _shielded.Stats.shieldRegenRate, 1e-3f, "The live ship regenerates twice as fast at once.");
            stack.Remove(bonus);
            Assert.AreEqual(authored, _shielded.Stats.shieldRegenRate, 1e-3f, "Taking it back restores the authored rate.");
        }

        private sealed class World
        {
            public Sector Sector;
            public PhaseSpineModule Spine;
            public ExtractionZone Beacon;
        }

        private World BuildSector(Ship hero, BeaconCacheFeelProfile feel, bool presentation, int phases)
        {
            var template = SpawnShip(new Vector2(500f, 500f));
            template.gameObject.SetActive(false);
            var sectorGO = Track(new GameObject("LullSector"));
            var sector = sectorGO.AddComponent<Sector>();
            var directorGO = new GameObject("WaveDirector");
            directorGO.transform.SetParent(sectorGO.transform, false);
            var director = directorGO.AddComponent<WaveDirector>();
            // One-second phases sit inside the beacon's lead, so each lull's beacon opens as the last lull ends.
            director.Configure(new[] { new WaveDirector.RosterEntry { template = template } },
                Enumerable.Range(0, phases).Select(_ => new SectorPhase { seconds = 1f, interval = 1f, cap = 0 }).ToArray(),
                lullSeconds: 1f);

            var beacon = NewZone(sectorGO.transform, "Beacon");
            var spine = sectorGO.AddComponent<PhaseSpineModule>();
            spine.Bind(director, NewZone(sectorGO.transform, "Zone"), beacon);
            var cache = sectorGO.AddComponent<BeaconCache>();
            cache.Configure(feel, spine);

            sector.SetManifest(null, new SectorSpawner[] { director }, new SectorModule[] { spine, cache });
            sector.Initialize(_unitService, _objectives, presentation, default, hero);
            return new World { Sector = sector, Spine = spine, Beacon = beacon };
        }

        private static IEnumerator ClaimNextBeacon(World world, Ship hero)
        {
            yield return WaitSeconds(() => world.Beacon.gameObject.activeSelf && !world.Spine.BeaconClaimed, 10f);
            Assert.IsTrue(world.Beacon.gameObject.activeSelf, "A beacon opened.");
            ShipTestFactory.MoveKinematicShip(hero, world.Beacon.transform.position);
            yield return WaitSeconds(() => world.Spine.BeaconClaimed, 2f);
            Assert.IsTrue(world.Spine.BeaconClaimed, "Flying in claims the beacon.");
            yield return null;
            ShipTestFactory.MoveKinematicShip(hero, Substrate.GamePlane.PlanePointToWorld(Vector2.zero));
        }

        private Ship SpawnHero()
        {
            var hero = SpawnShip(Vector2.zero);
            hero.BindMods(new LullModStack());
            return hero;
        }

        // Primitive test ship: it fits a hull bonus, the one kind every ship fits.
        private Ship SpawnShip(Vector2 planePos) =>
            Track(ShipTestFactory.CreateKinematicPrimitiveShipAt(planePos).gameObject).GetComponent<Ship>();

        private ShipMod HullBonus(float amount)
        {
            var mod = Track(ScriptableObject.CreateInstance<ShipMod>());
            mod.Configure("Hull bonus", ShipModField.Hull, amount);
            return mod;
        }

        // The hull patch weighs 0 unless a test asks for it, so these rolls always take the given bonus.
        private BeaconCacheFeelProfile Feel(float cacheSeconds, params LullMod[] bonuses)
        {
            var feel = Track(ScriptableObject.CreateInstance<BeaconCacheFeelProfile>());
            feel.cacheSeconds = cacheSeconds;
            feel.bonuses = bonuses;
            feel.hullWeight = 0f;
            return feel;
        }

        private static ExtractionZone NewZone(Transform sector, string name)
        {
            var zoneGO = new GameObject(name);
            zoneGO.transform.SetParent(sector, false);
            var collider = zoneGO.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 4f;
            return zoneGO.AddComponent<ExtractionZone>();
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
