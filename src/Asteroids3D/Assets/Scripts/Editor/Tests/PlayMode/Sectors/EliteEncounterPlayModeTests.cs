#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Player;
using Game.Preview;
using NUnit.Framework;
using Objectives;
using Ships;
using Ships.Loadout;
using Substrate;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using Substrate.Services;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Sectors
{
    /// <summary>A phased sector that ends at its elite: the phases' end summons a scaled elite on its faction's signature ship and announces it with the sector's index and faction id, its hull falling past the first escalation step calls that step's escorts on the faction's common ship, its death extracts, and teardown clears it and its escorts. A spawn log watching the encounter marks the elite's life and raises the player's kill of it.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class EliteEncounterPlayModeTests : PlayModeWorldFixture
    {
        private const float Size = 2f;

        private UnitService _unitService;
        private ObjectiveService _objectives;
        private readonly List<Object> _created = new();

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
            foreach (var item in _created)
                if (item) Object.DestroyImmediate(item);
            _created.Clear();
            base.TearDown();
        }

        private T Track<T>(T item) where T : Object
        {
            _created.Add(item);
            return item;
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

        // The elite flies Crimson, the faction's signature ship; escorts fly Vanguard, its common ship.
        private (Sector sector, PhaseSpineModule spine, EliteEncounter encounter, ExtractionZone zone) EliteSector(
            Ship hero, EliteProfile profile, string factionId, int sectorIndex)
        {
            var crimson = TestAssets.LoadCrimsonPrefab();
            var vanguard = TestAssets.Load<Ship>("Assets/Prefabs/Ships/Vanguard.prefab");
            var pilot = TestAssets.LoadTestPilotMpc();
            Assert.IsTrue(crimson && crimson.Weapons && crimson.Weapons.PrimaryMountPrefab,
                "test premise: Crimson carries a primary");
            Assert.IsTrue(vanguard && vanguard.Weapons && vanguard.Weapons.PrimaryMountPrefab,
                "test premise: Vanguard carries a primary");
            var wave = Track(ShipTestFactory.CreateKinematicPrimitiveShipAt(new Vector2(500f, 500f)).gameObject).GetComponent<Ship>();
            wave.gameObject.SetActive(false);

            var faction = Track(ScriptableObject.CreateInstance<Faction>());
            faction.id = factionId;
            faction.signature = new FactionShip { ship = crimson, kit = crimson.Weapons.PrimaryMountPrefab };
            faction.common = new FactionShip { ship = vanguard, kit = vanguard.Weapons.PrimaryMountPrefab };

            var sectorGO = Track(new GameObject("EliteSector"));
            var sector = sectorGO.AddComponent<Sector>();
            var sectorFaction = sectorGO.AddComponent<SectorFaction>();
            sectorFaction.Bind(faction);
            var directorGO = new GameObject("WaveDirector");
            directorGO.transform.SetParent(sectorGO.transform, false);
            var director = directorGO.AddComponent<WaveDirector>();
            director.Configure(new[] { new WaveDirector.RosterEntry { template = wave } },
                new[] { new SectorPhase { seconds = 1f, interval = 1f, cap = 0 } }, lullSeconds: 0f);
            var eliteGO = new GameObject("Elite");
            eliteGO.transform.SetParent(sectorGO.transform, false);
            var encounter = eliteGO.AddComponent<EliteEncounter>();
            encounter.Configure(profile, sectorFaction, pilot, pilot);

            var zone = NewZone(sectorGO.transform, "Zone");
            var spine = sectorGO.AddComponent<PhaseSpineModule>();
            spine.Bind(director, zone, NewZone(sectorGO.transform, "Beacon"), elite: encounter);

            sector.SetManifest(null, new SectorSpawner[] { director }, new SectorModule[] { sectorFaction, spine });
            sector.Initialize(_unitService, _objectives, true, default, hero, sectorIndex);
            return (sector, spine, encounter, zone);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator PhasesRunOut_SummonAScaledElite_ThatEscalates_AndWhoseDeathExtracts()
        {
            var hero = Track(ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero).gameObject).GetComponent<Ship>();
            var crimson = TestAssets.LoadCrimsonPrefab();
            var vanguard = TestAssets.Load<Ship>("Assets/Prefabs/Ships/Vanguard.prefab");

            var profile = Track(ScriptableObject.CreateInstance<EliteProfile>());
            profile.name = "TestElite";
            profile.Configure("Test elite", new ShipScale(Size, 4f, 4f, 8f, 3f),
                crimson.Weapons.PrimaryMountPrefab, crimson.Weapons.SecondaryMountPrefab, escalation: new[]
                {
                    new EscalationStep { hull = 2f / 3f, escorts = 2 },
                    new EscalationStep { hull = 1f / 3f, escorts = 1 },
                });

            var (sector, spine, encounter, zone) = EliteSector(hero, profile, "bastion", sectorIndex: 2);
            var announced = new List<EliteMoment>();
            var beaten = new List<EliteMoment>();
            encounter.Announced += announced.Add;
            encounter.Beaten += beaten.Add;

            yield return sector.Setup();
            Assert.AreEqual(PhaseSpineModule.StepSurvive, _objectives.SpineStep);
            Assert.IsFalse(spine.Elite.Present, "No elite while the phases run.");

            yield return AsyncAssert.WaitUntil(() => _objectives.SpineStep == PhaseSpineModule.StepElite, 5f,
                "the phases' end summons the elite");
            Assert.IsFalse(zone.gameObject.activeSelf, "The zone stays hidden while the elite lives.");
            Assert.AreEqual(1, announced.Count, "The elite is announced once.");
            Assert.AreEqual(2, announced[0].SectorIndex);
            Assert.AreEqual("bastion", announced[0].FactionId);
            Assert.IsTrue(_unitService.ActiveRegistry.TryGetShip(announced[0].Ship, out var elite),
                "The announced id names the spawned elite.");
            Assert.IsTrue(spine.Elite.Present);
            Assert.AreEqual("Test elite", spine.Elite.Name);

            var reference = _unitService.SpawnShip(crimson, null, 0, GamePlane.PlanePointToWorld(new Vector2(-500f, -500f)),
                elite.transform.rotation, null);
            Assert.That(Vector3.Distance(crimson.transform.localScale * Size, elite.transform.localScale), Is.LessThan(1e-4f),
                "The elite's root is the template's scaled by its size.");
            Assert.AreEqual(Size * reference.ShipRadius, elite.ShipRadius, .01f * Size * reference.ShipRadius,
                "The clearance radius follows the size.");

            Ship[] Escorts() => _unitService.ActiveRegistry.ActiveShips
                .Where(ship => ship && ship != elite && ship != reference).ToArray();
            Assert.IsEmpty(Escorts());
            StagedMoments.BringHullTo(elite, .66f);
            yield return null;
            yield return null;
            var escorts = Escorts();
            Assert.AreEqual(2, escorts.Length, "Falling past two thirds calls the first step's escorts, and only those.");
            Assert.IsTrue(escorts.All(ship => ship.name.StartsWith(vanguard.name)), "Escorts fly the faction's common ship.");

            StagedMoments.Kill(elite);
            Assert.AreEqual(1, beaten.Count, "The elite's death is announced once.");
            Assert.AreEqual(announced[0].Ship, beaten[0].Ship);
            yield return AsyncAssert.WaitUntil(() => _objectives.SpineStep == PhaseSpineModule.StepExtract, 5f,
                "the elite's death opens extraction");
            Assert.IsTrue(zone.gameObject.activeSelf);
            Assert.IsFalse(spine.Elite.Present);

            yield return sector.Teardown();
            yield return null;
            Assert.IsTrue(elite == null, "Teardown despawns the elite.");
            Assert.IsTrue(escorts.All(ship => !ship), "Teardown despawns the escorts.");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator PlayersKillOfTheWatchedElite_IsLoggedElite_AndRaisesEliteKilledOnce()
        {
            var hero = Track(ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero).gameObject).GetComponent<Ship>();
            var crimson = TestAssets.LoadCrimsonPrefab();
            var profile = Track(ScriptableObject.CreateInstance<EliteProfile>());
            profile.name = "TestElite";
            profile.Configure("Test elite", new ShipScale(1.5f, 1f, 1f, 1f, 1f), crimson.Weapons.PrimaryMountPrefab);
            var (sector, _, encounter, _) = EliteSector(hero, profile, "veil", sectorIndex: 1);

            var log = new SpawnLog();
            log.Bind(_unitService, () => hero.Id);
            log.Begin(Time.time);
            log.Watch(encounter);
            var kills = new List<SpawnLog.EliteKill>();
            log.EliteKilled += kills.Add;

            yield return sector.Setup();
            yield return AsyncAssert.WaitUntil(() => _objectives.SpineStep == PhaseSpineModule.StepElite, 5f,
                "the phases' end summons the elite");
            var logged = log.Entries();
            Assert.AreEqual(1, logged.Count(entry => entry.Elite), "Only the elite's life is marked elite.");
            Assert.IsTrue(_unitService.ActiveRegistry.TryGetShip(logged.Single(entry => entry.Elite).Id, out var elite));

            TestDamage.Kill(elite, hero);

            Assert.IsTrue(log.Entries().Single(entry => entry.Id == elite.Id).KilledByPlayer);
            Assert.AreEqual(1, kills.Count, "The player's kill of the elite is raised once.");
            Assert.AreEqual(1, kills[0].SectorIndex);
            Assert.AreEqual("veil", kills[0].FactionId);
            Assert.AreEqual(crimson.name, kills[0].Chassis);

            log.Watch(null);
            log.Bind(null, null);
            yield return sector.Teardown();
        }
    }
}
#endif
