using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using Substrate.Sessions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Factions
{
    /// <summary>The shipped run's three rows, read as assets: each names its own faction in run order, its waves fly only that faction's signature and common ships on their kits, each phase's mean group is at least the previous phase's with the last larger than the first, and the signature ship flies more often than the common one.</summary>
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
        public void PilotFor_ReturnsEachHullsPilot()
        {
            var signature = Hull("Signature");
            var common = Hull("Common");
            var signaturePilot = Pilot("SignaturePilot");
            var commonPilot = Pilot("CommonPilot");
            var faction = FactionOf(signature, signaturePilot, common, commonPilot);

            Assert.AreSame(signaturePilot, faction.PilotFor(signature));
            Assert.AreSame(commonPilot, faction.PilotFor(common));
        }

        [Test]
        public void PilotFor_ThrowsForAHullTheFactionDoesNotFly()
        {
            var faction = FactionOf(Hull("Signature"), Pilot("SignaturePilot"), Hull("Common"), Pilot("CommonPilot"));
            faction.name = "Shoal";
            var other = Hull("Stranger");

            var ex = Assert.Throws<InvalidOperationException>(() => faction.PilotFor(other));

            StringAssert.Contains("Shoal", ex.Message);
            StringAssert.Contains("Stranger", ex.Message);
        }

        [Test]
        public void PilotFor_ThrowsForAHullWithNoPilot()
        {
            var signature = Hull("Signature");
            var faction = Track(ScriptableObject.CreateInstance<Faction>());
            faction.name = "Veil";
            faction.signature = new FactionShip { ship = signature };

            var ex = Assert.Throws<InvalidOperationException>(() => faction.PilotFor(signature));

            StringAssert.Contains("Veil", ex.Message);
            StringAssert.Contains("Signature", ex.Message);
        }

        [Test]
        public void EachTrialRow_NamesADifferentFaction_InRunOrder()
        {
            var ids = Rows().Select(row => FactionOf(row).id).ToArray();

            CollectionAssert.AreEqual(new[] { "shoal", "veil", "bastion" }, ids);
        }

        [Test]
        public void EachTrialRoster_FliesOnlyItsFactionsSignatureAndCommonShips_OnTheirKits()
        {
            foreach (var (row, i) in Rows().Select((row, i) => (row, i)))
            {
                var faction = FactionOf(row);
                var threat = row.prefab.Module<PhaseSpineModule>().Threat(row);

                CollectionAssert.AreEquivalent(new[] { faction.signature.ship, faction.common.ship }, threat.Ships, $"row {i}");
                CollectionAssert.AreEquivalent(new[] { faction.signature.kit, faction.common.kit }.Distinct(),
                    threat.Weapons, $"row {i}");
            }
        }

        [Test]
        public void EachTrialRow_GrowsItsGroups_AndFliesItsSignatureShipMoreOftenThanItsCommonShip()
        {
            foreach (var (row, i) in Rows().Select((row, i) => (row, i)))
            {
                var faction = FactionOf(row);
                var means = row.phases.Select(phase => phase.groups.Average(group => (double)group.ships.Length)).ToArray();
                for (var p = 1; p < means.Length; p++)
                    Assert.That(means[p], Is.GreaterThanOrEqualTo(means[p - 1]),
                        $"row {i} phase {p}: mean group size");
                Assert.That(means[means.Length - 1], Is.GreaterThan(means[0]), $"row {i}: the last phase's groups are larger");

                var templates = row.phases.SelectMany(phase => phase.groups).SelectMany(group => group.ships)
                    .Select(ship => ship.template).ToArray();
                var common = templates.Count(t => t == faction.common.ship);
                Assert.Greater(common, 0, $"row {i}: the common ship still flies.");
                Assert.Greater(templates.Count(t => t == faction.signature.ship), common,
                    $"row {i}: the signature ship flies more often than the common one.");
            }
        }

        private static SectorEntry[] Rows()
        {
            var run = AssetDatabase.LoadAssetAtPath<SectorRun>(RunPath);
            Assert.IsNotNull(run, $"Sector run missing at {RunPath}");
            return run.Sectors.ToArray();
        }

        private Faction FactionOf(Ship signature, Commander signaturePilot, Ship common, Commander commonPilot)
        {
            var faction = Track(ScriptableObject.CreateInstance<Faction>());
            faction.signature = new FactionShip { ship = signature, pilot = signaturePilot };
            faction.common = new FactionShip { ship = common, pilot = commonPilot };
            return faction;
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

        private static Faction FactionOf(SectorEntry row)
        {
            Assert.IsNotNull(row.prefab.Module<SectorFaction>(), $"{row.prefab.name} carries no SectorFaction module.");
            Assert.IsNotNull(row.faction, "the row names no faction.");
            return row.faction;
        }
    }
}
