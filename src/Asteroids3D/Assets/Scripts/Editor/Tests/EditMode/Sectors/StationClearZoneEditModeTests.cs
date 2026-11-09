using System.Linq;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Stations;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Sectors
{
    /// <summary>Each trial sector's asteroid field reads its own repair stations, mod stations and armories for their clearings, and each station kind carries its own clear radius, 35 u as shipped.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class StationClearZoneEditModeTests
    {
        [TestCase("TrialSector")]
        [TestCase("TrialSector2")]
        [TestCase("TrialSector3")]
        public void TrialSector_TheFieldReadsItsOwnStations(string sector)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Sector>($"Assets/Prefabs/Sectors/{sector}.prefab");
            var spawner = prefab.Spawners.OfType<AsteroidFieldSpawner>().Single();
            using var serialized = new SerializedObject(spawner);
            Assert.AreSame(prefab.Modules.OfType<RepairStation>().Single(),
                serialized.FindProperty("repairs").objectReferenceValue);
            Assert.AreSame(prefab.Modules.OfType<ModStation>().Single(),
                serialized.FindProperty("mods").objectReferenceValue);
            Assert.AreSame(prefab.Modules.OfType<Armory>().Single(),
                serialized.FindProperty("armories").objectReferenceValue);
        }

        [TestCase("Assets/Prefabs/Objectives/Repair Station.prefab", typeof(RepairStation))]
        [TestCase("Assets/Prefabs/Objectives/Mod Station.prefab", typeof(ModStation))]
        [TestCase("Assets/Prefabs/Objectives/Armory.prefab", typeof(Armory))]
        public void EachStationKind_CarriesItsOwnClearRadius(string path, System.Type kind)
        {
            var module = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent(kind);
            using var serialized = new SerializedObject(module);
            Assert.AreEqual(35f, serialized.FindProperty("clearRadius").floatValue);
        }
    }
}
