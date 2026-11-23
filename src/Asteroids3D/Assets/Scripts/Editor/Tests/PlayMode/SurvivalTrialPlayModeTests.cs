#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI;
using AI.Pilots;
using AI.Tactics;
using Combat.Weapons;
using Damage;
using Game.Player.Recorders;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Keystones;
using Ships.Loadout;
using Ships.Registry;
using Ships.Presentation;
using Substrate;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using Substrate.Services;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Substrate.Sessions;

namespace Tests.PlayMode
{
    /// <summary>Survival trial end to end on primitives: the wave director's first-tick opening spawn rings the hero on the chassis's own engine, the player's killing blow lands on the run tally, and the director despawns its dead product. A product that requests a jump is despawned over the next frames, counts as nothing alive, and records no kill. A killed rival reads as a rival until it despawns. With the rival cap full, a beat's minions reinforce the live rival with the fewest followers at its free places, up to the minion cap.</summary>
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
        private readonly List<Object> _assets = new();

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
            foreach (var asset in _assets)
                if (asset) Object.DestroyImmediate(asset);
            _assets.Clear();

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
            var rig = template.gameObject.AddComponent<ShipVisualRig>();
            var serialized = new SerializedObject(template);
            serialized.FindProperty("visualRig").objectReferenceValue = rig;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return template;
        }

        private WaveDirector NewTrial(Ship template, Ship player, out Sector sector, bool sendsWaves = true,
            int fewest = 0, int most = 0, int rivalCap = 1, int minionCap = 4, Commander rivalPilot = null, Commander minionPilot = null,
            float? spawnRadius = null, float? groupSpread = null, float waveEvery = 100f)
        {
            var sectorGO = TrackGO(new GameObject("TrialSector"));
            sector = sectorGO.AddComponent<Sector>();
            var directorGO = new GameObject("WaveDirector");
            directorGO.transform.SetParent(sectorGO.transform, false);
            var director = directorGO.AddComponent<WaveDirector>();
            var faction = ScriptableObject.CreateInstance<Faction>();
            _assets.Add(faction);
            var kit = TestAssets.Load<WeaponComponent>("Assets/Prefabs/Weapons/Lasers.prefab");
            if (!rivalPilot) rivalPilot = TestAssets.LoadTestPilotMpc();
            if (!minionPilot) minionPilot = rivalPilot;
            faction.signature = new FactionShip { ship = template, kit = kit, pilot = rivalPilot };
            faction.minion = new FactionMinion
            {
                ship = template,
                kit = kit,
                pilot = minionPilot,
                scale = new ShipScale(0.75f, 1f, 1f, 1f, 0.7f),
            };
            faction.elite = new FactionElite { ship = template };
            var sectorFaction = sectorGO.AddComponent<SectorFaction>();
            sectorFaction.Bind(faction);
            var row = new SectorEntry
            {
                faction = faction,
                rivalCap = rivalCap,
                minionCap = minionCap,
                phases = new[]
                {
                    new SectorPhase
                    {
                        seconds = 100f,
                        waveEvery = waveEvery,
                        sendsWaves = sendsWaves,
                        fewestMinions = fewest,
                        mostMinions = most,
                    },
                },
            };
            director.Configure(row.phases, spawnRadius: spawnRadius ?? SpawnRadius, faction: sectorFaction,
                rivalCap: rivalCap, minionCap: minionCap, groupSpread: groupSpread ?? 8f);
            sector.SetManifest(null, new SectorSpawner[] { director }, new SectorModule[] { sectorFaction });
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
        [Timeout(600000)]
        public IEnumerator JumpRequested_DespawnsOnTheNextPass_CountsAsNothingAlive_AndFiresNoDeath()
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
            Assert.AreEqual(1, director.Spawned.Count, "test premise: the opening wave spawned one product");
            var product = director.Spawned[0];

            ((IJumpDrive)product).RequestJump();
            for (var i = 0; i < 10 && director.Spawned.Contains(product); i++)
                yield return null;
            // Object.Destroy lands at end of frame.
            yield return null;

            Assert.IsFalse(director.Spawned.Contains(product), "The director drops a product that requested a jump.");
            Assert.AreEqual(0, director.Alive, "A jumped product counts as nothing alive.");
            Assert.AreEqual(0, tally.Kills, "A jump records no kill.");
            Assert.IsFalse(product, "The despawned product is destroyed.");

            yield return sector.Teardown();
        }

        [UnityTest]
        public IEnumerator Wave_SendsOneRivalAtTheCentre_AndMinionsAtMinionScale()
        {
            var template = NewTemplate();
            var player = _unitService.SpawnShip(template, null, 0, GamePlane.PlanePointToWorld(Vector2.zero),
                GamePlane.Rotation, null);
            var rivalPilot = TestAssets.LoadTestPilotMpc();
            var minionPilot = TestAssets.Load<AICommander>("Assets/Prefabs/Pilots/AggressorPilot.prefab");
            var director = NewTrial(template, player, out var sector, fewest: 2, most: 2,
                rivalPilot: rivalPilot, minionPilot: minionPilot);

            yield return sector.Setup();
            for (var i = 0; i < 10 && director.Spawned.Count == 0; i++)
                yield return null;

            Assert.AreEqual(3, director.Spawned.Count, "The wave is one rival plus the phase's two minions.");
            Assert.That(director.Spawned[1].Commander.name, Does.StartWith(rivalPilot.name),
                "The rival takes the arc's centre slot.");
            Assert.That(director.Spawned[0].Commander.name, Does.StartWith(minionPilot.name));
            Assert.That(director.Spawned[2].Commander.name, Does.StartWith(minionPilot.name));
            var rival = director.Spawned[1];
            foreach (var minion in new[] { director.Spawned[0], director.Spawned[2] })
            {
                Assert.AreEqual(rival.transform.localScale.x * 0.75f, minion.transform.localScale.x, 1e-3f,
                    "A minion is 0.75 of the rival's root scale.");
                Assert.AreEqual(rival.Damage.maxHealth * 0.7f, minion.Damage.maxHealth, 1e-2f,
                    "A minion has 0.7 of the rival's hull.");
            }

            foreach (var ship in director.Spawned)
            {
                var commander = (AICommander)ship.Commander;
                Assert.IsTrue(commander.context.Combat.SightLimited, $"{ship.Commander.name} hunts by sight.");
                Assert.AreSame(player, commander.context.Combat.Enemy,
                    $"{ship.Commander.name} starts holding the hero.");
            }

            yield return sector.Teardown();
        }

        [UnityTest]
        public IEnumerator NoMinionRoom_SendsOnlyTheRival()
        {
            var template = NewTemplate();
            var player = _unitService.SpawnShip(template, null, 0, GamePlane.PlanePointToWorld(Vector2.zero),
                GamePlane.Rotation, null);
            var rivalPilot = TestAssets.LoadTestPilotMpc();
            var minionPilot = TestAssets.Load<AICommander>("Assets/Prefabs/Pilots/AggressorPilot.prefab");
            var director = NewTrial(template, player, out var sector, fewest: 4, most: 4, minionCap: 0,
                rivalPilot: rivalPilot, minionPilot: minionPilot);

            yield return sector.Setup();
            for (var i = 0; i < 10 && director.Spawned.Count == 0; i++)
                yield return null;

            Assert.AreEqual(1, director.Spawned.Count, "With no minion room, the wave is its rival alone.");
            Assert.That(director.Spawned[0].Commander.name, Does.StartWith(rivalPilot.name));

            yield return sector.Teardown();
        }

        [UnityTest]
        public IEnumerator PhaseThatSendsNoWaves_SpawnsNothing()
        {
            var template = NewTemplate();
            var player = _unitService.SpawnShip(template, null, 0, GamePlane.PlanePointToWorld(Vector2.zero),
                GamePlane.Rotation, null);
            var director = NewTrial(template, player, out var sector, sendsWaves: false);

            yield return sector.Setup();
            for (var i = 0; i < 5; i++)
                yield return null;

            Assert.AreEqual(0, director.Spawned.Count, "A phase with waves off sends nothing.");

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

        [UnityTest]
        public IEnumerator Wave_MinionsFollowTheRival_AtTheirPlace()
        {
            var template = NewTemplate();
            var player = _unitService.SpawnShip(template, null, 0, GamePlane.PlanePointToWorld(Vector2.zero),
                GamePlane.Rotation, null);
            var rivalPilot = TestAssets.LoadTestPilotMpc();
            var minionPilot = TestAssets.Load<AICommander>("Assets/Prefabs/Pilots/AggressorPilot.prefab");
            var spacing = minionPilot.GetComponent<ArchetypeBrain>().AuthoredTactics.lineSpacing;
            Assert.Greater(spacing, 0f, "the minion temper's line spacing");
            const float radius = 120f;
            var director = NewTrial(template, player, out var sector, fewest: 2, most: 2,
                rivalPilot: rivalPilot, minionPilot: minionPilot, spawnRadius: radius, groupSpread: spacing);

            yield return sector.Setup();
            for (var i = 0; i < 10 && director.Spawned.Count == 0; i++)
                yield return null;

            Assert.AreEqual(3, director.Spawned.Count);
            var rival = director.Spawned[1];
            Assert.That(rival.Commander.name, Does.StartWith(rivalPilot.name));
            Assert.AreEqual(radius, Vector2.Distance(rival.Kinematics.pos, player.Kinematics.pos), 0.5f,
                "A rival that does not ambush takes the arc's centre.");
            Assert.IsNull(((AICommander)rival.Commander).context.Leader);

            var plus = (AICommander)director.Spawned[2].Commander;
            var minus = (AICommander)director.Spawned[0].Commander;
            Assert.AreSame(rival, plus.context.Leader);
            Assert.AreSame(rival, minus.context.Leader);
            Assert.AreEqual(1, plus.context.Place);
            Assert.AreEqual(-1, minus.context.Place);

            var toTarget = (player.Kinematics.pos - rival.Kinematics.pos).normalized;
            var point = rival.Kinematics.pos - Vector2.Perpendicular(toTarget) * plus.context.Place * spacing;
            Assert.That(Vector2.Distance(director.Spawned[2].Kinematics.pos, point), Is.LessThan(1f),
                "place +1 spawns on -Perpendicular(rival to hero)");

            yield return sector.Teardown();
        }

        [UnityTest]
        public IEnumerator AmbushingRival_ArrivesAhead_Concealed_AndMinionsKeepTheirPlaces()
        {
            var template = NewTemplate();
            var player = _unitService.SpawnShip(template, null, 0, GamePlane.PlanePointToWorld(Vector2.zero),
                GamePlane.Rotation, null);
            CarryStealth(template);
            var rivalPilot = AmbushPilot();
            var minionPilot = TestAssets.Load<AICommander>("Assets/Prefabs/Pilots/AggressorPilot.prefab");
            var director = NewTrial(template, player, out var sector, fewest: 2, most: 2,
                rivalPilot: rivalPilot, minionPilot: minionPilot);

            yield return sector.Setup();
            for (var i = 0; i < 10 && director.Spawned.Count == 0; i++)
                yield return null;

            var rival = director.Spawned.Single(ship => ship.Commander.name.StartsWith(rivalPilot.name));
            var ahead = new SerializedObject(director).FindProperty("ambushSpawnAhead").floatValue;
            var expected = player.Kinematics.pos + player.Kinematics.Forward * ahead;
            Assert.That(Vector2.Distance(rival.Kinematics.pos, expected), Is.LessThan(0.5f),
                "an ambushing rival arrives ahead of the hero's nose");
            Assert.IsTrue(rival.Lock.IsConcealed, "it is concealed on the spawn frame");
            Assert.IsFalse(rival.Keystone.Ready, "arrival starts the kit's cooldown");

            var places = director.Spawned.Where(ship => ship != rival)
                .Select(ship => ((AICommander)ship.Commander).context)
                .OrderBy(context => context.Place)
                .ToArray();
            Assert.AreEqual(2, places.Length, "minions still take the arc");
            Assert.AreEqual(-1, places[0].Place);
            Assert.AreEqual(1, places[1].Place);
            Assert.AreSame(rival, places[0].Leader);
            Assert.AreSame(rival, places[1].Leader);
            foreach (var minion in director.Spawned.Where(ship => ship != rival))
                Assert.AreEqual(SpawnRadius, Vector2.Distance(minion.Kinematics.pos, player.Kinematics.pos), 1f);

            yield return sector.Teardown();
        }

        [UnityTest]
        public IEnumerator KilledRival_ReadsAsARival_UntilItDespawns_AndAKilledMinionDoesNot()
        {
            var template = NewTemplate();
            var tally = new RunTally();
            var victims = new List<ShipId>();
            tally.Killed += victims.Add;
            Ship player = null;
            tally.Bind(_unitService, () => player.Id);
            player = _unitService.SpawnShip(template, null, 0, GamePlane.PlanePointToWorld(Vector2.zero),
                GamePlane.Rotation, null);
            var director = NewTrial(template, player, out var sector, fewest: 1, most: 1);

            yield return sector.Setup();
            tally.Begin(Time.time);
            for (var i = 0; i < 10 && director.Spawned.Count == 0; i++)
                yield return null;
            Assert.AreEqual(2, director.Spawned.Count, "test premise: a rival and one minion");
            var rival = director.Spawned.Single(ship => ((AICommander)ship.Commander).context.Leader == null);
            var minion = director.Spawned.Single(ship => ship != rival);

            minion.Damage.TakeDamage(new DamageInfo(1e6f, DamageKind.Laser, player.Id, 0f, Vector3.zero, Vector3.zero));
            Assert.IsFalse(director.IsRival(minion.Id), "A minion is no rival.");
            rival.Damage.TakeDamage(new DamageInfo(1e6f, DamageKind.Laser, player.Id, 0f, Vector3.zero, Vector3.zero));
            Assert.IsTrue(director.IsRival(rival.Id), "The rival still reads as one on its killing blow.");
            CollectionAssert.AreEqual(new[] { minion.Id, rival.Id }, victims, "The tally names each victim.");

            var rivalId = rival.Id;
            for (var i = 0; i < 10 && director.Spawned.Count > 0; i++)
                yield return null;
            Assert.IsFalse(director.IsRival(rivalId), "A despawned rival is no longer one.");

            yield return sector.Teardown();
        }

        // Beats are ticked by hand on a long wave-every, so the frame clock never sends one.
        [UnityTest]
        public IEnumerator FullRivalCap_BeatsReinforceTheRival_AtItsFreePlaces_UpToTheMinionCap()
        {
            var template = NewTemplate();
            var player = _unitService.SpawnShip(template, null, 0, GamePlane.PlanePointToWorld(Vector2.zero),
                GamePlane.Rotation, null);
            var rivalPilot = TestAssets.LoadTestPilotMpc();
            var minionPilot = TestAssets.Load<AICommander>("Assets/Prefabs/Pilots/AggressorPilot.prefab");
            const float beat = 10f;
            var director = NewTrial(template, player, out var sector, fewest: 1, most: 1, rivalCap: 1, minionCap: 3,
                rivalPilot: rivalPilot, minionPilot: minionPilot, waveEvery: beat);
            var waves = 0;
            director.WaveSent += _ => waves++;

            yield return sector.Setup();
            for (var i = 0; i < 10 && director.Spawned.Count == 0; i++)
                yield return null;
            Assert.AreEqual(2, director.Spawned.Count, "test premise: the wave is a rival and one minion");
            var at = Time.time;
            for (var i = 0; i < 3; i++)
                director.Tick(at += beat);

            Assert.AreEqual(4, director.Spawned.Count, "Two beats add a minion each; the third finds the minion cap full.");
            Assert.AreEqual(1, waves, "A reinforcement is no wave.");
            var rival = director.Spawned.Single(ship => ship.Commander.name.StartsWith(rivalPilot.name));
            Assert.IsTrue(director.IsRival(rival.Id));
            var minions = director.Spawned.Where(ship => ship != rival)
                .Select(ship => ((AICommander)ship.Commander).context)
                .ToArray();
            Assert.IsTrue(minions.All(context => context.Leader == rival), "Every minion follows the live rival.");
            CollectionAssert.AreEquivalent(new[] { -2, -1, 1 }, minions.Select(context => context.Place),
                "The wave's minion holds +1, and reinforcements take -1, then -2.");

            yield return sector.Teardown();
        }

        [UnityTest]
        public IEnumerator FullRivalCap_SendsTheReinforcement_ToTheRivalWithFewerFollowers()
        {
            var template = NewTemplate();
            var player = _unitService.SpawnShip(template, null, 0, GamePlane.PlanePointToWorld(Vector2.zero),
                GamePlane.Rotation, null);
            var rivalPilot = TestAssets.LoadTestPilotMpc();
            var minionPilot = TestAssets.Load<AICommander>("Assets/Prefabs/Pilots/AggressorPilot.prefab");
            const float beat = 10f;
            var director = NewTrial(template, player, out var sector, fewest: 1, most: 1, rivalCap: 2, minionCap: 2,
                rivalPilot: rivalPilot, minionPilot: minionPilot, waveEvery: beat);

            yield return sector.Setup();
            for (var i = 0; i < 10 && director.Spawned.Count == 0; i++)
                yield return null;
            var at = Time.time;
            director.Tick(at += beat);
            Assert.AreEqual(4, director.Spawned.Count, "test premise: two waves of a rival and one minion");
            var second = director.Spawned.Where(ship => ship.Commander.name.StartsWith(rivalPilot.name)).Last();
            var escort = director.Spawned.Single(ship => ((AICommander)ship.Commander).context.Leader == second);
            escort.Damage.TakeDamage(new DamageInfo(1e6f, DamageKind.Laser, player.Id, 0f, Vector3.zero, Vector3.zero));
            for (var i = 0; i < 10 && director.Spawned.Contains(escort); i++)
                yield return null;
            Assert.AreEqual(3, director.Spawned.Count, "test premise: the second rival's minion is gone");

            director.Tick(at + beat);

            Assert.AreEqual(4, director.Spawned.Count, "The rival cap is full, so the beat sends one minion.");
            Assert.AreEqual(1, director.Spawned.Count(ship => ((AICommander)ship.Commander).context.Leader == second),
                "The minion joins the rival left with none.");

            yield return sector.Teardown();
        }

        private Commander AmbushPilot()
        {
            var source = TestAssets.Load<AICommander>("Assets/Prefabs/Pilots/AggressorPilot.prefab");
            var go = TrackGO(Object.Instantiate(source).gameObject);
            go.name = "AmbushPilot";
            var temper = ScriptableObject.CreateInstance<TacticsSettings>();
            _assets.Add(temper);
            temper.ambushes = true;
            var serialized = new SerializedObject(go.GetComponent<ArchetypeBrain>());
            serialized.FindProperty("tactics").objectReferenceValue = temper;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return go.GetComponent<AICommander>();
        }

        private static void CarryStealth(Ship template)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Keystones/Nightshade_Stealth.prefab");
            var stealth = root.GetComponent<Keystone>();
            Assert.IsTrue(stealth, "Nightshade's kit prefab");
            var serialized = new SerializedObject(template);
            serialized.FindProperty("keystone").objectReferenceValue = stealth;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
