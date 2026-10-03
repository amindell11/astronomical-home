using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Ships.Visuals.Wings;
using UnityEditor;
using UnityEngine;

public static class RebuildValis
{
    private const string Work = "D:/amind/git/astronomical-home/results/valis-wing-motion/v02/";
    private static Vector3 V(JToken t) => new Vector3((float)t["x"], (float)t["y"], (float)t["z"]);
    private static Quaternion Q(JToken t) => new Quaternion((float)t["x"], (float)t["y"], (float)t["z"], (float)t["w"]);
    public static string Main()
    {
        const string prefabPath = "Assets/Prefabs/Ships/Valis.prefab";
        const string meshPath = "Assets/Visuals/Ships/Valis/Meshes/Valis hull.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        var placement = JObject.Parse(File.ReadAllText(Work + "unity-build.json"));
        var scale = (float)placement["scale"];
        var center = V(placement["center"]);
        var data = JObject.Parse(File.ReadAllText(Work + "approved-export.json"));
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var renderer = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Valis");
            var model = renderer.transform;
            var frame = renderer.bones[1].parent;
            UnityEngine.Object.DestroyImmediate(frame.gameObject);
            frame = new GameObject("Flight surfaces").transform;
            frame.SetParent(model, false);
            var bones = new List<Transform> { model };
            var rotations = new List<Quaternion>();
            var groups = new Dictionary<string, int[]>();
            foreach (var name in new[] { "Upper wings", "Lower wings" })
            {
                var motion = data["models"][name];
                var pivot = V(motion["pivot"]);
                var rotation = Q(motion["rest_rotation"]);
                var indices = new int[2];
                for (var side = 0; side < 2; side++)
                {
                    var bone = new GameObject(name == "Upper wings" ? "Upper wing" : "Lower wing").transform;
                    bone.name += side == 0 ? " right" : " left";
                    bone.SetParent(frame, false);
                    var p = side == 0 ? pivot : new Vector3(-pivot.x, pivot.y, pivot.z);
                    bone.localPosition = (p - center) * scale;
                    indices[side] = bones.Count;
                    bones.Add(bone);
                    rotations.Add(side == 0 ? rotation : new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w));
                }
                groups.Add(name, indices);
            }
            var bindposes = bones.Select(b => b.worldToLocalMatrix * model.localToWorldMatrix).ToArray();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var sections = Enumerable.Range(0, 8).Select(_ => new List<int>()).ToArray();
            var regions = new[] { "Ivory", "Cool gray", "Lavender", "Graphite", "Edge", "Canopy", "Engine" };
            foreach (var part in data["parts"])
            {
                var positions = part["vertices"].Select(V).ToArray();
                var faceNormals = part["normals"].Select(V).ToArray();
                var paint = part["uv"].Select(t => new Vector2((float)t["x"], (float)t["y"])).ToArray();
                var region = Array.IndexOf(regions, (string)part["material"]);
                if (region < 0) throw new Exception("Unknown paint region: " + part["material"]);
                var group = (string)part["group"];
                var partWeights = positions.Select(p => new BoneWeight { boneIndex0 = group == null ? 0 : groups[group][p.x >= 0f ? 0 : 1], weight0 = 1f }).ToArray();
                var averaged = new Dictionary<Vector3, Vector3>();
                for (var i = 0; i < positions.Length; i++)
                {
                    averaged.TryGetValue(positions[i], out var normal);
                    averaged[positions[i]] = normal + faceNormals[i];
                }
                for (var contour = 0; contour < 2; contour++)
                {
                    var offset = vertices.Count;
                    vertices.AddRange(positions.Select(p => (p - center) * scale));
                    normals.AddRange(contour == 0 ? faceNormals : positions.Select(p => averaged[p].normalized));
                    uv.AddRange(paint);
                    weights.AddRange(partWeights);
                    sections[contour == 0 ? region : 7].AddRange(Enumerable.Range(offset, positions.Length));
                }
            }
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 8;
            for (var i = 0; i < 8; i++) mesh.SetTriangles(sections[i], i);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bindposes;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            if (mesh.vertexCount != vertices.Count || mesh.boneWeights.Length != vertices.Count)
                throw new Exception("Authored geometry and weight counts differ.");
            renderer.sharedMesh = mesh;
            renderer.bones = bones.ToArray();
            renderer.rootBone = model;
            renderer.quality = SkinQuality.Bone1;
            var bounds = mesh.bounds;
            for (var sample = 0; sample <= 32; sample++)
                for (var i = 1; i < bones.Count; i++)
                {
                    var pose = Matrix4x4.TRS(bones[i].localPosition, Quaternion.Slerp(rotations[i - 1], Quaternion.identity, sample / 32f), Vector3.one) * bindposes[i];
                    for (var v = 0; v < vertices.Count; v++)
                        if (weights[v].boneIndex0 == i) bounds.Encapsulate(pose.MultiplyPoint3x4(vertices[v]));
                }
            bounds.Expand(.02f);
            renderer.localBounds = bounds;
            var component = model.GetComponent<WingVisuals>();
            var serialized = new SerializedObject(component);
            var joints = serialized.FindProperty("joints");
            joints.arraySize = 4;
            for (var i = 0; i < 4; i++)
            {
                var joint = joints.GetArrayElementAtIndex(i);
                joint.FindPropertyRelative("bone").objectReferenceValue = bones[i + 1];
                joint.FindPropertyRelative("restRotation").quaternionValue = rotations[i];
                bones[i + 1].localRotation = rotations[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            var result = new { vertices = vertices.Count, parts = data["parts"].Count(), bones = bones.Count, scale, center = new { center.x, center.y, center.z } };
            File.WriteAllText(Work + "unity-build.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return $"Updated {result.parts} source parts, {result.vertices} surface/contour vertices, four complete wings; preserved scale {scale} and center {center}.";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
