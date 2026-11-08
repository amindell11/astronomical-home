using System.Linq;
using NUnit.Framework;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using Substrate.Sessions;
using UnityEditor;

namespace Tests.EditMode.Factions
{
    /// <summary>The shipped run's three rows, read as assets: each names its own faction in run order, and its roster flies only that faction's signature and common ships on their kits, none of which is the faction's answer weapon, and draws the signature ship three times as often as the common one.</summary>
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
                CollectionAssert.DoesNotContain(threat.Weapons, faction.answer,
                    $"row {i}: a kit never carries the weapon that answers its faction.");
            }
        }

        [Test]
        public void EachTrialRoster_DrawsItsSignatureShip_ThreeTimesAsOftenAsItsCommonShip()
        {
            foreach (var (row, i) in Rows().Select((row, i) => (row, i)))
            {
                var faction = FactionOf(row);
                var templates = row.roster.Select(entry => entry.template).ToArray();

                var common = templates.Count(t => t == faction.common.ship);
                Assert.Greater(common, 0, $"row {i}: the common ship still flies.");
                Assert.AreEqual(3 * common, templates.Count(t => t == faction.signature.ship),
                    $"row {i}: the director draws uniformly over entries, so entry counts set the mix.");
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
