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
using Substrate.Services;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>Survival trial end to end on primitives: the wave director's first-tick opening spawn rings the hero armed from the pool, the player's killing blow lands on the run tally, and the director despawns its dead product.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SurvivalTrialPlayModeTests : PlayModeWorldFixture
    {
        private const float SpawnRadius = 30f;

        private UnitService _unitService;
        private ObjectiveService _objectives;
        private SectorSettings _config;
        private ItemSubset _pool;
        private readonly List<GameObject> _created = new();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            var unitServiceGO = TrackGO(new GameObject("UnitService"));
            _unitService = unitServiceGO.AddComponent<UnitService>();
            _objectives = TrackGO(new GameObject("ObjectiveService")).AddComponent<ObjectiveService>();
            ShipServices.Compose(_unitService, unitServiceGO.transform, presentationEnabled: true);
            _config = ScriptableObject.CreateInstance<SectorSettings>();
            _pool = ScriptableObject.CreateInstance<ItemSubset>();
            _pool.engines = new[] { ScriptableObject.CreateInstance<EngineModule>() };
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

            if (_config != null) { Object.DestroyImmediate(_config); _config = null; }
            if (_pool != null)
            {
                Object.DestroyImmediate(_pool.engines[0]);
                Object.DestroyImmediate(_pool.shields[0]);
                Object.DestroyImmediate(_pool);
                _pool = null;
            }

            base.TearDown();
        }

        private GameObject TrackGO(GameObject go) { _created.Add(go); return go; }

        // Primitive test ship, not a Ship_N prefab: their layer-7 colliders need LFS geometry.
        private Ship NewTemplate() =>
            TrackGO(ShipTestFactory.CreateKinematicPrimitiveShipAt(new Vector2(1000f, 1000f)).gameObject)
                .GetComponent<Ship>();

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

            var sectorGO = TrackGO(new GameObject("TrialSector"));
            var sector = sectorGO.AddComponent<Sector>();
            var directorGO = new GameObject("WaveDirector");
            directorGO.transform.SetParent(sectorGO.transform, false);
            var director = directorGO.AddComponent<WaveDirector>();
            director.Configure(new[] { new WaveDirector.RosterEntry { template = template } },
                startInterval: 100f, endInterval: 100f, startCap: 1, endCap: 1, rampSeconds: 100f, SpawnRadius, _pool);
            sector.SetManifest(null, new SectorSpawner[] { director }, null);
            sector.Initialize(_unitService, _objectives, true, _config, default, player);

            yield return sector.Setup();
            tally.Begin(Time.time);
            for (var i = 0; i < 10 && director.Spawned.Count == 0; i++)
                yield return null;
            Assert.AreEqual(1, director.Spawned.Count, "The first tick's opening wave fills the start cap.");
            var enemy = director.Spawned[0];
            Assert.AreEqual(1, enemy.teamNumber, "Director products are hostile to the team-0 player.");
            Assert.AreEqual(_pool.engines[0], enemy.Engine, "Products are re-armed from the pool at spawn.");
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
        public IEnumerator SpawnWithLoadout_AiCommanderInitialisesAgainstTheEquippedEngine()
        {
            var engine = _pool.engines[0];
            engine.maxSpeed = 7f;

            var enemy = _unitService.SpawnShip(NewTemplate(), TestAssets.LoadTestPilotMpc(), 1,
                GamePlane.PlanePointToWorld(new Vector2(SpawnRadius, 0f)), GamePlane.Rotation, null,
                new ShipLoadout(null, engine, _pool.shields[0], null, null));

            Assert.AreEqual(engine, enemy.Engine);
            Assert.AreEqual(7f, ((AICommander)enemy.Commander).Navigator.dynamics.maxSpeed,
                "The AI plans against the equipped engine, not the template's.");
            yield return null;
        }

        private sealed class PoolWeapon : Combat.Weapons.WeaponComponent
        {
            public override Combat.Projectiles.ProjectileBase Fire(Substrate.Services.Projectiles.IProjectileService projectiles) => null;
        }
    }
}
#endif
