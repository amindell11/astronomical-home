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
    /// <summary>Each trial sector carries one repair station module on the shared feel, placed 120 to 180 u from the start, reading the sector's own hunt, with its zone on the body, its find icon, and a find-layer minimap marker among the parts that go dark.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class RepairStationEditModeTests
    {
        private const string FeelPath = "Assets/Settings/Game/RepairStationFeel.asset";

        [TestCase("TrialSector")]
        [TestCase("TrialSector2")]
        [TestCase("TrialSector3")]
        public void TrialSector_CarriesOneRepairStation_OnTheSharedFeel(string sector)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Sector>($"Assets/Prefabs/Sectors/{sector}.prefab");
            var station = prefab.Modules.OfType<RepairStation>().Single();
            using var serialized = new SerializedObject(station);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<RepairStationFeelProfile>(FeelPath),
                serialized.FindProperty("feel").objectReferenceValue);
            Assert.AreEqual(120f, serialized.FindProperty("distanceMin").floatValue);
            Assert.AreEqual(180f, serialized.FindProperty("distanceMax").floatValue);
            Assert.AreSame(prefab.GetComponentInChildren<SectorHunt>(true), serialized.FindProperty("hunt").objectReferenceValue,
                "The station reads the sector's own hunt for its alarm.");

            var body = (GameObject)serialized.FindProperty("body").objectReferenceValue;
            var lit = (GameObject)serialized.FindProperty("lit").objectReferenceValue;
            var zone = (ExtractionZone)serialized.FindProperty("zone").objectReferenceValue;
            Assert.AreSame(body, zone.gameObject, "The zone hides and shows with the body.");
            Assert.IsTrue(zone.GetComponent<Collider>().isTrigger);
            Assert.IsTrue(lit.transform.IsChildOf(body.transform));
            Assert.That(lit.GetComponentsInChildren<Renderer>(true).Select(r => r.gameObject.layer),
                Has.Member(LayerMask.NameToLayer("Minimap_Find")), "The find marker that shows on the minimap goes dark with the glow.");
            Assert.IsTrue(serialized.FindProperty("icon").objectReferenceValue as Sprite, "The station carries its find icon.");
            Assert.IsNotEmpty(lit.GetComponentsInChildren<Light>(true));
        }
    }
}
