using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    [Category("Ship")]
    public sealed class VanguardVariantAssetTests
    {
        private const string SourceShipPath = "Assets/Prefabs/Ships/Ship_1.prefab";
        private const string ShipVariantPath = "Assets/Prefabs/Ships/Ship_1_Vanguard.prefab";
        private const string RigVariantPath = "Assets/Prefabs/Ships/Ship_1_Vanguard_VisualRig.prefab";

        [Test]
        public void VanguardShipIsVisualOnlyVariantOfShipOne()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceShipPath);
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(ShipVariantPath);

            Assert.That(source, Is.Not.Null);
            Assert.That(variant, Is.Not.Null);
            Assert.That(PrefabUtility.GetPrefabAssetType(variant), Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(variant), Is.SameAs(source));

            var sourceTypes = source.GetComponents<Component>().Select(component => component.GetType()).ToArray();
            var variantTypes = variant.GetComponents<Component>().Select(component => component.GetType()).ToArray();
            Assert.That(variantTypes, Is.EqualTo(sourceTypes));

            var sourceCollider = source.transform.Find("Mesh").GetComponent<MeshCollider>();
            var variantCollider = variant.transform.Find("Mesh").GetComponent<MeshCollider>();
            Assert.That(variantCollider.sharedMesh, Is.SameAs(sourceCollider.sharedMesh));

            AssertTransformMatches(source.transform.Find("Hardpoints/Primary"), variant.transform.Find("Hardpoints/Primary"));
            AssertTransformMatches(source.transform.Find("Hardpoints/Secondary"), variant.transform.Find("Hardpoints/Secondary"));
            Assert.That(variant.transform.Find("Ship_1_VisualRig"), Is.Null);
            Assert.That(variant.transform.Find("Ship_1_Vanguard_VisualRig"), Is.Not.Null);
        }

        [Test]
        public void VanguardRigUsesExpectedMeshAndMaterials()
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigVariantPath);
            Assert.That(rig, Is.Not.Null);
            Assert.That(PrefabUtility.GetPrefabAssetType(rig), Is.EqualTo(PrefabAssetType.Variant));

            var model = rig.transform.Find("Model");
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            var materials = model.GetComponent<MeshRenderer>().sharedMaterials;

            Assert.That(mesh.name, Is.EqualTo("Vanguard_Model"));
            Assert.That(mesh.subMeshCount, Is.EqualTo(6));
            Assert.That(mesh.bounds.size.y, Is.GreaterThan(mesh.bounds.size.x));
            Assert.That(mesh.bounds.size.y, Is.GreaterThan(mesh.bounds.size.z));
            Assert.That(Vector3.Angle(model.localRotation * Vector3.up, Vector3.up), Is.LessThan(0.01f));
            Assert.That(Vector3.Angle(model.localRotation * Vector3.forward, Vector3.back), Is.LessThan(0.01f));
            Assert.That(materials.Select(material => material.name), Is.EquivalentTo(new[]
            {
                "VNG_Hull_White",
                "VNG_Panel_Gray",
                "VNG_Mechanical_Charcoal",
                "VNG_Accent_Orange",
                "VNG_Canopy_Smoke",
                "VNG_Engine_Blue"
            }));
        }

        private static void AssertTransformMatches(Transform expected, Transform actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.localPosition, Is.EqualTo(expected.localPosition));
            Assert.That(actual.localRotation, Is.EqualTo(expected.localRotation));
            Assert.That(actual.localScale, Is.EqualTo(expected.localScale));
        }
    }
}
