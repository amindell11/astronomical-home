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
    /// <summary>The shipped run's three rows, read as assets: each names its own faction in run order, its threat names that faction's signature ship and the rival's and minion's kits, each phase that sends waves draws its minions from an ordered range, and its minions fly their own chassis with paint for every silhouette slot. The rival resolves the signature ship and the minion its own chassis, each with its hull, kit, pilot and scale, and each throws naming the faction when its kit or pilot is missing.</summary>
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
        public void RivalAndMinion_ResolveTheirOwnShips_WithEachRanksHullKitPilotAndScale()
        {
            var ship = Hull("Valis");
            var minionShip = Hull("ValisMinion");
            var rivalHull = new Material[1];
            var minionHull = new Material[1];
            var rivalKit = Kit();
            var minionKit = Kit();
            var rivalPilot = Pilot("RivalPilot");
            var minionPilot = Pilot("MinionPilot");
            var scale = new ShipScale(0.75f, 1f, 1f, 1f, 0.7f);
            var faction = FactionOf(ship, rivalKit, rivalPilot, minionShip, rivalHull, minionHull,
                minionKit, minionPilot, scale);

            var rival = faction.Rival;
            Assert.AreSame(ship, rival.ship);
            Assert.AreSame(rivalHull, rival.hull);
            Assert.AreSame(rivalKit, rival.kit);
            Assert.AreSame(rivalPilot, rival.pilot);
            Assert.AreSame(ShipScale.One, rival.scale);

            var minion = faction.Minion;
            Assert.AreSame(minionShip, minion.ship);
            Assert.AreSame(minionHull, minion.hull);
            Assert.AreSame(minionKit, minion.kit);
            Assert.AreSame(minionPilot, minion.pilot);
            Assert.AreSame(scale, minion.scale);
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
        public void EachTrialRoster_FliesItsSignatureShip_OnTheRivalsAndMinionsKits()
        {
            foreach (var (row, i) in Rows().Select((row, i) => (row, i)))
            {
                var faction = FactionOf(row);
                var threat = row.prefab.Module<PhaseSpineModule>().Threat(row);

                CollectionAssert.AreEquivalent(new[] { faction.Ship }, threat.Ships, $"row {i}");
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
                var faction = FactionOf(row);
                Assert.AreNotSame(faction.Ship, faction.Minion.ship, $"row {i}");
                Assert.AreEqual(faction.Ship.name + "Minion", faction.Minion.ship.name, $"row {i}");
            }
        }

        [Test]
        public void ShoalBastionAndVeil_MinionHullCoversEverySilhouetteSlot()
        {
            foreach (var faction in Rows().Select(row => FactionOf(row)))
            {
                var slots = 0;
                foreach (var surface in faction.Minion.ship.VisualRig.Silhouette)
                    slots += surface.sharedMaterials.Length;
                Assert.IsNotNull(faction.Minion.hull, faction.name);
                Assert.AreEqual(slots, faction.Minion.hull.Length, faction.name);
            }
        }

        private static SectorEntry[] Rows()
        {
            var run = AssetDatabase.LoadAssetAtPath<SectorRun>(RunPath);
            Assert.IsNotNull(run, $"Sector run missing at {RunPath}");
            return run.Sectors.ToArray();
        }

        private Faction FactionOf(Ship ship, WeaponComponent rivalKit, Commander rivalPilot,
            Ship minionShip, Material[] rivalHull, Material[] minionHull,
            WeaponComponent minionKit, Commander minionPilot, ShipScale scale)
        {
            var faction = Track(ScriptableObject.CreateInstance<Faction>());
            faction.signature = new FactionShip { ship = ship, kit = rivalKit, pilot = rivalPilot, hull = rivalHull };
            faction.minion = new FactionMinion
            {
                ship = minionShip, kit = minionKit, pilot = minionPilot, scale = scale, hull = minionHull,
            };
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
