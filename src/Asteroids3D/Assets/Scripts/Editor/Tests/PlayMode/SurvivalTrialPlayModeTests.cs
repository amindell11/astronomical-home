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
using Substrate.Sessions;

namespace Tests.PlayMode
{
    /// <summary>Survival trial end to end on primitives: the wave director's first-tick opening spawn rings the hero on the chassis's own engine, the player's killing blow lands on the run tally, and the director despawns its dead product.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SurvivalTrialPlayModeTests : PlayModeWorldFixture
    {
        private const float SpawnRadius = 30f;

        private UnitService _unitService;
        private ObjectiveService _objectives;
        private EngineModule _chassisEngine;
        private ShieldModule _chassisShield;
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
            _chassisShield = ScriptableObject.CreateInstance<ShieldModule>();
        }

        [TearDown]
        public override void TearDown()
        {
            if (_unitService) _unitService.Clear();

            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();

            if (_chassisEngine != null) { Object.DestroyImmediate(_chassisEngine); _chassisEngine = null; }
            if (_chassisShield != null) { Object.DestroyImmediate(_chassisShield); _chassisShield = null; }

            base.TearDown();
        }

        private GameObject TrackGO(GameObject go) { _created.Add(go); return go; }

        // Primitive test ship, not a Ship_N prefab: their layer-7 colliders need LFS geometry.
        private Ship NewTemplate()
        {
            var template = TrackGO(ShipTestFactory.CreateKinematicPrimitiveShipAt(new Vector2(1000f, 1000f)).gameObject)
                .GetComponent<Ship>();
            template.engine = _chassisEngine;
            template.shield = _chassisShield;
            return template;
        }

        private WaveDirector NewTrial(Ship template, Ship player, out Sector sector)
        {
            var sectorGO = TrackGO(new GameObject("TrialSector"));
            sector = sectorGO.AddComponent<Sector>();
            var directorGO = new GameObject("WaveDirector");
            directorGO.transform.SetParent(sectorGO.transform, false);
            var director = directorGO.AddComponent<WaveDirector>();
            var row = new SectorEntry
            {
                phases = new[]
                {
                    new SectorPhase
                    {
                        seconds = 100f,
                        cap = 1,
                        breather = 100f,
                        longestWait = 100f,
                        groups = new[]
                        {
                            new WaveGroup { ships = new[] { new WaveDirector.RosterEntry { template = template } } },
                        },
                    },
                },
            };
            director.Configure(row.phases, spawnRadius: SpawnRadius);
            sector.SetManifest(null, new SectorSpawner[] { director }, null);
            sector.Initialize(_unitService, _objectives, true, default, player, row);
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
            Assert.AreEqual(1, director.Spawned.Count, "The phase opens with its wave.");
            var enemy = director.Spawned[0];
            Assert.AreEqual(1, enemy.teamNumber, "Director products are hostile to the team-0 player.");
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
        public IEnumerator WaveSpawn_FliesTheChassisEngineAndShield()
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
            Assert.AreEqual(_chassisEngine, product.Engine, "A wave product flies its chassis's own engine.");
            Assert.AreEqual(_chassisShield, product.Shield, "A wave product flies its chassis's own shield.");

            yield return sector.Teardown();
        }

        [UnityTest]
        public IEnumerator SpawnWithLoadout_AiCommanderInitialisesAgainstTheChassisEngine()
        {
            _chassisEngine.maxSpeed = 7f;

            var enemy = _unitService.SpawnShip(NewTemplate(), TestAssets.LoadTestPilotMpc(), 1,
                GamePlane.PlanePointToWorld(new Vector2(SpawnRadius, 0f)), GamePlane.Rotation, null,
                new ShipLoadout(null, null, null));

            Assert.AreEqual(_chassisEngine, enemy.Engine, "The loadout equip leaves the chassis's engine in place.");
            Assert.AreEqual(7f, ((AICommander)enemy.Commander).Navigator.dynamics.maxSpeed,
                "The AI plans against the chassis's engine.");
            yield return null;
        }
    }
}
#endif
