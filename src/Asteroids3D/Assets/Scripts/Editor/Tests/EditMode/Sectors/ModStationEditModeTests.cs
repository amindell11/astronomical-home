using System.Linq;
using NUnit.Framework;
using Objectives;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Stations;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Sectors
{
    /// <summary>Each trial sector carries one mod station module on the shared feel of two stations, placed 120 to 180 u from the start, reading the sector's own hunt and its repair stations, set up after those stations, with its zone on the body, its own find icon, and a find-layer minimap marker among the parts that go dark.</summary>
    [TestFixture]
    [Category("Sectors")]
    [Category("RequiresLfsPng")]
    public class ModStationEditModeTests
    {
        private const string FeelPath = "Assets/Settings/Game/ModStationFeel.asset";

        [TestCase("TrialSector")]
        [TestCase("TrialSector2")]
        [TestCase("TrialSector3")]
        public void TrialSector_CarriesOneModStation_AfterItsRepairStations(string sector)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Sector>($"Assets/Prefabs/Sectors/{sector}.prefab");
            var mods = prefab.Modules.OfType<ModStation>().Single();
            var repairs = prefab.Modules.OfType<RepairStation>().Single();
            var modules = prefab.Modules.ToList();
            Assert.Greater(modules.IndexOf(mods), modules.IndexOf(repairs),
                "The mod stations set up after the repair stations, so they never read a previous sector's.");

            using var serialized = new SerializedObject(mods);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<ModStationFeelProfile>(FeelPath),
                serialized.FindProperty("feel").objectReferenceValue);
            Assert.AreEqual(120f, serialized.FindProperty("distanceMin").floatValue);
            Assert.AreEqual(180f, serialized.FindProperty("distanceMax").floatValue);
            Assert.AreSame(prefab.GetComponentInChildren<SectorHunt>(true), serialized.FindProperty("hunt").objectReferenceValue,
                "The mod stations read the sector's own hunt for its alarm.");
            Assert.AreSame(repairs, serialized.FindProperty("repairs").objectReferenceValue,
                "The mod stations keep their spacing from the sector's own repair stations.");

            var body = (GameObject)serialized.FindProperty("body").objectReferenceValue;
            var lit = (GameObject)serialized.FindProperty("lit").objectReferenceValue;
            var zone = (ExtractionZone)serialized.FindProperty("zone").objectReferenceValue;
            Assert.AreSame(body, zone.gameObject, "The zone hides and shows with the body.");
            Assert.IsTrue(zone.GetComponent<Collider>().isTrigger);
            Assert.IsTrue(lit.transform.IsChildOf(body.transform));
            Assert.That(lit.GetComponentsInChildren<Renderer>(true).Select(r => r.gameObject.layer),
                Has.Member(LayerMask.NameToLayer("Minimap_Find")), "The find marker that shows on the minimap goes dark with the glow.");
            Assert.IsNotEmpty(lit.GetComponentsInChildren<Light>(true));

            var icon = serialized.FindProperty("icon").objectReferenceValue as Sprite;
            Assert.IsTrue(icon, "The mod station carries its find icon.");
            using var repairSerialized = new SerializedObject(repairs);
            Assert.AreNotSame(repairSerialized.FindProperty("icon").objectReferenceValue, icon,
                "The mod station's icon is its own, not the repair station's.");
        }

        [Test]
        public void TheFeel_PlacesTwoStationsPerSector()
        {
            Assert.AreEqual(2, AssetDatabase.LoadAssetAtPath<ModStationFeelProfile>(FeelPath).stationsPerSector);
        }
    }
}
