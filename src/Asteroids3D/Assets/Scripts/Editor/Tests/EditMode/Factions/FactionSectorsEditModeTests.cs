using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using Substrate.Sessions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Factions
{
    /// <summary>The shipped run's three rows, read as assets: each names its own faction in run order, its waves fly each rank's chassis on the rival's and minion's kits, and each phase that sends waves draws its minions from an ordered range. Rival and minion resolve their own chassis with that rank's kit, pilot, scale and hull, and each throws naming the faction when its kit or pilot is missing. Each shipped livery is empty or paints every hull slot of its chassis.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class FactionSectorsEditModeTests
    {
        private const string RunPath = "Assets/Settings/Game/SurvivalTrialRun.asset";

        private readonly List<Object> trash = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in trash)
                if (item) Object.DestroyImmediate(item);
            trash.Clear();
        }

        [Test]
        public void RivalAndMinion_ResolveTheirChassis_WithEachRanksKitPilotScaleAndHull()
        {
            var ship = Hull("Valis");
            var rivalKit = Kit();
            var minionKit = Kit();
            var rivalPilot = Pilot("RivalPilot");
            var minionPilot = Pilot("MinionPilot");
            var scale = new ShipScale(0.75f, 1f, 1f, 1f, 0.7f);
            var rivalHull = new Material[1];
            var minionHull = new Material[1];
            var faction = FactionOf(ship, rivalKit, rivalPilot, minionKit, minionPilot, scale, rivalHull, minionHull);

            var rival = faction.Rival;
            Assert.AreSame(ship, rival.ship);
            Assert.AreSame(rivalKit, rival.kit);
            Assert.AreSame(rivalPilot, rival.pilot);
            Assert.AreSame(ShipScale.One, rival.scale);
            Assert.AreSame(rivalHull, rival.hull);

            var minion = faction.Minion;
            Assert.AreSame(ship, minion.ship);
            Assert.AreSame(minionKit, minion.kit);
            Assert.AreSame(minionPilot, minion.pilot);
            Assert.AreSame(scale, minion.scale);
            Assert.AreSame(minionHull, minion.hull);
        }

        [Test]
        public void Rival_ThrowsNamingTheFaction_WhenItsKitOrPilotIsMissing()
        {
            var ship = Hull("Valis");
            var pilot = Pilot("RivalPilot");
            var faction = Track(ScriptableObject.CreateInstance<Faction>());
            faction.name = "Shoal";
            faction.signature = new FactionShip { ship = ship, pilot = pilot };

            var missingKit = Assert.Throws<InvalidOperationException>(() => _ = faction.Rival);
            StringAssert.Contains("Shoal", missingKit.Message);
            StringAssert.Contains("rival kit", missingKit.Message);

            faction.signature = new FactionShip { ship = ship, kit = Kit() };
            var missingPilot = Assert.Throws<InvalidOperationException>(() => _ = faction.Rival);
            StringAssert.Contains("Shoal", missingPilot.Message);
            StringAssert.Contains("rival pilot", missingPilot.Message);
        }

        [Test]
        public void Minion_ThrowsNamingTheFaction_WhenItsKitOrPilotIsMissing()
        {
            var ship = Hull("Valis");
            var scale = new ShipScale(0.75f, 1f, 1f, 1f, 0.7f);
            var faction = Track(ScriptableObject.CreateInstance<Faction>());
            faction.name = "Veil";
            faction.signature = new FactionShip { ship = ship };
            faction.minion = new FactionMinion { ship = ship, pilot = Pilot("MinionPilot"), scale = scale };

            var missingKit = Assert.Throws<InvalidOperationException>(() => _ = faction.Minion);
            StringAssert.Contains("Veil", missingKit.Message);
            StringAssert.Contains("minion kit", missingKit.Message);

            faction.minion = new FactionMinion { ship = ship, kit = Kit(), scale = scale };
            var missingPilot = Assert.Throws<InvalidOperationException>(() => _ = faction.Minion);
            StringAssert.Contains("Veil", missingPilot.Message);
            StringAssert.Contains("minion pilot", missingPilot.Message);
        }

        [Test]
        public void EachTrialRow_NamesADifferentFaction_InRunOrder()
        {
            var ids = Rows().Select(row => FactionOf(row).id).ToArray();

            CollectionAssert.AreEqual(new[] { "shoal", "veil", "bastion" }, ids);
        }

        [Test]
        public void EachTrialFaction_NamesItsElite()
        {
            foreach (var row in Rows())
                Assert.IsNotEmpty(FactionOf(row).EliteName, FactionOf(row).name);
        }

        [Test]
        public void EachTrialRoster_FliesEachRanksChassis_OnTheRivalsAndMinionsKits()
        {
            foreach (var (row, i) in Rows().Select((row, i) => (row, i)))
            {
                var faction = FactionOf(row);
                var threat = row.prefab.Module<PhaseSpineModule>().Threat(row);

                CollectionAssert.AreEquivalent(
                    new[] { faction.Rival.ship, faction.Minion.ship, faction.EliteShip }.Distinct().ToArray(),
                    threat.Ships, $"row {i}");
                CollectionAssert.AreEquivalent(new[] { faction.Rival.kit, faction.Minion.kit }.Distinct(),
                    threat.Weapons, $"row {i}");
            }
        }

        [Test]
        public void EachTrialRow_SendsWaves_OnAnOrderedMinionRange()
        {
            foreach (var (row, i) in Rows().Select((row, i) => (row, i)))
            {
                var sending = row.phases.Where(phase => phase.sendsWaves).ToArray();
                Assert.IsNotEmpty(sending, $"row {i} sends waves");
                foreach (var phase in sending)
                    Assert.That(phase.fewestMinions, Is.InRange(0, phase.mostMinions), $"row {i}");
            }
        }

        [Test]
        public void EachFaction_UnlocksAShipThePlayerLoadoutOffers_AndShoalUnlocksValis()
        {
            const string offerPath = "Assets/Settings/Ships/PlayerLoadout.asset";
            var offer = AssetDatabase.LoadAssetAtPath<ItemSubset>(offerPath);
            Assert.IsNotNull(offer, $"player loadout missing at {offerPath}");
            var valis = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
            Assert.IsNotNull(valis, "Valis prefab loads");

            foreach (var name in new[] { "Shoal", "Bastion", "Veil" })
            {
                var path = $"Assets/Settings/Factions/{name}.asset";
                var faction = AssetDatabase.LoadAssetAtPath<Faction>(path);
                Assert.IsNotNull(faction, $"faction missing at {path}");
                var unlocked = faction.Unlocks;
                CollectionAssert.Contains(offer.ships, unlocked, $"{name} unlocks an offered ship");
                if (name == "Shoal")
                    Assert.AreSame(valis, unlocked);
            }
        }

        [Test]
        public void EachFactionLivery_IsEmpty_OrPaintsEverySlotOfItsChassis()
        {
            foreach (var name in new[] { "Shoal", "Bastion", "Veil" })
            {
                var faction = AssetDatabase.LoadAssetAtPath<Faction>($"Assets/Settings/Factions/{name}.asset");
                AssertPaints(faction.signature.ship, faction.signature.hull, $"{name} rival");
                AssertPaints(faction.minion.ship, faction.minion.hull, $"{name} minion");
                AssertPaints(faction.elite.ship, faction.elite.hull, $"{name} elite");
            }
        }

        private static void AssertPaints(Ship chassis, Material[] hull, string rank)
        {
            if (hull == null || hull.Length == 0) return;
            var slots = chassis.VisualRig.Silhouette.Sum(surface => surface.sharedMaterials.Length);
            Assert.AreEqual(slots, hull.Length, $"{rank}: '{chassis.name}' has {slots} hull material slots");
        }

        private static SectorEntry[] Rows()
        {
            var run = AssetDatabase.LoadAssetAtPath<SectorRun>(RunPath);
            Assert.IsNotNull(run, $"Sector run missing at {RunPath}");
            return run.Sectors.ToArray();
        }

        private Faction FactionOf(Ship ship, WeaponComponent rivalKit, Commander rivalPilot,
            WeaponComponent minionKit, Commander minionPilot, ShipScale scale, Material[] rivalHull,
            Material[] minionHull)
        {
            var faction = Track(ScriptableObject.CreateInstance<Faction>());
            faction.signature = new FactionShip { ship = ship, kit = rivalKit, pilot = rivalPilot, hull = rivalHull };
            faction.minion = new FactionMinion
            {
                ship = ship, kit = minionKit, pilot = minionPilot, scale = scale, hull = minionHull,
            };
            faction.elite = new FactionElite { ship = ship };
            return faction;
        }

        private WeaponComponent Kit()
        {
            var host = Track(new GameObject("Kit"));
            host.SetActive(false);
            return host.AddComponent<StubWeapon>();
        }

        private Ship Hull(string hullName)
        {
            var host = Track(new GameObject(hullName));
            host.SetActive(false);
            return host.AddComponent<Ship>();
        }

        private Commander Pilot(string pilotName)
        {
            var host = Track(new GameObject(pilotName));
            host.SetActive(false);
            return host.AddComponent<StubCommander>();
        }

        private T Track<T>(T item) where T : Object
        {
            trash.Add(item);
            return item;
        }

        private sealed class StubCommander : Commander
        {
            public override void Initialize(in ShipControl control) { }
        }

        private sealed class StubWeapon : WeaponComponent
        {
            public override Combat.Projectiles.ProjectileBase Fire(UnityEngine.Vector3 targetPoint,
                Substrate.Services.Projectiles.IProjectileService projectiles) => null;
        }

        private static Faction FactionOf(SectorEntry row)
        {
            Assert.IsNotNull(row.prefab.Module<SectorFaction>(), $"{row.prefab.name} carries no SectorFaction module.");
            Assert.IsNotNull(row.faction, "the row names no faction.");
            return row.faction;
        }
    }
}
