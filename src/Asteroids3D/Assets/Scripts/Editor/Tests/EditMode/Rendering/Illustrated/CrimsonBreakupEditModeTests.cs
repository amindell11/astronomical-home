using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ships.Visuals.Breakup;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Tests.EditMode.Rendering.Illustrated
{
    [Category("Ships")]
    public sealed class CrimsonBreakupEditModeTests
    {
        [Test]
        public void Debris_ReconstructsIntactPaintedGeometry_AndHasIndependentContours()
        {
            const string folder = "Assets/Visuals/Ships/Crimson/";
            var original = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "Crimson.asset");
            var debris = AssetDatabase.LoadAssetAtPath<ShipBreakupDebris>(folder + "Breakup/CrimsonBreakup.prefab");
            Assert.That(debris, Is.Not.Null);
            var filters = debris.GetComponentsInChildren<MeshFilter>();
            Assert.That(filters, Has.Length.EqualTo(10));
            Assert.That(debris.GetComponentsInChildren<Collider>(), Is.Empty);
            var triangles = filters.SelectMany(filter =>
            {
                var vertices = filter.sharedMesh.vertices;
                return filter.sharedMesh.GetIndices(0).Select(index => filter.transform.localPosition + vertices[index]);
            }).ToArray();
            var sourceVertices = original.vertices;
            var source = original.GetIndices(0).Select(index => sourceVertices[index]).ToArray();
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
                "Assembled debris must reproduce every intact painted triangle vertex without moving the authored geometry.");
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                Assert.That(mesh.GetIndexCount(1), Is.EqualTo(mesh.GetIndexCount(0)), filter.name);
                Assert.That(mesh.GetIndices(0).Intersect(mesh.GetIndices(1)), Is.Empty, filter.name);
                Assert.That(filter.GetComponent<Renderer>().sharedMaterials.All(material => material.HasProperty("_DebrisVisibility")), Is.True);
            }
            Assert.That(filters.Single(filter => filter.name == "Engine left").transform.localPosition.x, Is.LessThan(0));
            Assert.That(filters.Single(filter => filter.name == "Engine right").transform.localPosition.x, Is.GreaterThan(0));
        }
    }
}
