using System.Linq;
using NUnit.Framework;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using Substrate.Sessions;
using UnityEditor;

namespace Tests.EditMode.Factions
{
    /// <summary>The shipped run's three rows, read as assets: each names its own faction in run order, its waves fly only that faction's signature and common ships on their kits, each phase's mean group is at least the previous phase's with the last larger than the first, and the signature ship flies more often than the common one.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class FactionSectorsEditModeTests
    {
        private const string RunPath = "Assets/Settings/Game/SurvivalTrialRun.asset";

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

        private static Faction FactionOf(SectorEntry row)
        {
            Assert.IsNotNull(row.prefab.Module<SectorFaction>(), $"{row.prefab.name} carries no SectorFaction module.");
            Assert.IsNotNull(row.faction, "the row names no faction.");
            return row.faction;
        }
    }
}
