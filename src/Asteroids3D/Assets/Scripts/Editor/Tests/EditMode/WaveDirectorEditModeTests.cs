using System.Linq;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Wave director pacing over a sector phase table with injected time: each phase opens once, its interval gates cadence and its cap gates count, a lull spawns nothing, and the done table keeps the last phase's pacing. Loadout draws come from the pool in force and leave the second mount empty, and the threat read lists the roster's ships and the weapons in force, off a prefab asset too.</summary>
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
        public void PoolFor_PrefersThePhasesPool_OverTheDirectors()
        {
            var directorPool = ScriptableObject.CreateInstance<ItemSubset>();
            var phasePool = ScriptableObject.CreateInstance<ItemSubset>();
            var phases = Phases();
            phases[0].loadouts = phasePool;
            _director.Configure(null, phases, Lull, loadouts: directorPool);

            Assert.AreSame(phasePool, _director.PoolFor(phases[0]));
            Assert.AreSame(directorPool, _director.PoolFor(phases[1]), "A phase naming no pool uses the director's.");

            Object.DestroyImmediate(directorPool);
            Object.DestroyImmediate(phasePool);
        }

        [Test]
        public void Draw_StaysInsidePool_AndLeavesTheSecondMountEmpty()
        {
            var pool = ScriptableObject.CreateInstance<ItemSubset>();
            pool.shields = new[] { ScriptableObject.CreateInstance<ShieldModule>(), ScriptableObject.CreateInstance<ShieldModule>() };
            pool.weapons = new WeaponComponent[]
            {
                _go.AddComponent<PoolWeapon>(), _go.AddComponent<PoolWeapon>(), _go.AddComponent<PoolWeapon>(),
            };

            for (var i = 0; i < 200; i++)
            {
                var draw = WaveDirector.Draw(null, pool);
                CollectionAssert.Contains(pool.shields, draw.Shield);
                CollectionAssert.Contains(pool.weapons, draw.PrimaryWeapon);
                Assert.IsFalse(draw.SecondaryWeapon, "Products fly primary-only.");
            }

            foreach (var module in pool.shields) Object.DestroyImmediate(module);
            Object.DestroyImmediate(pool);
        }

        [Test]
        public void Threat_ListsEachRosterShipOnce_AndTheWeaponsOfEveryPoolInForce()
        {
            var vanguard = new GameObject("Vanguard").AddComponent<Ship>();
            var valis = new GameObject("Valis").AddComponent<Ship>();
            var lasers = _go.AddComponent<PoolWeapon>();
            var rippers = _go.AddComponent<PoolWeapon>();
            var missiles = _go.AddComponent<PoolWeapon>();
            var directorPool = ScriptableObject.CreateInstance<ItemSubset>();
            directorPool.weapons = new WeaponComponent[] { lasers, rippers };
            var phasePool = ScriptableObject.CreateInstance<ItemSubset>();
            phasePool.weapons = new WeaponComponent[] { missiles, lasers };
            var phases = Phases();
            phases[1].loadouts = phasePool;
            _director.Configure(new[]
            {
                new WaveDirector.RosterEntry { template = vanguard },
                new WaveDirector.RosterEntry { template = valis },
                new WaveDirector.RosterEntry { template = vanguard },
            }, phases, Lull, loadouts: directorPool);

            var threat = _director.Threat();

            CollectionAssert.AreEqual(new[] { vanguard, valis }, threat.Ships, "a ship two pilots fly is listed once");
            CollectionAssert.AreEqual(new WeaponComponent[] { lasers, rippers, missiles }, threat.Weapons,
                "phase order, each weapon once");

            Object.DestroyImmediate(directorPool);
            Object.DestroyImmediate(phasePool);
            Object.DestroyImmediate(vanguard.gameObject);
            Object.DestroyImmediate(valis.gameObject);
        }

        [Test]
        public void Threat_WithNoPool_ListsTheRosterShipsAuthoredWeapons()
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

        private sealed class PoolWeapon : WeaponComponent
        {
            public override Combat.Projectiles.ProjectileBase Fire(UnityEngine.Vector3 targetPoint, Substrate.Services.Projectiles.IProjectileService projectiles) => null;
        }
    }
}
