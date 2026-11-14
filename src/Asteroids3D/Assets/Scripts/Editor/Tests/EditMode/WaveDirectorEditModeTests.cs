using System.Linq;
using System.Text.RegularExpressions;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Substrate.Sessions;

namespace Tests.EditMode
{
    /// <summary>Wave director pacing over a sector phase table with injected time: a phase opens with a wave, and another comes on its wave-every beat. A lull, the opening one too, sends nothing, the elite stops them, and the done table keeps the last phase's waves. An earned lull, once the phase's last wave is out and nothing is alive, moves no phase boundary. The threat read lists each group ship once and the weapons they fly, their faction kits in group order, off a prefab asset too. A bad row fails CheckRow, and validation refuses a director that misses its spine's elite.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class WaveDirectorEditModeTests
    {
        private const float Lull = 5f;
        private const float WaveEvery = 20f;

        private WaveDirector.RosterEntry[] _openA;
        private WaveDirector.RosterEntry[] _openB;
        private WaveDirector.RosterEntry[] _late;
        private SectorPhase[] _phases;

        private GameObject _go;
        private WaveDirector _director;

        [SetUp]
        public void SetUp()
        {
            _openA = new[] { new WaveDirector.RosterEntry() };
            _openB = new[] { new WaveDirector.RosterEntry() };
            _late = new[] { new WaveDirector.RosterEntry() };
            _phases = new[]
            {
                new SectorPhase
                {
                    seconds = 40f, waveEvery = WaveEvery,
                    groups = new[] { new WaveGroup { ships = _openA }, new WaveGroup { ships = _openB } },
                },
                new SectorPhase
                {
                    seconds = 40f, waveEvery = WaveEvery,
                    groups = new[] { new WaveGroup { ships = _late } },
                },
            };

            _go = new GameObject("WaveDirector");
            _director = _go.AddComponent<WaveDirector>();
            _director.Configure(_phases, Lull);
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
        public void PhaseOpen_ReturnsAWholeGroup_AndTheOpeningLullReturnsNothing()
        {
            _director.Configure(_phases, Lull, openingLullSeconds: 5f);
            _director.BeginSchedule(0f);

            _director.Advance(4f);
            Assert.IsNull(_director.WaveDue(4f), "The opening lull sends nothing.");

            _director.Advance(5f);
            var wave = _director.WaveDue(5f);
            Assert.IsTrue(wave == _openA || wave == _openB, "Phase one draws a whole group from its list.");
            Assert.AreSame(wave, _director.WaveDue(5f));
        }

        [Test]
        public void WaveEvery_SendsOnItsBeat()
        {
            OpenAndArrive(0f);

            Assert.IsNull(_director.WaveDue(WaveEvery - .1f), "The beat has not arrived.");
            Assert.IsTrue(IsOpenGroup(_director.WaveDue(WaveEvery)), "The next wave comes on the beat.");
        }

        [Test]
        public void EarnedLull_IsFalseWhileAWaveFitsOrAnythingIsAlive()
        {
            OpenAndArrive(0f);
            _director.Advance(10f);
            Assert.IsNull(_director.WaveDue(10f));
            _director.Observe(10f, 0);
            Assert.IsFalse(_director.EarnedLull, "A further wave still fits.");

            _director.Advance(WaveEvery);
            Assert.IsNotNull(_director.WaveDue(WaveEvery));
            _director.Arrived(WaveEvery);
            _director.Observe(WaveEvery, 0);
            Assert.IsTrue(_director.EarnedLull, "The last wave is sent and nothing is alive.");

            _director.Advance(WaveEvery);
            _director.Observe(WaveEvery, 1);
            Assert.IsFalse(_director.EarnedLull, "Any alive count above zero holds the lull.");

            _director.Advance(WaveEvery);
            _director.Observe(WaveEvery, 0);
            Assert.IsTrue(_director.EarnedLull);

            _director.Configure(new[]
            {
                new SectorPhase
                {
                    seconds = 40f, waveEvery = WaveEvery, groups = System.Array.Empty<WaveGroup>(),
                },
            }, Lull);
            _director.BeginSchedule(0f);
            _director.Advance(0f);
            Assert.IsNull(_director.WaveDue(0f));
            _director.Observe(0f, 0);
            Assert.IsTrue(_director.EarnedLull, "A phase with no groups earns the lull while nothing is alive.");
        }

        [Test]
        public void EarnedLull_DoesNotMoveThePhaseBoundary()
        {
            OpenAndArrive(0f);
            _director.Advance(WaveEvery);
            Assert.IsNotNull(_director.WaveDue(WaveEvery));
            _director.Arrived(WaveEvery);
            _director.Observe(WaveEvery, 0);
            Assert.IsTrue(_director.EarnedLull);

            var earned = _director.Phase;
            var clock = new GameObject("Clock");
            clock.transform.SetParent(_go.transform);
            var other = clock.AddComponent<WaveDirector>();
            other.Configure(_phases, Lull);
            other.BeginSchedule(0f);
            other.Advance(WaveEvery);
            other.Observe(WaveEvery, 0);

            Assert.IsFalse(other.EarnedLull);
            Assert.AreEqual(earned.Index, other.Phase.Index);
            Assert.AreEqual(earned.InLull, other.Phase.InLull);
            Assert.AreEqual(earned.SecondsLeft, other.Phase.SecondsLeft, 1e-4f);
            Assert.AreEqual(earned.CountdownSeconds, other.Phase.CountdownSeconds, 1e-4f);
            Assert.AreEqual(earned.Done, other.Phase.Done);

            _director.Advance(40f);
            other.Advance(40f);
            Assert.IsTrue(_director.Phase.InLull, "The scheduled lull starts when the phase's seconds run out.");
            Assert.AreEqual(other.Phase.Index, _director.Phase.Index);
            Assert.AreEqual(other.Phase.InLull, _director.Phase.InLull);
            Assert.AreEqual(other.Phase.SecondsLeft, _director.Phase.SecondsLeft, 1e-4f);

            _director.Advance(45f);
            other.Advance(45f);
            Assert.AreEqual(1, _director.Phase.Index);
            Assert.IsFalse(_director.Phase.InLull);
            Assert.AreEqual(other.Phase.Index, _director.Phase.Index);
            Assert.AreEqual(other.Phase.InLull, _director.Phase.InLull);
        }

        [Test]
        public void Alive_WithNoHunt_CountsOnlyTheDirectorsShips()
        {
            Assert.AreEqual(0, _director.Alive, "With no hunt, only the director's ships count.");
        }

        [Test]
        public void WaveDue_ReturnsTheWholeGroup_AndSlotsSpreadItAlongTheRing()
        {
            const float radius = 55f;
            const float spread = 8f;
            var ships = new[]
            {
                new WaveDirector.RosterEntry(), new WaveDirector.RosterEntry(), new WaveDirector.RosterEntry(),
            };
            _director.Configure(new[]
            {
                new SectorPhase
                {
                    seconds = 40f, waveEvery = WaveEvery,
                    groups = new[] { new WaveGroup { ships = ships } },
                },
            }, Lull, spawnRadius: radius, groupSpread: spread);
            _director.BeginSchedule(0f);
            _director.Advance(0f);

            Assert.AreSame(ships, _director.WaveDue(0f), "A wave is the whole group at once.");

            var center = new Vector2(3f, 4f);
            const float bearing = 0.4f;
            var slots = _director.Slots(center, bearing, ships.Length);
            Assert.AreEqual(ships.Length, slots.Length);
            foreach (var slot in slots)
                Assert.AreEqual(radius, Vector2.Distance(slot, center), 1e-3f);

            var bearingDeg = bearing * Mathf.Rad2Deg;
            var deltas = slots.Select(slot => Mathf.Abs(Mathf.DeltaAngle(bearingDeg, Degrees(center, slot)))).ToArray();
            Assert.That(deltas, Has.Some.LessThan(0.05f), "An odd group puts one ship on the bearing.");
            Assert.That(deltas, Has.All.LessThan(20f), "The group stays in a small arc of the bearing.");
            for (var i = 1; i < slots.Length; i++)
            {
                var step = Mathf.Abs(Mathf.DeltaAngle(Degrees(center, slots[i - 1]), Degrees(center, slots[i])));
                Assert.AreEqual(spread, step * Mathf.Deg2Rad * radius, 0.02f, "Neighbours sit groupSpread apart along the ring.");
            }
        }

        [Test]
        public void Lull_SendsNothing_AndTheNextPhaseDrawsFromItsOwnGroups()
        {
            OpenAndArrive(0f);
            _director.Advance(41f);
            Assert.IsTrue(_director.Phase.InLull);
            Assert.IsNull(_director.WaveDue(1000f), "A scheduled lull sends nothing.");

            _director.Advance(45f);
            Assert.AreSame(_late, _director.WaveDue(45f), "The next phase opens with a group from its own list.");
        }

        [Test]
        public void OpeningLull_OpensNothing_ThenPhaseOneOpensAtItsEnd()
        {
            _director.Configure(_phases, Lull, openingLullSeconds: 5f);
            _director.BeginSchedule(0f);

            _director.Advance(0f);
            Assert.IsTrue(_director.Phase.InLull, "The sector opens in a lull.");
            Assert.IsFalse(_director.OpensPhase(), "The opening lull opens nothing.");
            _director.Advance(4.9f);
            Assert.IsFalse(_director.OpensPhase());
            Assert.IsNull(_director.WaveDue(4.9f), "The opening lull sends nothing, whatever is due.");

            _director.Advance(5f);
            Assert.AreEqual(0, _director.Phase.Index);
            Assert.IsTrue(_director.OpensPhase(), "Phase one opens as the opening lull ends.");
        }

        [Test]
        public void EliteArrival_StopsWaves()
        {
            _director.Advance(0f);
            _director.OnEliteArrived(default);
            Assert.IsNull(_director.WaveDue(0f), "The elite stops waves even when one is due.");
            Assert.IsNull(_director.WaveDue(100f));
        }

        [Test]
        public void DoneTable_KeepsTheLastPhasesWaves()
        {
            _director.Advance(500f);
            Assert.IsTrue(_director.Phase.Done);
            _director.Arrived(500f);

            Assert.IsNull(_director.WaveDue(519f));
            Assert.AreSame(_late, _director.WaveDue(520f), "The done table keeps the last phase's groups and wave every.");
        }

        [Test]
        public void CheckRow_RefusesAnEmptyGroup()
        {
            var ex = Assert.Throws<System.InvalidOperationException>(() => _director.CheckRow(Row(new SectorPhase
            {
                groups = new[] { new WaveGroup { ships = System.Array.Empty<WaveDirector.RosterEntry>() } },
            })));
            StringAssert.Contains("WaveDirector", ex.Message);
        }

        [Test]
        public void CheckRow_RefusesAnUnsetTemplate()
        {
            var ex = Assert.Throws<System.InvalidOperationException>(() => _director.CheckRow(Row(new SectorPhase
            {
                groups = new[] { new WaveGroup { ships = new[] { new WaveDirector.RosterEntry() } } },
            })));
            StringAssert.Contains("WaveDirector", ex.Message);
        }

        [Test]
        public void CheckRow_AllowsAPhaseWithNoGroups()
        {
            Assert.DoesNotThrow(() =>
                _director.CheckRow(Row(new SectorPhase { groups = System.Array.Empty<WaveGroup>() })));
        }

        [Test]
        public void Threat_ListsEachGroupShipOnce_AndTheFactionKitEachFlies()
        {
            var valis = new GameObject("Valis").AddComponent<Ship>();
            var crimson = new GameObject("Crimson").AddComponent<Ship>();
            var lasers = _go.AddComponent<PoolWeapon>();
            var rippers = _go.AddComponent<PoolWeapon>();
            var faction = ScriptableObject.CreateInstance<Faction>();
            faction.signature = new FactionShip { ship = valis, kit = rippers };
            faction.common = new FactionShip { ship = crimson, kit = lasers };
            var sectorFaction = _go.AddComponent<SectorFaction>();
            var row = new SectorEntry
            {
                faction = faction,
                phases = new[]
                {
                    new SectorPhase
                    {
                        groups = new[]
                        {
                            new WaveGroup
                            {
                                ships = new[]
                                {
                                    new WaveDirector.RosterEntry { template = crimson },
                                    new WaveDirector.RosterEntry { template = valis },
                                    new WaveDirector.RosterEntry { template = crimson },
                                },
                            },
                        },
                    },
                },
            };
            _director.Configure(_phases, Lull, faction: sectorFaction);

            var threat = _director.Threat(row);

            CollectionAssert.AreEqual(new[] { crimson, valis }, threat.Ships, "a ship two groups share is listed once");
            CollectionAssert.AreEqual(new WeaponComponent[] { lasers, rippers }, threat.Weapons,
                "each group ship once, faction kits in group order");

            Object.DestroyImmediate(faction);
            Object.DestroyImmediate(valis.gameObject);
            Object.DestroyImmediate(crimson.gameObject);
        }

        [Test]
        public void Threat_WithNoFaction_ListsTheGroupShipsAuthoredWeapons()
        {
            var ship = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Vanguard.prefab");
            var row = new SectorEntry
            {
                phases = new[]
                {
                    new SectorPhase
                    {
                        groups = new[]
                        {
                            new WaveGroup { ships = new[] { new WaveDirector.RosterEntry { template = ship } } },
                        },
                    },
                },
            };

            var threat = _director.Threat(row);

            CollectionAssert.AreEqual(new[] { ship }, threat.Ships);
            CollectionAssert.AreEqual(
                new[] { ship.Weapons.PrimaryMountPrefab, ship.Weapons.SecondaryMountPrefab }.Where(weapon => weapon),
                threat.Weapons, "products fly their template's authored build");
            Assert.IsNotEmpty(threat.Weapons);
        }

        [Test]
        public void Threat_ReadsOffTheRowAndItsSectorPrefabAsset_BeforeItLoads()
        {
            var row = AssetDatabase.LoadAssetAtPath<SectorRun>("Assets/Settings/Game/SurvivalTrialRun.asset").Sectors[1];
            var threat = row.prefab.Module<PhaseSpineModule>().Threat(row);

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

        private void OpenAndArrive(float now)
        {
            _director.Advance(now);
            Assert.IsNotNull(_director.WaveDue(now));
            _director.Arrived(now);
        }

        private bool IsOpenGroup(WaveDirector.RosterEntry[] wave) => wave == _openA || wave == _openB;

        private static SectorEntry Row(SectorPhase phase) => new() { phases = new[] { phase } };

        private static float Degrees(Vector2 center, Vector2 slot)
        {
            var offset = slot - center;
            return Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        }

        private sealed class PoolWeapon : WeaponComponent
        {
            public override Combat.Projectiles.ProjectileBase Fire(UnityEngine.Vector3 targetPoint, Substrate.Services.Projectiles.IProjectileService projectiles) => null;
        }
    }
}
