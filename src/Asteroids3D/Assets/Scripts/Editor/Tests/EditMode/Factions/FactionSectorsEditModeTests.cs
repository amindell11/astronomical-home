using System.Linq;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using UnityEditor;

namespace Tests.EditMode.Factions
{
    /// <summary>The three trial sector prefabs, read as assets: each names its own faction in run order, and its roster flies only that faction's signature and common ships on their kits, and draws the signature ship three times as often as the common one.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class FactionSectorsEditModeTests
    {
        private static readonly string[] TrialSectorPaths =
        {
            "Assets/Prefabs/Sectors/TrialSector.prefab",
            "Assets/Prefabs/Sectors/TrialSector2.prefab",
            "Assets/Prefabs/Sectors/TrialSector3.prefab",
        };

        [Test]
        public void EachTrialSector_NamesADifferentFaction_InRunOrder()
        {
            var ids = TrialSectorPaths.Select(path => FactionOf(Load(path)).id).ToArray();

            CollectionAssert.AreEqual(new[] { "shoal", "veil", "bastion" }, ids);
        }

        [Test]
        public void EachTrialRoster_FliesOnlyItsFactionsSignatureAndCommonShips_OnTheirKits()
        {
            foreach (var path in TrialSectorPaths)
            {
                var sector = Load(path);
                var faction = FactionOf(sector);
                var threat = sector.Module<PhaseSpineModule>().Threat();

                CollectionAssert.AreEquivalent(new[] { faction.signature.ship, faction.common.ship }, threat.Ships, path);
                CollectionAssert.AreEquivalent(new[] { faction.signature.kit, faction.common.kit }.Distinct(),
                    threat.Weapons, path);
            }
        }

        [Test]
        public void EachTrialRoster_DrawsItsSignatureShip_ThreeTimesAsOftenAsItsCommonShip()
        {
            foreach (var path in TrialSectorPaths)
            {
                var sector = Load(path);
                var faction = FactionOf(sector);
                var roster = new SerializedObject(sector.GetComponentInChildren<WaveDirector>(true))
                    .FindProperty("roster");
                var templates = Enumerable.Range(0, roster.arraySize)
                    .Select(i => roster.GetArrayElementAtIndex(i).FindPropertyRelative("template").objectReferenceValue)
                    .ToArray();

                var common = templates.Count(t => t == faction.common.ship);
                Assert.Greater(common, 0, $"{path}: the common ship still flies.");
                Assert.AreEqual(3 * common, templates.Count(t => t == faction.signature.ship),
                    $"{path}: the director draws uniformly over entries, so entry counts set the mix.");
            }
        }

        private static Sector Load(string path)
        {
            var sector = AssetDatabase.LoadAssetAtPath<Sector>(path);
            Assert.IsNotNull(sector, $"Sector prefab missing at {path}");
            return sector;
        }

        private static Faction FactionOf(Sector sector)
        {
            var named = sector.Module<SectorFaction>();
            Assert.IsNotNull(named, $"{sector.name} carries no SectorFaction module.");
            Assert.IsNotNull(named.Faction, $"{sector.name} names no faction.");
            return named.Faction;
        }
    }
}
