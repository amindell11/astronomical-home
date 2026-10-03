using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Ships.Visuals;
using Ships.Visuals.Wings;
using UnityEditor;
using UnityEngine;

public static class BuildValisWings
{
    private static Vector3 V(JToken t) => new Vector3((float)t["x"], (float)t["y"], (float)t["z"]);
    private static Vector3 Convert(Vector3 v) => new Vector3(v.x, -v.y, -v.z);
    private static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);
    public static string Main()
    {
        var data = JObject.Parse(File.ReadAllText("D:/amind/git/astronomical-home/results/valis-wing-motion/rig-export.json"));
        var parts = (JArray)data["parts"];
        var source = parts.SelectMany(p => p["vertices"].Select(V)).ToArray();
        const string prefabPath = "Assets/Prefabs/Ships/Valis.prefab";
        const string meshPath = "Assets/Visuals/Ships/Valis/Meshes/Valis hull.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        var vertices = mesh.vertices;
        if (vertices.Length != source.Length * 2) throw new Exception("Source/hull vertex counts differ.");
        // Recover the original builder's uniform scale and Y centering from surface vertices.
        var scale = vertices[1].x / source[1].x;
        var center = source[0] - vertices[0] / scale;
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var old = root.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "Valis");
            var model = old.transform;
            if (old is SkinnedMeshRenderer previous)
            {
                foreach (var b in previous.bones.Skip(1)) UnityEngine.Object.DestroyImmediate(b.gameObject);
                UnityEngine.Object.DestroyImmediate(model.GetComponent<WingVisuals>());
            }
            var frame = new GameObject("Wing Frame").transform;
            frame.SetParent(model, false);
            frame.localPosition = -center * scale;
            frame.localScale = V(data["models"]["10 upper swept wings"]["frame_scale"]);
            var bones = new List<Transform> { model };
            var authored = new List<(Transform bone, Vector3 pivot, Vector3 translation, Quaternion rotation, float idle)>();
            var weights = new BoneWeight[vertices.Length];
            var offset = 0;
            var maxError = 0f;
            foreach (var part in parts)
            {
                var name = (string)part["name"];
                var points = part["vertices"].Select(V).ToArray();
                var motion = data["models"][name];
                var indices = new int[2];
                if (motion != null)
                {
                    var pivot = Convert(V(motion["pivot"])) * scale;
                    var translation = Convert(V(motion["translation"])) * scale;
                    var q = motion["rotation"];
                    var rotation = new Quaternion((float)q["x"], -(float)q["y"], -(float)q["z"], (float)q["w"]);
                    for (var side = 0; side < 2; side++)
                    {
                        var bone = new GameObject(name + (side == 0 ? " right" : " left")).transform;
                        bone.SetParent(frame, false);
                        var p = side == 0 ? pivot : Mirror(pivot);
                        var t = side == 0 ? translation : Mirror(translation);
                        var r = side == 0 ? rotation : new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
                        bone.localPosition = p;
                        indices[side] = bones.Count;
                        bones.Add(bone);
                        authored.Add((bone, p, t, r, (float)motion["idle_fraction"]));
                    }
                }
                for (var i = 0; i < points.Length; i++)
                {
                    var expected = (points[i] - center) * scale;
                    var index = offset + i;
                    var error = Vector3.Distance(vertices[index], expected);
                    maxError = Mathf.Max(maxError, error);
                    if (error > .00001f) throw new Exception($"Source surface mismatch {name} {i}: {error}, offset {offset}.");
                    var uv = part["uv"][i];
                    if (Vector2.Distance(mesh.uv[index], new Vector2((float)uv["x"], (float)uv["y"])) > .00001f)
                        throw new Exception("Paint UV mismatch: " + name);
                    var weight = new BoneWeight { boneIndex0 = indices[points[i].x >= 0f ? 0 : 1], weight0 = 1f };
                    weights[index] = weight;
                    weights[index + points.Length] = weight;
                    if (Vector3.Distance(vertices[index + points.Length], vertices[index]) > .00001f)
                        throw new Exception("Contour ordering mismatch: " + name);
                }
                offset += points.Length * 2;
            }
            var bindposes = bones.Select(b => b.worldToLocalMatrix * model.localToWorldMatrix).ToArray();
            var animated = UnityEngine.Object.Instantiate(mesh);
            animated.name = mesh.name;
            animated.boneWeights = weights;
            animated.bindposes = bindposes;
            EditorUtility.CopySerialized(animated, mesh);
            UnityEngine.Object.DestroyImmediate(animated);
            var materials = old.sharedMaterials;
            var shadows = old.shadowCastingMode;
            var receive = old.receiveShadows;
            var lightProbes = old.lightProbeUsage;
            var reflections = old.reflectionProbeUsage;
            var layers = old.renderingLayerMask;
            UnityEngine.Object.DestroyImmediate(old);
            var renderer = model.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = receive;
            renderer.lightProbeUsage = lightProbes;
            renderer.reflectionProbeUsage = reflections;
            renderer.renderingLayerMask = layers;
            renderer.rootBone = model;
            renderer.bones = bones.ToArray();
            renderer.quality = SkinQuality.Bone1;
            var bounds = mesh.bounds;
            for (var sample = 0; sample <= 40; sample++)
            {
                var fraction = sample / 40f;
                for (var i = 0; i < authored.Count; i++)
                {
                    var a = authored[i];
                    var matrix = model.worldToLocalMatrix * frame.localToWorldMatrix * Matrix4x4.TRS(a.pivot + a.translation * fraction,
                        Quaternion.Slerp(Quaternion.identity, a.rotation, fraction), Vector3.one) * bindposes[i + 1];
                    for (var v = 0; v < vertices.Length; v++)
                        if (weights[v].boneIndex0 == i + 1) bounds.Encapsulate(matrix.MultiplyPoint3x4(vertices[v]));
                }
            }
            bounds.Expand(.02f);
            renderer.localBounds = bounds;
            var hull = new SerializedObject(root.GetComponentInChildren<HullVisuals>(true));
            hull.FindProperty("hull").objectReferenceValue = renderer;
            hull.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(model.GetComponent<MeshFilter>());
            var component = model.gameObject.AddComponent<WingVisuals>();
            var serialized = new SerializedObject(component);
            var joints = serialized.FindProperty("joints");
            joints.arraySize = authored.Count;
            for (var i = 0; i < authored.Count; i++)
            {
                var a = authored[i];
                var joint = joints.GetArrayElementAtIndex(i);
                joint.FindPropertyRelative("bone").objectReferenceValue = a.bone;
                joint.FindPropertyRelative("pivot").vector3Value = a.pivot;
                joint.FindPropertyRelative("translation").vector3Value = a.translation;
                joint.FindPropertyRelative("forwardRotation").quaternionValue = a.rotation;
                joint.FindPropertyRelative("idleFraction").floatValue = a.idle;
                a.bone.localPosition = a.pivot + a.translation * a.idle;
                a.bone.localRotation = Quaternion.Slerp(Quaternion.identity, a.rotation, a.idle);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            return $"Baked {authored.Count} rigid joints, {vertices.Length} vertices, 8 unchanged submeshes. Scale {scale}, center {center}, maximum source error {maxError}.";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
