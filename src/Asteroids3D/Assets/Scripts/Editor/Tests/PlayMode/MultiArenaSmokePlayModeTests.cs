#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI;
using AI.Context;
using AI.Scanning;
using Game;
using Game.Bootstrap;
using Game.Sectors;
using Game.Services;
using NUnit.Framework;
using Ships;
using Ships.Damage;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>PR-B bring-up proof: two rig-less sessions at different arena offsets compose translated, provider-isolated, and combat-isolated in one process.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class MultiArenaSmokePlayModeTests
    {
        // Min spacing ≈ 2·(fieldRadius 400 + max sensor/weapon reach ~60) ≈ 920 plane units; 1000 adds margin.
        private const float ArenaSpacing = 1000f;
        private const float PlacementTolerance = 1f;
        private const float SimSeconds = 4f;

        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/ArenaSector.prefab";
        private const string ConfigPath = "Assets/Settings/Game/DefaultSectorConfig.asset";

        private sealed class ArenaUnderTest
        {
            public GameObject Root;
            public SessionHost Host;
            public GameSession Session;
            public Vector2 Offset;
            public readonly List<Ship> ShipsInSpawnOrder = new();

            public ArenaContext Arena => Session.Services.Arena;
        }

        private readonly List<GameObject> created = new();
        private float savedTimeScale;
        private float savedMaxDelta;
        private bool savedAudioPause;
        private bool savedPresentation;

        [SetUp]
        public void SetUp()
        {
            savedTimeScale = Time.timeScale;
            savedMaxDelta = Time.maximumDeltaTime;
            savedAudioPause = AudioListener.pause;
            savedPresentation = GameSettings.PresentationEnabled;

            AudioListener.pause = true;
            // Frozen during composition so arena A cannot simulate ahead while arena B still composes.
            Time.timeScale = 0f;
            Time.maximumDeltaTime = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go) Object.DestroyImmediate(go);
            created.Clear();

            Time.timeScale = savedTimeScale;
            Time.maximumDeltaTime = savedMaxDelta;
            AudioListener.pause = savedAudioPause;
            GameSettings.SetPresentationEnabled(savedPresentation);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator TwoArenas_ComposeTranslatedByOffset_WithIsolatedFields()
        {
            var a = new ArenaUnderTest { Offset = Vector2.zero };
            var b = new ArenaUnderTest { Offset = new Vector2(ArenaSpacing, 0f) };
            yield return ComposeArena("ArenaA", a);
            yield return ComposeArena("ArenaB", b);
            yield return SettleFrames();

            var worldOffset = GamePlane.PlaneDirToWorld(b.Offset - a.Offset);

            Assert.Less(
                Vector3.Distance(b.Root.transform.position, a.Root.transform.position + worldOffset),
                PlacementTolerance, "Arena roots must sit apart by the profile offset.");
            Assert.Less(
                Vector3.Distance(
                    b.Session.ActiveSector.transform.position,
                    a.Session.ActiveSector.transform.position + worldOffset),
                PlacementTolerance, "Sector content must inherit the arena offset by hierarchy.");

            Assert.Greater(a.ShipsInSpawnOrder.Count, 0, "Arena A composed no ships");
            Assert.AreEqual(a.ShipsInSpawnOrder.Count, b.ShipsInSpawnOrder.Count,
                "Identically composed arenas must spawn the same ship roster.");
            for (var i = 0; i < a.ShipsInSpawnOrder.Count; i++)
            {
                var planeA = GamePlane.WorldPointToPlane(a.ShipsInSpawnOrder[i].transform.position);
                var planeB = GamePlane.WorldPointToPlane(b.ShipsInSpawnOrder[i].transform.position);
                Assert.Less((planeB - planeA - (b.Offset - a.Offset)).magnitude, PlacementTolerance,
                    $"Ship {i} in arena B must spawn at its arena-A twin's position translated by the offset.");
            }

            var fieldA = a.Arena.ObstacleField;
            var fieldB = b.Arena.ObstacleField;
            Assert.IsNotNull(fieldA, "Arena A's sector must register its asteroid field on A's arena handle");
            Assert.IsNotNull(fieldB, "Arena B's sector must register its asteroid field on B's arena handle");
            Assert.AreNotSame(fieldA, fieldB, "Each arena must carry its own obstacle-field provider");

            var buffer = new DetectedObstacle[64];
            Assert.Greater(fieldA.QueryObstacles(a.Offset, 300f, buffer), 0,
                "Arena A's field must hold asteroids around A's origin");
            Assert.Greater(fieldB.QueryObstacles(b.Offset, 300f, buffer), 0,
                "Arena B's field must hold asteroids around B's origin");
            Assert.AreEqual(0, fieldA.QueryObstacles(b.Offset, 300f, buffer),
                "Arena A's field must be empty at B's origin — a consumer wired to A can never sense B's rocks");
            Assert.AreEqual(0, fieldB.QueryObstacles(a.Offset, 300f, buffer),
                "Arena B's field must be empty at A's origin");

            var found = fieldB.QueryObstacles(b.Offset, 300f, buffer);
            for (var i = 0; i < found; i++)
                Assert.Less((buffer[i].position - b.Offset).magnitude, ArenaSpacing / 2f,
                    "Every rock B senses must lie inside B's own half of the plane");

            yield return TeardownArena(a);
            yield return TeardownArena(b);
        }

        // Twin-trajectory mirroring is deliberately NOT asserted: float non-invariance + unordered physics queries make identically seeded arenas diverge chaotically.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator TwoArenas_SimulateConcurrently_StayIsolated()
        {
            var a = new ArenaUnderTest { Offset = Vector2.zero };
            var b = new ArenaUnderTest { Offset = new Vector2(ArenaSpacing, 0f) };
            yield return ComposeArena("ArenaA", a);
            yield return ComposeArena("ArenaB", b);
            yield return SettleFrames();

            Assert.Greater(a.ShipsInSpawnOrder.Count, 0, "Arena A composed no ships");
            Assert.AreEqual(a.ShipsInSpawnOrder.Count, b.ShipsInSpawnOrder.Count);

            // Arranged pre-window so the sim window's steps double as the scan cadence.
            var probeA = ArrangeScanProbe(a);
            var probeB = ArrangeScanProbe(b);

            var spawnPlaneA = RecordSpawnPositions(a);
            var spawnPlaneB = RecordSpawnPositions(b);

            var combat = new CombatLog(a, b);

            // Both arenas take their first simulated step together.
            Time.timeScale = 20f;
            var elapsed = 0f;
            while (elapsed < SimSeconds)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }
            // Teardown is frame-bound: at 20x (or with a permissive maximumDeltaTime) every rendered frame drags a multi-step 16-ship fixed batch.
            Time.timeScale = 1f;
            Time.maximumDeltaTime = Time.fixedDeltaTime;

            AssertShipsDisplaced(a, spawnPlaneA);
            AssertShipsDisplaced(b, spawnPlaneB);

            Assert.IsTrue(HasAcquired(probeA),
                "An arena-A ship in scanner range of a hostile all window never acquired one — the scan/targeting stack is dead in the composition.");
            Assert.IsTrue(HasAcquired(probeB),
                "An arena-B ship in scanner range of a hostile all window never acquired one — the scan/targeting stack is dead in the composition.");
            AssertAcquiredEnemiesInOwnHalf(a);
            AssertAcquiredEnemiesInOwnHalf(b);

            ApplySameArenaDamage(a);
            ApplySameArenaDamage(b);
            combat.Unsubscribe();

            Assert.Greater(combat.CombatDamageIn(a), 0,
                "Arena-A ship-vs-ship damage was not recorded — attribution never reached the arena's CombatLog.");
            Assert.Greater(combat.CombatDamageIn(b), 0,
                "Arena-B ship-vs-ship damage was not recorded — attribution never reached the arena's CombatLog.");
            Assert.IsEmpty(combat.CrossArenaHits,
                "Every attacker must belong to its victim's own arena:\n" + string.Join("\n", combat.CrossArenaHits));

            AssertShipsStayInOwnHalf(a);
            AssertShipsStayInOwnHalf(b);

            foreach (var ship in a.ShipsInSpawnOrder)
                if (ship)
                    Assert.IsFalse(b.Session.Services.UnitService.ActiveRegistry.ActiveShips.Contains(ship),
                        "An arena-A ship leaked into arena B's registry.");
            foreach (var ship in b.ShipsInSpawnOrder)
                if (ship)
                    Assert.IsFalse(a.Session.Services.UnitService.ActiveRegistry.ActiveShips.Contains(ship),
                        "An arena-B ship leaked into arena A's registry.");

            var buffer = new DetectedObstacle[64];
            Assert.AreEqual(0, a.Arena.ObstacleField.QueryObstacles(b.Offset, 300f, buffer),
                "After simulating, arena A's field must still be empty at B's origin.");
            Assert.AreEqual(0, b.Arena.ObstacleField.QueryObstacles(a.Offset, 300f, buffer),
                "After simulating, arena B's field must still be empty at A's origin.");

            yield return TeardownArena(a);
            yield return TeardownArena(b);
        }

        // Records ship-vs-ship damage per arena via the existing OnDamaged event; LastAttackerId is updated before it fires.
        private sealed class CombatLog
        {
            private readonly Dictionary<ArenaUnderTest, int> combatDamage = new();
            private readonly List<(DamageController damage, System.Action<float, Vector3> handler)> subscriptions = new();
            public readonly List<string> CrossArenaHits = new();

            public CombatLog(ArenaUnderTest a, ArenaUnderTest b)
            {
                combatDamage[a] = 0;
                combatDamage[b] = 0;
                foreach (var ship in a.ShipsInSpawnOrder) Subscribe(ship, own: a, other: b);
                foreach (var ship in b.ShipsInSpawnOrder) Subscribe(ship, own: b, other: a);
            }

            public int CombatDamageIn(ArenaUnderTest arena) => combatDamage[arena];

            public void Unsubscribe()
            {
                foreach (var (damage, handler) in subscriptions)
                    if (damage) damage.OnDamaged -= handler;
                subscriptions.Clear();
            }

            private void Subscribe(Ship victim, ArenaUnderTest own, ArenaUnderTest other)
            {
                if (!victim || !victim.Damage) return;
                var damage = victim.Damage;
                var otherRegistry = other.Arena.Registry;
                void Handler(float _, Vector3 __)
                {
                    var attackerId = damage.LastAttackerId;
                    if (!attackerId.IsValid) return;
                    combatDamage[own]++;
                    if (otherRegistry.TryGetShip(attackerId, out var attacker))
                        CrossArenaHits.Add(
                            $"{victim.name} in {own.Root.name} was damaged by {attacker.name} from {other.Root.name}");
                }
                damage.OnDamaged += Handler;
                subscriptions.Add((damage, Handler));
            }
        }

        private static Vector2[] RecordSpawnPositions(ArenaUnderTest arena)
        {
            var positions = new Vector2[arena.ShipsInSpawnOrder.Count];
            for (var i = 0; i < positions.Length; i++)
                positions[i] = GamePlane.WorldPointToPlane(arena.ShipsInSpawnOrder[i].transform.position);
            return positions;
        }

        private static void AssertShipsDisplaced(ArenaUnderTest arena, Vector2[] spawnPlane)
        {
            var maxDisplacement = 0f;
            for (var i = 0; i < arena.ShipsInSpawnOrder.Count; i++)
            {
                var ship = arena.ShipsInSpawnOrder[i];
                if (!ship) continue;
                maxDisplacement = Mathf.Max(maxDisplacement,
                    (GamePlane.WorldPointToPlane(ship.transform.position) - spawnPlane[i]).magnitude);
            }
            Assert.Greater(maxDisplacement, 1f,
                $"No {arena.Root.name} ship moved — the composed AI ships never simulated.");
        }

        private static IEnumerable<EnemyTracker> AcquiredTrackers(ArenaUnderTest arena) =>
            arena.ShipsInSpawnOrder
                .Where(ship => ship)
                .Select(ship => (ship.Commander as AICommander)?.context?.Combat)
                .Where(combat => combat != null && combat.HasEnemy);

        private static AICommander ArrangeScanProbe(ArenaUnderTest arena)
        {
            var live = arena.ShipsInSpawnOrder.Where(ship => ship).ToArray();
            var scout = live.FirstOrDefault(ship => ship.Commander is AICommander);
            Assert.IsNotNull(scout, $"{arena.Root.name} has no live AI ship for the scan probe.");
            var hostile = live.FirstOrDefault(ship => ship.teamNumber != scout.teamNumber);
            Assert.IsNotNull(hostile, $"{arena.Root.name} has no live hostile for the scan probe.");

            var beside = hostile.transform.position + GamePlane.PlaneDirToWorld(Vector2.right) * 8f;
            scout.Rigidbody.position = beside;
            scout.transform.position = beside;
            // Invulnerable so a mid-window kill cannot drop the acquisition being asserted.
            scout.Damage.SetInvulnerability(SimSeconds * 4f);
            hostile.Damage.SetInvulnerability(SimSeconds * 4f);

            return (AICommander)scout.Commander;
        }

        private static bool HasAcquired(AICommander cmdr) => cmdr.context?.Combat?.HasEnemy == true;

        private static void AssertAcquiredEnemiesInOwnHalf(ArenaUnderTest arena)
        {
            foreach (var tracker in AcquiredTrackers(arena))
                Assert.Less((tracker.EnemyPos - arena.Offset).magnitude, ArenaSpacing / 2f,
                    $"A {arena.Root.name} ship acquired an enemy outside its own arena half — targeting leaked across arenas.");
        }

        private static void ApplySameArenaDamage(ArenaUnderTest arena)
        {
            var live = arena.ShipsInSpawnOrder.Where(ship => ship && ship.Damage).Take(2).ToArray();
            Assert.AreEqual(2, live.Length, $"{arena.Root.name} needs two live ships for the attribution probe.");
            live[0].Damage.SetInvulnerability(0f);
            live[0].Damage.TakeDamage(1f, 0f, Vector3.zero, Vector3.zero, live[1].gameObject);
        }

        private static void AssertShipsStayInOwnHalf(ArenaUnderTest arena)
        {
            foreach (var ship in arena.ShipsInSpawnOrder)
            {
                if (!ship) continue;
                var rel = GamePlane.WorldPointToPlane(ship.transform.position) - arena.Offset;
                Assert.Less(rel.magnitude, ArenaSpacing / 2f,
                    $"{arena.Root.name} ship strayed {rel.magnitude:F0} units from its arena origin — spacing invariant violated.");
            }
        }

        private IEnumerator ComposeArena(string name, ArenaUnderTest arena)
        {
            var sectorPrefab = AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath);
            Assert.IsNotNull(sectorPrefab, $"Sector prefab missing at {SectorPrefabPath}");
            var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(ConfigPath);
            Assert.IsNotNull(config, $"Sector config missing at {ConfigPath}");

            arena.Root = new GameObject(name);
            created.Add(arena.Root);
            arena.Host = arena.Root.AddComponent<SessionHost>();
            arena.Session = new GameSession
            {
                Profile = new SessionProfile
                {
                    sectorEntry = new SectorEntry { prefab = sectorPrefab, config = config },
                    buildPlayer = false,
                    presentation = false,
                    offset = arena.Offset
                }
            };

            yield return arena.Host.ComposeSession(arena.Session);

            arena.Session.Services.UnitService.OnShipSpawned += arena.ShipsInSpawnOrder.Add;
            yield return arena.Host.LoadSector(arena.Session);
        }

        // The asteroid field fills in its own Start, one frame after LoadSector returns.
        private static IEnumerator SettleFrames()
        {
            for (var i = 0; i < 3; i++)
                yield return null;
        }

        private IEnumerator TeardownArena(ArenaUnderTest arena)
        {
            yield return arena.Host.UnloadSector(arena.Session);
            yield return arena.Host.TeardownSession(arena.Session);
        }
    }
}
#endif
