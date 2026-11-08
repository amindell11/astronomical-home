using System.Linq;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using Objectives;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Stations;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Sectors
{
    /// <summary>Each trial sector carries one weapon cache module on the shared feel and a +25% damage boost mod, placed 120 to 180 u from the start, reading the sector's own hunt and its repair stations, set up after those stations, with its zone on the body, its own find icon, and a find-layer minimap marker among the parts that go dark.</summary>
    [TestFixture]
    [Category("Sectors")]
    [Category("RequiresLfsPng")]
    public class WeaponCacheEditModeTests
    {
        private const string FeelPath = "Assets/Settings/Game/WeaponCacheFeel.asset";
        private const string BoostPath = "Assets/Settings/Game/WeaponCacheBoost.asset";

        [TestCase("TrialSector")]
        [TestCase("TrialSector2")]
        [TestCase("TrialSector3")]
        public void TrialSector_CarriesOneWeaponCache_AfterItsRepairStations(string sector)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Sector>($"Assets/Prefabs/Sectors/{sector}.prefab");
            var cache = prefab.Modules.OfType<WeaponCache>().Single();
            var station = prefab.Modules.OfType<RepairStation>().Single();
            var modules = prefab.Modules.ToList();
            Assert.Greater(modules.IndexOf(cache), modules.IndexOf(station),
                "The cache sets up after the repair stations, so it never reads a previous sector's.");

            using var serialized = new SerializedObject(cache);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<WeaponCacheFeelProfile>(FeelPath),
                serialized.FindProperty("feel").objectReferenceValue);
            Assert.AreEqual(120f, serialized.FindProperty("distanceMin").floatValue);
            Assert.AreEqual(180f, serialized.FindProperty("distanceMax").floatValue);
            Assert.AreSame(prefab.GetComponentInChildren<SectorHunt>(true), serialized.FindProperty("hunt").objectReferenceValue,
                "The cache reads the sector's own hunt for its alarm.");
            Assert.AreSame(station, serialized.FindProperty("stations").objectReferenceValue,
                "The cache keeps its spacing from the sector's own repair stations.");

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
            Assert.IsTrue(icon, "The cache carries its find icon.");
            using var stationSerialized = new SerializedObject(station);
            Assert.AreNotSame(stationSerialized.FindProperty("icon").objectReferenceValue, icon,
                "The cache's icon is its own, not the repair station's.");
        }

        [Test]
        public void TheBoostMod_IsAPlusAQuarterDamageMod_ForEveryWeapon()
        {
            var boost = AssetDatabase.LoadAssetAtPath<WeaponMod>(BoostPath);
            Assert.AreEqual(WeaponModField.Damage, boost.Field);
            Assert.AreEqual(.25f, boost.Amount, 1e-6f);
            using var serialized = new SerializedObject(boost);
            Assert.AreEqual(0, serialized.FindProperty("fits").arraySize, "The boost lands on any carried weapon.");
            Assert.IsFalse(AssetDatabase.LoadAssetAtPath<LullModPool>("Assets/Settings/Weapons/Mods/LullMods.asset")
                .mods.Contains(boost), "The boost is the cache's, never a lull pick.");
        }
    }
}
