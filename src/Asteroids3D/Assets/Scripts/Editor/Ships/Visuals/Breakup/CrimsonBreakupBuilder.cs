using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Utils;
using Object = UnityEngine.Object;

namespace Ships.Visuals.Breakup
{
    public static class CrimsonBreakupBuilder
    {
        private const string Folder = "Assets/Visuals/Ships/Crimson";
        private const string Output = Folder + "/Breakup";
        private const string RigPath = "Assets/Prefabs/Ships/Ship_2_IllustratedRig.prefab";
        private const string ExplosionPath = "Assets/Visuals/Vfx/LayeredExplosion/Prefabs/LayeredAsteroidExplosion.prefab";

        private static readonly string[][] PartGroups =
        {
            new[] { "Upper wing foundation", "Lower wing foundation", "Wing root beam", "Shoulder underside link" },
            new[] { "Upper aft shoulder armor", "Upper inner shoulder armor", "Upper prow armor", "Upper prow tip",
                "Upper outer armor rail", "Lower aft shoulder armor", "Lower inner shoulder armor", "Lower prow armor",
                "Lower prow tip", "Lower outer armor rail", "Outer rail mounting block", "Shoulder service pod", "Service pod cap" },
            new[] { "Engine support frame", "Engine forward cowling", "Engine housing", "Engine neck", "Engine nozzle", "Engine throat" },
            new[] { "Upper Aft swept fin", "Lower Aft swept fin", "Upper Outer swept fin", "Lower Outer swept fin",
                "Upper Forward fin", "Lower Forward fin" },
            new[] { "Cube", "Structural center web", "Central hull", "Service channel floor", "Dorsal service spine", "Rear cockpit yoke" },
            new[] { "Cockpit surround", "Canopy", "Canopy perimeter rim", "Canopy transverse frame" }
        };

        [MenuItem("Tools/Ships/Build Crimson Breakup")]
        public static void Build()
        {
            var source = Load<GameObject>(Folder + "/Crimson.fbx");
            var paint = Load<Material>(Folder + "/Hull paint.mat");
            var contour = Load<Material>("Assets/Visuals/Ships/Shared/Illustrated/Ship contour.mat");
            var explosion = Load<GameObject>(ExplosionPath).GetComponent<PooledVFX>();
            if (!explosion) throw new InvalidOperationException("LayeredAsteroidExplosion requires PooledVFX.");
            var filters = source.GetComponentsInChildren<MeshFilter>(true).ToDictionary(filter => filter.name);
            var expected = PartGroups.SelectMany(group => group).ToArray();
            if (filters.Count != expected.Length || expected.Any(name => !filters.ContainsKey(name)))
                throw new InvalidOperationException("Crimson FBX part names must match the approved 39-part source.");
            if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder(Folder, "Breakup");

            var rig = PrefabUtility.LoadPrefabContents(RigPath);
            var root = new GameObject("Crimson breakup");
            try
            {
                var hull = rig.GetComponentsInChildren<Transform>(true).Single(transform => transform.name == "Crimson");
                var debris = root.AddComponent<ShipBreakupDebris>();
                var serialized = new SerializedObject(debris);
                var pieces = serialized.FindProperty("pieces");
                pieces.arraySize = 10;
                serialized.FindProperty("explosionPrefab").objectReferenceValue = explosion;
                var names = new[] { "Wing", "Armor", "Engine", "Fins", "Core", "Canopy" };
                var delays = new[] { .06f, .1f, .16f, .22f, .32f, .27f };
                var velocities = new[]
                {
                    new Vector3(.44f, .045f, -.05f), new Vector3(.60f, -.06f, -.15f),
                    new Vector3(.17f, .025f, .52f), new Vector3(.67f, .08f, .24f),
                    new Vector3(-.04f, -.06f, .09f), new Vector3(.08f, .16f, -.28f)
                };
                var spins = new[]
                {
                    new Vector3(18, 38, 25), new Vector3(-30, 48, 42), new Vector3(35, -24, 18),
                    new Vector3(80, 100, 110), new Vector3(14, 22, -16), new Vector3(-52, 34, 65)
                };
                var width = hull.GetComponent<MeshFilter>().sharedMesh.bounds.size.x;
                var index = 0;
                for (var group = 0; group < PartGroups.Length; group++)
                {
                    var sides = group < 4 ? new[] { -1, 1 } : new[] { 0 };
                    foreach (var side in sides)
                    {
                        var name = names[group] + (side == 0 ? "" : side < 0 ? " left" : " right");
                        var mesh = BuildMesh(source.transform, PartGroups[group].Select(part => filters[part]), side, name, out var pivot);
                        var path = Output + "/" + name + ".asset";
                        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if (existing)
                        {
                            EditorUtility.CopySerialized(mesh, existing);
                            Object.DestroyImmediate(mesh);
                            mesh = existing;
                        }
                        else AssetDatabase.CreateAsset(mesh, path);
                        var piece = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                        piece.layer = hull.gameObject.layer;
                        piece.transform.SetParent(root.transform, false);
                        piece.transform.localPosition = pivot;
                        piece.GetComponent<MeshFilter>().sharedMesh = mesh;
                        piece.GetComponent<MeshRenderer>().sharedMaterials = new[] { paint, contour };
                        var entry = pieces.GetArrayElementAtIndex(index++);
                        entry.FindPropertyRelative("transform").objectReferenceValue = piece.transform;
                        var velocity = velocities[group];
                        var spin = spins[group];
                        if (side != 0)
                        {
                            velocity.x *= side;
                            spin.y *= side;
                            spin.z *= side;
                            if (side > 0) { velocity *= .92f; spin *= 1.13f; }
                        }
                        entry.FindPropertyRelative("velocity").vector3Value = velocity * width;
                        entry.FindPropertyRelative("spin").vector3Value = spin;
                        entry.FindPropertyRelative("delay").floatValue = delays[group] + (side > 0 ? .035f : 0);
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var saved = PrefabUtility.SaveAsPrefabAsset(root, Output + "/CrimsonBreakup.prefab");
                var visual = rig.GetComponent<ShipBreakupVisual>();
                if (!visual) visual = rig.AddComponent<ShipBreakupVisual>();
                var visualData = new SerializedObject(visual);
                visualData.FindProperty("hull").objectReferenceValue = hull;
                visualData.FindProperty("debrisPrefab").objectReferenceValue = saved.GetComponent<ShipBreakupDebris>();
                visualData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(rig, RigPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(root);
                PrefabUtility.UnloadPrefabContents(rig);
            }
        }

        private static Mesh BuildMesh(Transform root, IEnumerable<MeshFilter> filters, int side, string name, out Vector3 pivot)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var contourNormals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var indices = new List<int>();
            foreach (var filter in filters)
            {
                using var dataArray = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                var data = dataArray[0];
                if (data.subMeshCount != 1) throw new InvalidOperationException(filter.name + " requires one painted submesh.");
                using var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
                using var sourceNormals = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
                using var sourceUvs = new NativeArray<Vector2>(data.vertexCount, Allocator.Temp);
                using var triangles = new NativeArray<int>(data.GetSubMesh(0).indexCount, Allocator.Temp);
                data.GetVertices(vertices);
                data.GetNormals(sourceNormals);
                data.GetUVs(0, sourceUvs);
                data.GetIndices(triangles, 0);
                var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var normalMatrix = matrix.inverse.transpose;
                var transformed = new Vector3[data.vertexCount];
                for (var i = 0; i < transformed.Length; i++) transformed[i] = matrix.MultiplyPoint3x4(vertices[i]);
                var sums = new Dictionary<Vector3, Vector3>();
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var a = transformed[triangles[i]];
                    var b = transformed[triangles[i + 1]];
                    var c = transformed[triangles[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a);
                    for (var corner = 0; corner < 3; corner++)
                    {
                        var position = transformed[triangles[i + corner]];
                        sums.TryGetValue(position, out var sum);
                        sums[position] = sum + normal;
                    }
                }
                var remap = new Dictionary<int, int>();
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var centerX = (transformed[triangles[i]].x + transformed[triangles[i + 1]].x + transformed[triangles[i + 2]].x) / 3;
                    if (side < 0 && centerX >= 0 || side > 0 && centerX < 0) continue;
                    for (var corner = 0; corner < 3; corner++)
                    {
                        var sourceIndex = triangles[i + corner];
                        if (!remap.TryGetValue(sourceIndex, out var targetIndex))
                        {
                            targetIndex = positions.Count;
                            remap.Add(sourceIndex, targetIndex);
                            positions.Add(transformed[sourceIndex]);
                            normals.Add(normalMatrix.MultiplyVector(sourceNormals[sourceIndex]).normalized);
                            contourNormals.Add(sums[transformed[sourceIndex]].normalized);
                            uvs.Add(sourceUvs[sourceIndex]);
                        }
                        indices.Add(targetIndex);
                    }
                }
            }
            if (positions.Count == 0) throw new InvalidOperationException(name + " contains no source triangles.");
            var bounds = new Bounds(positions[0], Vector3.zero);
            foreach (var position in positions) bounds.Encapsulate(position);
            pivot = bounds.center;
            for (var i = 0; i < positions.Count; i++) positions[i] -= pivot;
            var count = positions.Count;
            positions.AddRange(positions.ToArray());
            normals.AddRange(contourNormals);
            uvs.AddRange(uvs.ToArray());
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, subMeshCount = 2 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);
            mesh.SetTriangles(indices.Select(index => index + count).ToArray(), 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new InvalidOperationException("Missing Crimson breakup source: " + path);
            return asset;
        }
    }
}
