using NUnit.Framework;
using Substrate.Sectors.Elements.Stations;
using Substrate.Sectors.Elements.Stations.Visual;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Sectors
{
    /// <summary>Each station kind's body carries its kind's model outside the lit parts, on the shared motion feel, and every collider on the station is a trigger; the repair trigger is a sphere of radius 16.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class StationModelEditModeTests
    {
        private const string FeelPath = "Assets/Settings/Game/StationMotionFeel.asset";

        [TestCase("Assets/Prefabs/Objectives/Repair Station.prefab",
            "Assets/Visuals/Environment/Stations/Repair Station/Repair Station Model.prefab")]
        [TestCase("Assets/Prefabs/Objectives/Mod Station.prefab",
            "Assets/Visuals/Environment/Stations/Mod Station/Mod Station Model.prefab")]
        [TestCase("Assets/Prefabs/Objectives/Armory.prefab",
            "Assets/Visuals/Environment/Stations/Armory/Armory Model.prefab")]
        public void EachStationBody_CarriesItsKindsModel_OutsideItsLitParts(string stationPath, string modelPath)
        {
            var module = AssetDatabase.LoadAssetAtPath<GameObject>(stationPath).GetComponent<FindStation>();
            using var serialized = new SerializedObject(module);
            var body = (FindStationBody)serialized.FindProperty("body").objectReferenceValue;
            var motions = body.GetComponentsInChildren<StationMotion>(true);
            Assert.AreEqual(1, motions.Length, stationPath);
            var motion = motions[0];
            Assert.IsFalse(motion.transform.IsChildOf(body.Lit.transform), stationPath);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),
                PrefabUtility.GetCorrespondingObjectFromSource(motion.gameObject), stationPath);
            using var motionSerialized = new SerializedObject(motion);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<StationMotionFeelProfile>(FeelPath),
                motionSerialized.FindProperty("feel").objectReferenceValue, stationPath);
            Assert.AreSame(motion.GetComponent<StationModel>(), body.Model, stationPath);
        }

        [TestCase("Assets/Prefabs/Objectives/Repair Station.prefab")]
        [TestCase("Assets/Prefabs/Objectives/Mod Station.prefab")]
        [TestCase("Assets/Prefabs/Objectives/Armory.prefab")]
        public void EveryStationCollider_IsATrigger(string stationPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stationPath);
            foreach (var collider in prefab.GetComponentsInChildren<Collider>(true))
                Assert.IsTrue(collider.isTrigger, collider.name);
        }

        [Test]
        public void TheRepairTrigger_IsASphereOfRadius16()
        {
            var module = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objectives/Repair Station.prefab")
                .GetComponent<FindStation>();
            using var serialized = new SerializedObject(module);
            var body = (FindStationBody)serialized.FindProperty("body").objectReferenceValue;
            var sphere = body.Zone.GetComponent<SphereCollider>();
            Assert.IsTrue(sphere);
            Assert.AreEqual(16f, sphere.radius, 1e-4f);
        }
    }
}
