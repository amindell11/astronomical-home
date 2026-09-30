using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using NUnit.Framework;
using Ships.Visuals.Breakup;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Rendering.Illustrated
{
    [Category("Ships")]
    public sealed class CrimsonBreakupEditModeTests
    {
        [Test]
        public void Debris_PreservesSurvivingGeometry_AndSeparatesFinsWithScorchedPaint()
        {
            const string folder = "Assets/Visuals/Ships/Crimson/";
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Crimson.fbx");
            var debris = AssetDatabase.LoadAssetAtPath<ShipBreakupDebris>(folder + "Breakup/CrimsonBreakup.prefab");
            Assert.That(debris, Is.Not.Null);
            var filters = debris.GetComponentsInChildren<MeshFilter>();
            Assert.That(filters, Has.Length.EqualTo(18));
            Assert.That(debris.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(debris.transform.Find("Core"), Is.Null);
            Assert.That(debris.transform.Find("Canopy"), Is.Null);
            var fins = filters.Where(filter => filter.name.Contains("fin")).ToArray();
            Assert.That(fins, Has.Length.EqualTo(12));
            Assert.That(fins.Select(filter => filter.sharedMesh).Distinct().Count(), Is.EqualTo(12));
            var triangles = filters.SelectMany(filter =>
            {
                var vertices = filter.sharedMesh.vertices;
                return filter.sharedMesh.GetIndices(0).Select(index => filter.transform.localPosition + vertices[index]);
            }).ToArray();
            var consumed = new HashSet<string> { "Cube", "Structural center web", "Central hull", "Service channel floor",
                "Dorsal service spine", "Rear cockpit yoke", "Cockpit surround", "Canopy", "Canopy perimeter rim", "Canopy transverse frame" };
            var source = original.GetComponentsInChildren<MeshFilter>().Where(filter => !consumed.Contains(filter.name)).SelectMany(filter =>
            {
                using var dataArray = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                var data = dataArray[0];
                using var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
                using var indices = new NativeArray<int>(data.GetSubMesh(0).indexCount, Allocator.Temp);
                data.GetVertices(vertices);
                data.GetIndices(indices, 0);
                var matrix = original.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                return indices.ToArray().Select(index => matrix.MultiplyPoint3x4(vertices[index])).ToArray();
            }).ToArray();
            var order = Comparer<Vector3>.Create((left, right) =>
            {
                if (Mathf.Abs(left.x - right.x) > .00001f) return left.x.CompareTo(right.x);
                if (Mathf.Abs(left.y - right.y) > .00001f) return left.y.CompareTo(right.y);
                if (Mathf.Abs(left.z - right.z) > .00001f) return left.z.CompareTo(right.z);
                return 0;
            });
            Array.Sort(triangles, order);
            Array.Sort(source, order);
            Assert.That(triangles, Is.EqualTo(source).Using(new Vector3EqualityComparer(.00001f)),
                "Surviving pieces must retain the authored geometry; only the consumed cockpit and core are omitted.");
            var visibility = Shader.PropertyToID("_DebrisVisibility");
            var soot = Shader.PropertyToID("_SootStrength");
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                Assert.That(mesh.GetIndexCount(1), Is.EqualTo(mesh.GetIndexCount(0)), filter.name);
                Assert.That(mesh.GetIndices(0).Intersect(mesh.GetIndices(1)), Is.Empty, filter.name);
                var materials = filter.GetComponent<Renderer>().sharedMaterials;
                Assert.That(materials.All(material => material.HasProperty(visibility)), Is.True);
                Assert.That(materials[0].GetFloat(soot), Is.GreaterThan(.5f));
                Assert.That(mesh.colors.Max(color => color.r) - mesh.colors.Min(color => color.r), Is.GreaterThan(.1f));
            }
            Assert.That(filters.Single(filter => filter.name == "Engine left").transform.localPosition.x, Is.LessThan(0));
            Assert.That(filters.Single(filter => filter.name == "Engine right").transform.localPosition.x, Is.GreaterThan(0));
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(folder + "Hull paint.mat").GetFloat(soot), Is.Zero);
        }

        [Test]
        public void AuthoredBurst_FrontLoadsSeparation_AndFinsHaveDifferentPaths()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Visuals/Ships/Crimson/Breakup/CrimsonBreakup.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var clip = instance.GetComponent<Animation>().clip;
                var wing = instance.transform.Find("Wing left");
                clip.SampleAnimation(instance, 0);
                var start = wing.localPosition;
                var fins = instance.GetComponentsInChildren<MeshFilter>().Where(filter => filter.name.Contains("fin") && filter.name.EndsWith("left")).ToArray();
                var finStarts = fins.Select(filter => filter.transform.localPosition).ToArray();
                clip.SampleAnimation(instance, .2f);
                var burst = Vector3.Distance(start, wing.localPosition);
                var finMotion = fins.Select((filter, index) => filter.transform.localPosition - finStarts[index]).ToArray();
                Assert.That(Vector3.Distance(finMotion[0], finMotion[1]), Is.GreaterThan(.01f));
                clip.SampleAnimation(instance, 1.4f);
                Assert.That(burst / Vector3.Distance(start, wing.localPosition), Is.GreaterThan(.6f));
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
