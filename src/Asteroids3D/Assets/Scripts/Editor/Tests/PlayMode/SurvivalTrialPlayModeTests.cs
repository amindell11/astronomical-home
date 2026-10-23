#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using AI;
using Damage;
using Game.Player;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Substrate;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;
using Substrate.Services;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>Survival trial end to end on primitives: the wave director's first-tick opening spawn rings the hero on the chassis's own engine, armed from the pool, the player's killing blow lands on the run tally, and the director despawns its dead product.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SurvivalTrialPlayModeTests : PlayModeWorldFixture
    {
        private const float SpawnRadius = 30f;

        private UnitService _unitService;
        private ObjectiveService _objectives;
        private ItemSubset _pool;
        private EngineModule _chassisEngine;
        private readonly List<GameObject> _created = new();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            var unitServiceGO = TrackGO(new GameObject("UnitService"));
            _unitService = unitServiceGO.AddComponent<UnitService>();
            _objectives = TrackGO(new GameObject("ObjectiveService")).AddComponent<ObjectiveService>();
            ShipServices.Compose(_unitService, unitServiceGO.transform, presentationEnabled: true);
            _chassisEngine = ScriptableObject.CreateInstance<EngineModule>();
            _pool = ScriptableObject.CreateInstance<ItemSubset>();
            _pool.shields = new[] { ScriptableObject.CreateInstance<ShieldModule>() };
            _pool.weapons = new[] { TrackGO(new GameObject("PoolWeapon")).AddComponent<PoolWeapon>() };
        }

        [TearDown]
        public override void TearDown()
        {
            if (_unitService) _unitService.Clear();

            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();

            if (_chassisEngine != null) { Object.DestroyImmediate(_chassisEngine); _chassisEngine = null; }
            if (_pool != null)
            {
                Object.DestroyImmediate(_pool.shields[0]);
                Object.DestroyImmediate(_pool);
                _pool = null;
            }

            base.TearDown();
        }

        private GameObject TrackGO(GameObject go) { _created.Add(go); return go; }

        // Primitive test ship, not a Ship_N prefab: their layer-7 colliders need LFS geometry.
        private Ship NewTemplate()
        {
            var template = TrackGO(ShipTestFactory.CreateKinematicPrimitiveShipAt(new Vector2(1000f, 1000f)).gameObject)
                .GetComponent<Ship>();
            template.engine = _chassisEngine;
            return template;
        }

        private WaveDirector NewTrial(Ship template, Ship player, out Sector sector)
        {
            var sectorGO = TrackGO(new GameObject("TrialSector"));
            sector = sectorGO.AddComponent<Sector>();
            var directorGO = new GameObject("WaveDirector");
            directorGO.transform.SetParent(sectorGO.transform, false);
            var director = directorGO.AddComponent<WaveDirector>();
            director.Configure(new[] { new WaveDirector.RosterEntry { template = template } },
                new[] { new SectorPhase { seconds = 100f, interval = 100f, cap = 1 } }, spawnRadius: SpawnRadius, loadouts: _pool);
            sector.SetManifest(null, new SectorSpawner[] { director }, null);
            sector.Initialize(_unitService, _objectives, true, default, player);
            return director;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator OpeningSpawn_PlayerKill_CountsOnTally_AndDeadProductDespawns()
        {
            var template = NewTemplate();
            var tally = new RunTally();
            Ship player = null;
            tally.Bind(_unitService, () => player.Id);
            player = _unitService.SpawnShip(template, null, 0, GamePlane.PlanePointToWorld(Vector2.zero),
                GamePlane.Rotation, null);

            var director = NewTrial(template, player, out var sector);

            yield return sector.Setup();
            tally.Begin(Time.time);
            for (var i = 0; i < 10 && director.Spawned.Count == 0; i++)
                yield return null;
            Assert.AreEqual(1, director.Spawned.Count, "The first phase opens by filling its cap.");
            var enemy = director.Spawned[0];
            Assert.AreEqual(1, enemy.teamNumber, "Director products are hostile to the team-0 player.");
            Assert.AreEqual(_pool.shields[0], enemy.Shield, "Products are re-armed from the pool at spawn.");
            Assert.AreEqual(SpawnRadius,
                Vector2.Distance(GamePlane.WorldPointToPlane(enemy.transform.position), Vector2.zero), 0.01f,
                "Products spawn on the ring around the hero.");

            enemy.Damage.TakeDamage(new DamageInfo(1e6f, DamageKind.Laser, player.Id, 0f, Vector3.zero, Vector3.zero));
            Assert.AreEqual(1, tally.Kills, "The player's killing blow counts.");

            for (var i = 0; i < 10 && director.Spawned.Count > 0; i++)
                yield return null;
            Assert.AreEqual(0, director.Spawned.Count, "The director despawns its dead product.");
            // Object.Destroy lands at end of frame.
            yield return null;
            Assert.IsFalse(enemy, "The despawned product is destroyed.");

            yield return sector.Teardown();
        }

        [UnityTest]
        public IEnumerator WaveSpawn_FliesTheChassisEngine_AndArmsTheShieldFromThePool()
        {
            var template = NewTemplate();
            var player = _unitService.SpawnShip(template, null, 0, GamePlane.PlanePointToWorld(Vector2.zero),
                GamePlane.Rotation, null);
            var director = NewTrial(template, player, out var sector);

            yield return sector.Setup();
            for (var i = 0; i < 10 && director.Spawned.Count == 0; i++)
                yield return null;

            Assert.AreEqual(1, director.Spawned.Count, "test premise: the opening wave spawned one product");
            var product = director.Spawned[0];
            Assert.AreEqual(_chassisEngine, product.Engine, "A wave product flies its chassis's own engine; the pool offers none.");
            Assert.AreEqual(_pool.shields[0], product.Shield, "The pool still arms the shield.");

            yield return sector.Teardown();
        }

        [UnityTest]
        public IEnumerator SpawnWithLoadout_AiCommanderInitialisesAgainstTheChassisEngine()
        {
            _chassisEngine.maxSpeed = 7f;

            var enemy = _unitService.SpawnShip(NewTemplate(), TestAssets.LoadTestPilotMpc(), 1,
                GamePlane.PlanePointToWorld(new Vector2(SpawnRadius, 0f)), GamePlane.Rotation, null,
                new ShipLoadout(null, _pool.shields[0], null, null));

            Assert.AreEqual(_chassisEngine, enemy.Engine, "The loadout equip leaves the chassis's engine in place.");
            Assert.AreEqual(7f, ((AICommander)enemy.Commander).Navigator.dynamics.maxSpeed,
                "The AI plans against the chassis's engine.");
            yield return null;
        }

        private sealed class PoolWeapon : Combat.Weapons.WeaponComponent
        {
            public override Combat.Projectiles.ProjectileBase Fire(UnityEngine.Vector3 targetPoint, Substrate.Services.Projectiles.IProjectileService projectiles) => null;
        }
    }
}
#endif
