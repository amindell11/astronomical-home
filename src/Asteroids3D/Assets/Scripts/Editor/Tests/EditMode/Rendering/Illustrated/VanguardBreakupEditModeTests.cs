using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ships.Visuals.Breakup;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Rendering.Illustrated
{
    [Category("Ships")]
    public sealed class VanguardBreakupEditModeTests
    {
        private const string Folder = "Assets/Visuals/Ships/Vanguard/";
        private static readonly string[] Surviving = { "MVP Wing", "MVP wing_armature", "MVP wing_armor", "MVP Power Nacelle Housing", "MVP Sparrow Tail", "MVP Tail", "MVP wing_tail" };

        [Test]
        public void DebrisRetainsSourceTrianglesUVsAndNormalsWithAttachedCoresAndInk()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "DrawnStudy/VanguardStructure.fbx");
            var rig = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Ships/Ship_1_IllustratedRig.prefab");
            try
            {
                var hull = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Vanguard");
                var authored = hull.Find("Authored mesh");
                var matrix = hull.worldToLocalMatrix * authored.localToWorldMatrix;
                var debris = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Breakup/VanguardBreakup.prefab");
                var filters = debris.GetComponentsInChildren<MeshFilter>();
                Assert.That(filters, Has.Length.EqualTo(14));
                Assert.That(debris.GetComponentsInChildren<Rigidbody>(), Is.Empty);
                Assert.That(debris.GetComponentsInChildren<Collider>(), Is.Empty);
                var expected = new Dictionary<string, int>();
                var actual = new Dictionary<string, int>();
                foreach (var filter in source.GetComponentsInChildren<MeshFilter>().Where(f => Surviving.Contains(f.name)))
                {
                    var transform = matrix * source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var normalTransform = transform.inverse.transpose;
                    var mesh = filter.sharedMesh;
                    foreach (var index in mesh.triangles)
                        Count(expected, Key(transform.MultiplyPoint3x4(mesh.vertices[index]), mesh.uv[index], normalTransform.MultiplyVector(mesh.normals[index]).normalized));
                }
                foreach (var filter in filters)
                {
                    var mesh = filter.sharedMesh;
                    foreach (var index in mesh.GetIndices(0))
                        Count(actual, Key(filter.transform.localPosition + mesh.vertices[index], mesh.uv[index], mesh.normals[index]));
                    var materials = filter.GetComponent<Renderer>().sharedMaterials;
                    Assert.That(materials.All(m => m.HasProperty("_DebrisVisibility")), Is.True, filter.name);
                    Assert.That(materials[0].GetTexture("_BaseMap"), Is.SameAs(AssetDatabase.LoadAssetAtPath<Texture>(Folder + "DrawnStudy/VanguardBaseColor.png")));
                    Assert.That(materials[0].GetFloat("_SootStrength"), Is.EqualTo(.9f));
                    Assert.That(mesh.GetIndexCount(3), Is.EqualTo(mesh.GetIndexCount(0) + mesh.GetIndexCount(2)));
                    Assert.That(mesh.GetIndices(3).Intersect(mesh.GetIndices(0)), Is.Empty);
                    Assert.That(filter.transform.localPosition.x, filter.name.EndsWith("left") ? Is.LessThan(0) : Is.GreaterThan(0));
                }
                CollectionAssert.AreEquivalent(expected, actual, "Painted triangles, UVs and authored normals must survive unchanged.");
                Assert.That(filters.Where(f => f.name.StartsWith("Nacelle")).All(f => f.sharedMesh.GetIndexCount(2) > 0), Is.True);
                Assert.That(filters.Where(f => !f.name.StartsWith("Nacelle")).All(f => f.sharedMesh.GetIndexCount(2) == 0), Is.True);
                Assert.That(filters.Where(f => f.sharedMesh.GetIndexCount(1) > 0).Select(f => f.name), Is.EquivalentTo(new[] { "Armor left", "Armor right", "Sparrow tail left", "Sparrow tail right" }));
            }
            finally { PrefabUtility.UnloadPrefabContents(rig); }
        }

        [Test]
        public void BurstSeparatesIndependentTailsBeforePointTwoSeconds()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Breakup/VanguardBreakup.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var clip = instance.GetComponent<Animation>().clip;
                var transforms = instance.GetComponentsInChildren<MeshFilter>().Select(f => f.transform).ToArray();
                clip.SampleAnimation(instance, 0);
                var starts = transforms.Select(t => t.localPosition).ToArray();
                clip.SampleAnimation(instance, .2f);
                var bursts = transforms.Select((t, i) => t.localPosition - starts[i]).ToArray();
                clip.SampleAnimation(instance, 1.4f);
                for (var i = 0; i < transforms.Length; i++)
                    Assert.That(bursts[i].magnitude / (transforms[i].localPosition - starts[i]).magnitude, Is.GreaterThan(.6f), transforms[i].name);
                Assert.That(Vector3.Distance(bursts[8], bursts[10]), Is.GreaterThan(.1f));
                Assert.That(Vector3.Distance(bursts[10], bursts[12]), Is.GreaterThan(.1f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static string Key(Vector3 position, Vector2 uv, Vector3 normal) =>
            $"{Math.Round(position.x, 4)},{Math.Round(position.y, 4)},{Math.Round(position.z, 4)}:{Math.Round(uv.x, 4)},{Math.Round(uv.y, 4)}:{Math.Round(normal.x, 4)},{Math.Round(normal.y, 4)},{Math.Round(normal.z, 4)}";
        private static void Count(Dictionary<string, int> counts, string key) { counts.TryGetValue(key, out var count); counts[key] = count + 1; }
    }
}
