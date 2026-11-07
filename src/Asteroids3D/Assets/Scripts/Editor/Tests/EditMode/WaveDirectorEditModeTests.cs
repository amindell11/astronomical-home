using System.Linq;
using System.Text.RegularExpressions;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    /// <summary>Wave director pacing over a sector phase table with injected time: each phase opens once, its interval gates cadence and its cap gates count, a lull (the opening one too) spawns nothing, and the done table keeps the last phase's pacing. The threat read lists the roster's ships and the weapons they fly, their faction kits when the sector names a faction, off a prefab asset too. Validation refuses a director that misses its spine's elite.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class WaveDirectorEditModeTests
    {
        private const float Lull = 5f;

        private static SectorPhase[] Phases() => new[]
        {
            new SectorPhase { seconds = 40f, interval = 6f, cap = 2 },
            new SectorPhase { seconds = 40f, interval = 4f, cap = 3 },
        };

        private GameObject _go;
        private WaveDirector _director;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("WaveDirector");
            _director = _go.AddComponent<WaveDirector>();
            _director.Configure(null, Phases(), Lull);
            _director.BeginSchedule(0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
        }

        [Test]
        public void Opening_FiresOncePerPhase_AndNeverInALullOrOnceDone()
        {
            _director.Advance(0f);
            Assert.IsTrue(_director.OpensPhase(), "The first tick opens phase one.");
            Assert.IsFalse(_director.OpensPhase(), "A phase opens once.");

            _director.Advance(42f);
            Assert.IsTrue(_director.Phase.InLull);
            Assert.IsFalse(_director.OpensPhase(), "A lull opens nothing.");

            _director.Advance(45f);
            Assert.AreEqual(1, _director.Phase.Index);
            Assert.IsTrue(_director.OpensPhase(), "The phase after the lull opens.");

            _director.Advance(200f);
            Assert.IsTrue(_director.Phase.Done);
            Assert.IsFalse(_director.OpensPhase(), "The done table opens nothing.");
        }

        [Test]
        public void Cadence_NextSpawnWaitsTheRunningPhasesInterval()
        {
            _director.Advance(0f);
            _director.ScheduleNext(0f);
            Assert.IsFalse(_director.SpawnDue(5.99f, 0), "The opening covers the first interval.");
            Assert.IsTrue(_director.SpawnDue(6f, 0));

            _director.Advance(45f);
            _director.ScheduleNext(45f);
            Assert.IsFalse(_director.SpawnDue(48.99f, 0));
            Assert.IsTrue(_director.SpawnDue(49f, 0), "Phase two's shorter interval paces it.");
        }

        [Test]
        public void Cap_IsTheRunningPhases_AndHoldsSpawnsUntilOneDies()
        {
            _director.Advance(0f);
            _director.ScheduleNext(0f);
            Assert.IsFalse(_director.SpawnDue(6f, 2), "At phase one's cap, a due spawn must hold.");
            Assert.IsTrue(_director.SpawnDue(6f, 1), "A death under the cap releases the held spawn.");

            _director.Advance(45f);
            _director.ScheduleNext(45f);
            Assert.IsTrue(_director.SpawnDue(49f, 2), "Phase two raises the cap.");
        }

        [Test]
        public void Lull_SpawnsNothing()
        {
            _director.Advance(0f);
            _director.ScheduleNext(0f);
            _director.Advance(42f);
            Assert.IsFalse(_director.SpawnDue(42f, 0), "A lull spawns nothing, whatever is due.");
        }

        [Test]
        public void OpeningLull_OpensAndSpawnsNothing_ThenPhaseOneOpensAtItsEnd()
        {
            _director.Configure(null, Phases(), Lull, openingLullSeconds: 5f);
            _director.BeginSchedule(0f);

            _director.Advance(0f);
            Assert.IsTrue(_director.Phase.InLull, "The sector opens in a lull.");
            Assert.IsFalse(_director.OpensPhase(), "The opening lull opens nothing.");
            _director.Advance(4.9f);
            Assert.IsFalse(_director.OpensPhase());
            Assert.IsFalse(_director.SpawnDue(4.9f, 0), "The opening lull spawns nothing, whatever is due.");

            _director.Advance(5f);
            Assert.AreEqual(0, _director.Phase.Index);
            Assert.IsTrue(_director.OpensPhase(), "Phase one opens as the opening lull ends.");
        }

        [Test]
        public void DoneTable_KeepsTheLastPhasesPacing()
        {
            _director.Advance(500f);
            _director.ScheduleNext(500f);
            Assert.IsTrue(_director.Phase.Done);
            Assert.IsFalse(_director.SpawnDue(503.9f, 0));
            Assert.IsTrue(_director.SpawnDue(504f, 2), "Pursuit runs at the last phase's interval and cap.");
            Assert.IsFalse(_director.SpawnDue(504f, 3));
        }

        [Test]
        public void Threat_ListsEachRosterShipOnce_AndTheFactionKitEachFlies()
        {
            var valis = new GameObject("Valis").AddComponent<Ship>();
            var crimson = new GameObject("Crimson").AddComponent<Ship>();
            var lasers = _go.AddComponent<PoolWeapon>();
            var rippers = _go.AddComponent<PoolWeapon>();
            var faction = ScriptableObject.CreateInstance<Faction>();
            faction.signature = new FactionShip { ship = valis, kit = rippers };
            faction.common = new FactionShip { ship = crimson, kit = lasers };
            var sectorFaction = _go.AddComponent<SectorFaction>();
            sectorFaction.Bind(faction);
            _director.Configure(new[]
            {
                new WaveDirector.RosterEntry { template = crimson },
                new WaveDirector.RosterEntry { template = valis },
                new WaveDirector.RosterEntry { template = crimson },
            }, Phases(), Lull, faction: sectorFaction);

            var threat = _director.Threat();

            CollectionAssert.AreEqual(new[] { crimson, valis }, threat.Ships, "a ship two pilots fly is listed once");
            CollectionAssert.AreEqual(new WeaponComponent[] { lasers, rippers }, threat.Weapons, "each ship's faction kit, in roster order");

            Object.DestroyImmediate(faction);
            Object.DestroyImmediate(valis.gameObject);
            Object.DestroyImmediate(crimson.gameObject);
        }

        [Test]
        public void Threat_WithNoFaction_ListsTheRosterShipsAuthoredWeapons()
        {
            var ship = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Vanguard.prefab");
            _director.Configure(new[] { new WaveDirector.RosterEntry { template = ship } }, Phases(), Lull);

            var threat = _director.Threat();

            CollectionAssert.AreEqual(new[] { ship }, threat.Ships);
            CollectionAssert.AreEqual(
                new[] { ship.Weapons.PrimaryMountPrefab, ship.Weapons.SecondaryMountPrefab }.Where(weapon => weapon),
                threat.Weapons, "products fly their template's authored build");
            Assert.IsNotEmpty(threat.Weapons);
        }

        [Test]
        public void Threat_ReadsOffTheSectorPrefabAsset_BeforeItLoads()
        {
            var sector = AssetDatabase.LoadAssetAtPath<Sector>("Assets/Prefabs/Sectors/TrialSector2.prefab");
            var threat = sector.Module<PhaseSpineModule>().Threat();

            Assert.IsNotEmpty(threat.Ships);
            Assert.IsNotEmpty(threat.Weapons);
            CollectionAssert.AllItemsAreUnique(threat.Ships);
            CollectionAssert.AllItemsAreUnique(threat.Weapons);
        }

        [Test]
        public void Validation_RefusesADirectorThatMissesItsSpinesElite()
        {
            var sector = new GameObject("Sector");
            _go.transform.SetParent(sector.transform, false);
            var encounter = new GameObject("Elite").AddComponent<EliteEncounter>();
            encounter.transform.SetParent(sector.transform, false);
            sector.AddComponent<PhaseSpineModule>().Bind(_director, null, null, elite: encounter);
            using var serialized = new SerializedObject(_director);

            LogAssert.Expect(LogType.Exception, new Regex("must watch its spine's elite"));
            serialized.FindProperty("team").intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            serialized.FindProperty("elite").objectReferenceValue = encounter;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            LogAssert.NoUnexpectedReceived();

            Object.DestroyImmediate(sector);
        }

        private sealed class PoolWeapon : WeaponComponent
        {
            public override Combat.Projectiles.ProjectileBase Fire(UnityEngine.Vector3 targetPoint, Substrate.Services.Projectiles.IProjectileService projectiles) => null;
        }
    }
}
