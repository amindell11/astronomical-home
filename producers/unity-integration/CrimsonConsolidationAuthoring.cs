using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Rendering.Illustrated
{
    public sealed class CrimsonConsolidationAuthoring
    {
        const string Folder = "Assets/Visuals/Ships/Crimson/";
        const string Rig = "Assets/Prefabs/Ships/Ship_2_IllustratedRig.prefab";

        [Test]
        public void CombineSavedHull()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "Crimson.fbx");
            importer.isReadable = true;
            importer.SaveAndReimport();
            var root = PrefabUtility.LoadPrefabContents(Rig);
            try
            {
                var hull = root.transform.Find("Model/Crimson");
                var renderers = hull.GetComponentsInChildren<MeshRenderer>();
                var surfaces = renderers.Where(r => r.sharedMaterial.shader.name.EndsWith("Drawn Surface")).ToArray();
                var shells = renderers.Where(r => r.sharedMaterial.shader.name.EndsWith("Drawn Contour")).ToArray();
                Assert.That(surfaces.Length, Is.GreaterThan(1));
                Assert.That(shells.Length, Is.EqualTo(surfaces.Length));
                var paint = surfaces[0].sharedMaterial;
                var ink = shells[0].sharedMaterial;
                var surface = Combine(surfaces, hull);
                var contour = Combine(shells, hull);
                AssertEquivalent(surfaces, hull, surface);
                AssertEquivalent(shells, hull, contour);
                var combined = new Mesh { name = "Crimson", indexFormat = IndexFormat.UInt32 };
                combined.CombineMeshes(new[] {
                    new CombineInstance { mesh = surface },
                    new CombineInstance { mesh = contour }
                }, false, false);
                Assert.That(combined.subMeshCount, Is.EqualTo(2));
                Assert.That(combined.vertexCount, Is.EqualTo(surface.vertexCount + contour.vertexCount));
                Assert.That(combined.GetIndexCount(0), Is.EqualTo(surface.GetIndexCount(0)));
                Assert.That(combined.GetIndexCount(1), Is.EqualTo(contour.GetIndexCount(0)));
                AssetDatabase.CreateAsset(combined, Folder + "Crimson.asset");
                var report = $"Consolidated {surfaces.Length} painted pieces and {shells.Length} shells into one mesh, one renderer, two material slots. Paint vertices={surface.vertexCount}; contour vertices={contour.vertexCount}; triangles={combined.triangles.Length / 3}. Transformed positions, UVs and normals match the source geometry.\n";
                Object.DestroyImmediate(surface);
                Object.DestroyImmediate(contour);
                foreach (var child in hull.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                hull.gameObject.AddComponent<MeshFilter>().sharedMesh = combined;
                var renderer = hull.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { paint, ink };
                PrefabUtility.SaveAsPrefabAsset(root, Rig);
                foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { Folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(" outline.asset", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
                }
                AssetDatabase.SaveAssets();
                File.WriteAllText("D:/amind/git/astronomical-home/results/visual-playable/crimson-consolidation.txt", report);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        static Mesh Combine(MeshRenderer[] renderers, Transform root)
        {
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(renderers.Select(r => new CombineInstance {
                mesh = r.GetComponent<MeshFilter>().sharedMesh,
                transform = root.worldToLocalMatrix * r.transform.localToWorldMatrix
            }).ToArray(), true, true);
            return mesh;
        }

        static void AssertEquivalent(MeshRenderer[] renderers, Transform root, Mesh combined)
        {
            var output = combined.vertices;
            var normals = combined.normals;
            var uvs = combined.uv;
            var cursor = 0;
            foreach (var renderer in renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var matrix = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                var normalMatrix = matrix.inverse.transpose;
                var vertices = mesh.vertices;
                var sourceNormals = mesh.normals;
                var sourceUvs = mesh.uv;
                for (var i = 0; i < vertices.Length; i++, cursor++)
                {
                    Assert.That(Vector3.Distance(output[cursor], matrix.MultiplyPoint3x4(vertices[i])), Is.LessThan(.00001f));
                    Assert.That(Vector3.Distance(normals[cursor].normalized, normalMatrix.MultiplyVector(sourceNormals[i]).normalized), Is.LessThan(.00001f));
                    Assert.That(Vector2.Distance(uvs[cursor], sourceUvs[i]), Is.LessThan(.00001f));
                }
            }
            Assert.That(cursor, Is.EqualTo(combined.vertexCount));
        }
    }
}
