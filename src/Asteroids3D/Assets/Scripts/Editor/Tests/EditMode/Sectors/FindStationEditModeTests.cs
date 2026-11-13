using NUnit.Framework;
using Substrate.Sectors.Elements.Stations;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Sectors
{
    /// <summary>Every station kind's body carries its zone, lit parts and minimap marker, and a copy of the body points at the copy's own parts, not the authored body's.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class FindStationEditModeTests
    {
        [TestCase("Assets/Prefabs/Objectives/Repair Station.prefab")]
        [TestCase("Assets/Prefabs/Objectives/Mod Station.prefab")]
        [TestCase("Assets/Prefabs/Objectives/Armory.prefab")]
        public void ACopiedBody_PointsAtItsOwnParts(string path)
        {
            var module = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<FindStation>();
            using var serialized = new SerializedObject(module);
            var body = (FindStationBody)serialized.FindProperty("body").objectReferenceValue;
            var copy = Object.Instantiate(body);
            try
            {
                Assert.IsTrue(copy.Zone.transform.IsChildOf(copy.transform), "The copy's zone is its own.");
                Assert.IsTrue(copy.Lit.transform.IsChildOf(copy.transform), "The copy's lit part is its own.");
                Assert.IsTrue(copy.Marker.transform.IsChildOf(copy.Lit.transform), "The copy's marker is under its lit part.");
                Assert.AreNotSame(body.Zone, copy.Zone);
                Assert.AreNotSame(body.Lit, copy.Lit);
                Assert.AreNotSame(body.Marker, copy.Marker);
            }
            finally
            {
                Object.DestroyImmediate(copy.gameObject);
            }
        }
    }
}
