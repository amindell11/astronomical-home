using System.Linq;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Stations;
using Substrate.Sessions;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Sectors
{
    /// <summary>The trial sector, which every survival trial row flies, carries one armory module, set up after its repair and mod stations, reading the sector's own hunt and spaced from both, with its own find icon and a find-layer minimap marker that goes dark with its glow. The trial run's rows place no armory, then one, then one.</summary>
    [TestFixture]
    [Category("Sectors")]
    [Category("RequiresLfsPng")]
    public class ArmoryEditModeTests
    {
        private const string RunPath = "Assets/Settings/Game/SurvivalTrialRun.asset";

        [Test]
        public void TrialSector_CarriesOneArmory_AfterItsRepairAndModStations()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Sector>("Assets/Prefabs/Sectors/TrialSector.prefab");
            var armory = prefab.Modules.OfType<Armory>().Single();
            var repairs = prefab.Modules.OfType<RepairStation>().Single();
            var mods = prefab.Modules.OfType<ModStation>().Single();
            var modules = prefab.Modules.ToList();
            Assert.Greater(modules.IndexOf(armory), modules.IndexOf(mods),
                "The armory sets up after the stations it is spaced from, so it never reads a previous sector's.");

            using var serialized = new SerializedObject(armory);
            Assert.AreSame(prefab.GetComponentInChildren<SectorHunt>(true), serialized.FindProperty("hunt").objectReferenceValue,
                "The armory reads the sector's own hunt for its alarm.");
            var spacedFrom = serialized.FindProperty("spacedFrom");
            CollectionAssert.AreEqual(new Object[] { repairs, mods },
                Enumerable.Range(0, spacedFrom.arraySize)
                    .Select(i => spacedFrom.GetArrayElementAtIndex(i).objectReferenceValue).ToArray());

            var lit = ((FindStationBody)serialized.FindProperty("body").objectReferenceValue).Lit;
            Assert.That(lit.GetComponentsInChildren<Renderer>(true).Select(r => r.gameObject.layer),
                Has.Member(LayerMask.NameToLayer("Minimap_Find")), "The find marker goes dark with the glow.");

            var icon = serialized.FindProperty("icon").objectReferenceValue as Sprite;
            Assert.IsTrue(icon, "The armory carries its find icon.");
            foreach (var other in new FindStation[] { repairs, mods })
            {
                using var otherSerialized = new SerializedObject(other);
                Assert.AreNotSame(otherSerialized.FindProperty("icon").objectReferenceValue, icon,
                    "The armory's icon is its own.");
            }
        }

        [Test]
        public void TheTrialRun_PlacesNoneInSectorOne_AndOneInSectorsTwoAndThree()
        {
            var rows = AssetDatabase.LoadAssetAtPath<SectorRun>(RunPath).Sectors;
            CollectionAssert.AreEqual(new[] { 0, 1, 1 }, rows.Select(row => row.armories).ToArray());
        }
    }
}
