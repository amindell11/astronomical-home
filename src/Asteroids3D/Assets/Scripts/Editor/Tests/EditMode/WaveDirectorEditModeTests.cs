using System.Linq;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Substrate.Sessions;

namespace Tests.EditMode
{
    /// <summary>Wave director pacing over a sector phase table with injected time: a phase opens with a wave, and another comes on its wave-every beat. Minions that fit under the minion cap arrive whole, ones that only partly fit are trimmed to the room left, a rival comes only under the rival cap, and a wave with no room is dropped so the next waits for the beat. A lull, the opening one too, sends nothing, a finished table with an elite sends nothing, and the done table keeps the last phase's waves. A closing lull before the elite sends nothing. A lull window covers a following lull, its grace, and an earned lull, and stays shut for the opening lull and the first phase's first seconds. An earned lull, once the phase's last wave is out and nothing is alive, moves no phase boundary. The threat read lists a chassis every rank shares once and the rival's and minion's kits, off a prefab asset too. A bad row fails CheckRow.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class WaveDirectorEditModeTests
    {
        private const float Lull = 5f;
        private const float WaveEvery = 20f;
        private const int OpenMinions = 2;
        private const int LateMinions = 5;

        private SectorPhase[] _phases;

        private GameObject _go;
        private WaveDirector _director;

        [SetUp]
        public void SetUp()
        {
            _phases = new[]
            {
                new SectorPhase
                {
                    seconds = 40f, waveEvery = WaveEvery, sendsWaves = true,
                    fewestMinions = OpenMinions, mostMinions = OpenMinions,
                },
                new SectorPhase
                {
                    seconds = 40f, waveEvery = WaveEvery, sendsWaves = true,
                    fewestMinions = LateMinions, mostMinions = LateMinions,
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
            Assert.AreEqual(OpenMinions, wave, "Phase one draws its minion count.");
            Assert.AreEqual(wave, _director.WaveDue(5f));
        }

        [Test]
        public void WaveEvery_SendsOnItsBeat()
        {
            OpenAndArrive(0f);

            Assert.IsNull(_director.WaveDue(WaveEvery - .1f), "The beat has not arrived.");
            Assert.AreEqual(OpenMinions, _director.WaveDue(WaveEvery), "The next wave comes on the beat.");
        }

        [Test]
        public void Minions_ArriveWhole_WhileTheyFitUnderTheMinionCap()
        {
            _director.Configure(_phases, Lull, minionCap: 6);
            Assert.AreEqual(4, _director.MinionRoom(4, 2), "Minions that fit under the minion cap arrive whole.");
        }

        [Test]
        public void Minions_TrimToTheRoomLeft_WhenOnlySomeFit()
        {
            _director.Configure(_phases, Lull, minionCap: 6);
            Assert.AreEqual(2, _director.MinionRoom(4, 4),
                "Four minions alive under a minion cap of 6 leave room for two.");
        }

        [Test]
        public void Rival_Comes_OnlyWhileLiveRivalsSitUnderTheRivalCap()
        {
            _director.Configure(_phases, Lull, rivalCap: 2);
            Assert.IsTrue(_director.RivalRoom(1), "One rival alive under a rival cap of 2 leaves room.");
            Assert.IsFalse(_director.RivalRoom(2), "Two rivals alive fill a rival cap of 2.");
        }

        [Test]
        public void DroppedWave_IsNotHeld_AndTheNextComesOnTheBeat()
        {
            _director.Configure(_phases, Lull, rivalCap: 1, minionCap: 3);
            _director.BeginSchedule(0f);
            _director.Advance(0f);
            var minions = _director.WaveDue(0f);
            Assert.IsFalse(_director.RivalRoom(1), "A live rival fills a rival cap of 1.");
            Assert.AreEqual(0, _director.MinionRoom(minions.Value, 3), "Three live minions fill a minion cap of 3.");

            _director.Arrived(0f);
            Assert.IsNull(_director.WaveDue(0f), "A dropped wave is not held.");
            Assert.IsNull(_director.WaveDue(WaveEvery - .1f), "Nothing is due again until the next beat.");
            Assert.AreEqual(OpenMinions, _director.WaveDue(WaveEvery), "The next wave comes on the beat.");
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
                    seconds = 40f, waveEvery = WaveEvery, sendsWaves = false,
                },
            }, Lull);
            _director.BeginSchedule(0f);
            _director.Advance(0f);
            Assert.IsNull(_director.WaveDue(0f));
            _director.Observe(0f, 0);
            Assert.IsTrue(_director.EarnedLull, "A phase that sends no waves earns the lull while nothing is alive.");
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
        public void WaveDue_DrawsTheMinionCount_AndSlotsSpreadTheWaveAlongTheRing()
        {
            const float radius = 55f;
            const float spread = 8f;
            const int minions = 2;
            _director.Configure(new[]
            {
                new SectorPhase
                {
                    seconds = 40f, waveEvery = WaveEvery, sendsWaves = true,
                    fewestMinions = minions, mostMinions = minions,
                },
            }, Lull, spawnRadius: radius, groupSpread: spread);
            _director.BeginSchedule(0f);
            _director.Advance(0f);

            Assert.AreEqual(minions, _director.WaveDue(0f), "A wave draws its minion count.");

            var center = new Vector2(3f, 4f);
            const float bearing = 0.4f;
            var count = 1 + minions;
            var slots = _director.Slots(center, bearing, count);
            Assert.AreEqual(count, slots.Length);
            foreach (var slot in slots)
                Assert.AreEqual(radius, Vector2.Distance(slot, center), 1e-3f);

            var bearingDeg = bearing * Mathf.Rad2Deg;
            var deltas = slots.Select(slot => Mathf.Abs(Mathf.DeltaAngle(bearingDeg, Degrees(center, slot)))).ToArray();
            Assert.That(deltas, Has.Some.LessThan(0.05f), "An odd wave puts the centre slot, the rival's, on the bearing.");
            Assert.That(deltas, Has.All.LessThan(20f), "The wave stays in a small arc of the bearing.");
            for (var i = 1; i < slots.Length; i++)
            {
                var step = Mathf.Abs(Mathf.DeltaAngle(Degrees(center, slots[i - 1]), Degrees(center, slots[i])));
                Assert.AreEqual(spread, step * Mathf.Deg2Rad * radius, 0.02f, "Neighbours sit groupSpread apart along the ring.");
            }
        }

        [Test]
        public void Lull_SendsNothing_AndTheNextPhaseDrawsItsOwnMinionCount()
        {
            OpenAndArrive(0f);
            _director.Advance(41f);
            Assert.IsTrue(_director.Phase.InLull);
            Assert.IsNull(_director.WaveDue(1000f), "A scheduled lull sends nothing.");

            _director.Advance(45f);
            Assert.AreEqual(LateMinions, _director.WaveDue(45f), "The next phase opens with its own minion count.");
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
        public void InLullWindow_CoversAFollowingLullItsGraceAndAnEarnedLull()
        {
            _director.Configure(_phases, Lull, openingLullSeconds: 5f);
            _director.BeginSchedule(0f);
            Assert.IsFalse(_director.ClosingLull);

            _director.Advance(1f);
            _director.Observe(1f, 0);
            Assert.IsTrue(_director.Phase.InOpeningLull);
            Assert.IsFalse(_director.InLullWindow(10f), "The opening lull stays dark.");

            _director.Advance(5f);
            _director.Observe(5f, 1);
            Assert.AreEqual(0, _director.Phase.Index);
            Assert.IsFalse(_director.Phase.InLull);
            Assert.IsFalse(_director.InLullWindow(10f), "The first phase's first seconds stay dark.");

            _director.Advance(46f);
            _director.Observe(46f, 1);
            Assert.IsTrue(_director.Phase.InLull);
            Assert.IsFalse(_director.Phase.InOpeningLull);
            Assert.IsTrue(_director.InLullWindow(10f), "A lull after a phase is a lull window.");

            _director.Advance(50f);
            _director.Observe(50f, 1);
            Assert.IsFalse(_director.Phase.InLull);
            Assert.AreEqual(1, _director.Phase.Index);
            Assert.IsTrue(_director.InLullWindow(10f), "The grace into the next phase.");

            _director.Advance(60f);
            _director.Observe(60f, 1);
            Assert.AreEqual(10f, _director.Phase.ElapsedSeconds, 1e-4f);
            Assert.IsFalse(_director.InLullWindow(10f), "The grace has run out.");

            _director.Configure(new[]
            {
                new SectorPhase
                {
                    seconds = 40f, waveEvery = WaveEvery, sendsWaves = false,
                },
            }, Lull);
            _director.BeginSchedule(0f);
            _director.Advance(1f);
            Assert.IsNull(_director.WaveDue(1f));
            _director.Observe(1f, 0);
            Assert.IsTrue(_director.EarnedLull);
            Assert.IsTrue(_director.InLullWindow(10f), "An earned lull is a lull window.");
        }

        [Test]
        public void ClosingLull_SendsNothing_AndItsGraceRunsIntoTheElite()
        {
            var elite = _go.AddComponent<EliteEncounter>();
            _director.Configure(_phases, Lull, elite: elite);
            _director.BeginSchedule(0f);
            Assert.IsTrue(_director.ClosingLull);

            _director.Advance(86f);
            Assert.IsTrue(_director.Phase.Closing);
            Assert.IsTrue(_director.Phase.InLull);
            Assert.AreEqual(1, _director.Phase.Index);
            Assert.IsNull(_director.WaveDue(86f), "The closing lull sends nothing.");
            _director.Observe(86f, 0);
            Assert.IsFalse(_director.EarnedLull);
            Assert.IsFalse(_director.WaveAhead, "No wave follows the closing lull.");
            Assert.AreEqual(0f, _director.NextWaveSeconds, 1e-4f);
            Assert.IsTrue(_director.InLullWindow(10f));

            _director.Advance(90f);
            Assert.IsTrue(_director.Phase.Done);
            _director.Observe(90f, 0);
            Assert.IsTrue(_director.InLullWindow(10f), "The grace after the closing lull.");

            _director.Advance(100f);
            _director.Observe(100f, 0);
            Assert.AreEqual(10f, _director.Phase.ElapsedSeconds, 1e-4f);
            Assert.IsFalse(_director.InLullWindow(10f));
        }

        [Test]
        public void FinishedTable_SendsNothing_WhenAnEliteFollows()
        {
            var elite = _go.AddComponent<EliteEncounter>();
            _director.Configure(_phases, Lull, elite: elite);
            _director.BeginSchedule(0f);

            _director.Advance(90f);
            Assert.IsTrue(_director.Phase.Done);
            Assert.IsNull(_director.WaveDue(90f), "A finished table with an elite sends nothing.");
            Assert.IsNull(_director.WaveDue(500f));
        }

        [Test]
        public void DoneTable_KeepsTheLastPhasesWaves()
        {
            _director.Advance(500f);
            Assert.IsTrue(_director.Phase.Done);
            _director.Arrived(500f);

            Assert.IsNull(_director.WaveDue(519f));
            Assert.AreEqual(LateMinions, _director.WaveDue(520f), "The done table keeps the last phase's waves and wave every.");
        }

        [Test]
        public void WaveDue_DrawsAMinionCountInsideThePhasesRange()
        {
            _director.Configure(new[]
            {
                new SectorPhase
                {
                    seconds = 40f, waveEvery = WaveEvery, sendsWaves = true,
                    fewestMinions = 1, mostMinions = 3,
                },
            }, Lull);
            _director.BeginSchedule(0f);
            _director.Advance(0f);

            Assert.That(_director.WaveDue(0f), Is.GreaterThanOrEqualTo(1).And.LessThanOrEqualTo(3));
        }

        [Test]
        public void CheckRow_RefusesAMinionRangeThatRunsBackwards()
        {
            var ex = Assert.Throws<System.InvalidOperationException>(() => _director.CheckRow(Row(new SectorPhase
            {
                sendsWaves = true, fewestMinions = 3, mostMinions = 1,
            })));
            StringAssert.Contains("WaveDirector", ex.Message);
            StringAssert.Contains("phase 0", ex.Message);
        }

        [Test]
        public void CheckRow_RefusesARivalCapOfZero()
        {
            var row = Row(new SectorPhase());
            row.rivalCap = 0;
            var ex = Assert.Throws<System.InvalidOperationException>(() => _director.CheckRow(row));
            StringAssert.Contains("rival cap", ex.Message);
            StringAssert.Contains("0", ex.Message);
        }

        [Test]
        public void CheckRow_RefusesANegativeMinionCap()
        {
            var row = Row(new SectorPhase());
            row.minionCap = -1;
            var ex = Assert.Throws<System.InvalidOperationException>(() => _director.CheckRow(row));
            StringAssert.Contains("minion cap", ex.Message);
            StringAssert.Contains("-1", ex.Message);
        }

        [Test]
        public void CheckRow_RefusesAnUnsetFaction()
        {
            var ex = Assert.Throws<System.InvalidOperationException>(() =>
                _director.CheckRow(Row(new SectorPhase())));
            StringAssert.Contains(_director.name, ex.Message);
        }

        [Test]
        public void CheckRow_AllowsAPhaseThatSendsNoWaves()
        {
            var faction = CompleteFaction("Shoal");
            var sectorFaction = _go.AddComponent<SectorFaction>();
            _director.Configure(_phases, Lull, faction: sectorFaction);
            var row = Row(new SectorPhase { sendsWaves = false });
            row.faction = faction;

            Assert.DoesNotThrow(() => _director.CheckRow(row));

            Object.DestroyImmediate(faction);
        }

        [Test]
        public void CheckRow_RefusesAMinionWithNoPilot()
        {
            var faction = ScriptableObject.CreateInstance<Faction>();
            faction.name = "Veil";
            var ship = new GameObject("Valis").AddComponent<Ship>();
            var kit = _go.AddComponent<PoolWeapon>();
            var pilot = new GameObject("Rival").AddComponent<StubCommander>();
            faction.signature = new FactionShip { ship = ship, kit = kit, pilot = pilot };
            faction.minion = new FactionMinion { ship = ship, kit = kit, scale = new ShipScale(0.75f, 1f, 1f, 1f, 0.7f) };
            var sectorFaction = _go.AddComponent<SectorFaction>();
            _director.Configure(_phases, Lull, faction: sectorFaction);
            var row = Row(new SectorPhase { sendsWaves = true, fewestMinions = 0, mostMinions = 1 });
            row.faction = faction;

            var ex = Assert.Throws<System.InvalidOperationException>(() => _director.CheckRow(row));

            StringAssert.Contains("Veil", ex.Message);
            StringAssert.Contains("minion pilot", ex.Message);
            Object.DestroyImmediate(faction);
            Object.DestroyImmediate(ship.gameObject);
            Object.DestroyImmediate(pilot.gameObject);
        }

        [Test]
        public void Threat_ListsASharedChassisOnce_AndEachRanksKitOnce()
        {
            var valis = new GameObject("Valis").AddComponent<Ship>();
            var lasers = _go.AddComponent<PoolWeapon>();
            var rippers = _go.AddComponent<PoolWeapon>();
            var rivalPilot = new GameObject("RivalPilot").AddComponent<StubCommander>();
            var minionPilot = new GameObject("MinionPilot").AddComponent<StubCommander>();
            var faction = ScriptableObject.CreateInstance<Faction>();
            faction.signature = new FactionShip { ship = valis, kit = rippers, pilot = rivalPilot };
            faction.minion = new FactionMinion
            {
                ship = valis, kit = lasers, pilot = minionPilot, scale = new ShipScale(0.75f, 1f, 1f, 1f, 0.7f),
            };
            faction.elite = new FactionElite { ship = valis };
            var sectorFaction = _go.AddComponent<SectorFaction>();
            var row = new SectorEntry { faction = faction, phases = new[] { new SectorPhase { sendsWaves = true } } };
            _director.Configure(_phases, Lull, faction: sectorFaction);

            var threat = _director.Threat(row);

            CollectionAssert.AreEqual(new[] { valis }, threat.Ships, "a chassis every rank flies is listed once");
            CollectionAssert.AreEqual(new WeaponComponent[] { rippers, lasers }, threat.Weapons,
                "the rival's kit, then the minion's");

            Object.DestroyImmediate(faction);
            Object.DestroyImmediate(valis.gameObject);
            Object.DestroyImmediate(rivalPilot.gameObject);
            Object.DestroyImmediate(minionPilot.gameObject);
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

        private void OpenAndArrive(float now)
        {
            _director.Advance(now);
            Assert.IsNotNull(_director.WaveDue(now));
            _director.Arrived(now);
        }

        private Faction CompleteFaction(string factionName)
        {
            var faction = ScriptableObject.CreateInstance<Faction>();
            faction.name = factionName;
            var ship = new GameObject("Valis").AddComponent<Ship>();
            ship.transform.SetParent(_go.transform);
            var kit = _go.AddComponent<PoolWeapon>();
            var pilot = new GameObject("Pilot").AddComponent<StubCommander>();
            pilot.transform.SetParent(_go.transform);
            faction.signature = new FactionShip { ship = ship, kit = kit, pilot = pilot };
            faction.minion = new FactionMinion
            {
                ship = ship, kit = kit, pilot = pilot, scale = new ShipScale(0.75f, 1f, 1f, 1f, 0.7f),
            };
            faction.elite = new FactionElite { ship = ship };
            return faction;
        }

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

        private sealed class StubCommander : Commander
        {
            public override void Initialize(in ShipControl control) { }
        }
    }
}
