using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Ships.Visuals.Wings;
using UnityEditor;
using UnityEngine;

public static class VerifyValisWings
{
    private static Vector3 V(JToken t) => new Vector3((float)t["x"], (float)t["y"], (float)t["z"]);
    public static string Main()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Ships/Valis.prefab");
        root.transform.localScale = Vector3.one;
        var baked = new Mesh();
        try
        {
            var r = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var authored = new SerializedObject(root.GetComponentInChildren<WingVisuals>(true));
            var joints = authored.FindProperty("joints");
            var parts = (JArray)JObject.Parse(File.ReadAllText("D:/amind/git/astronomical-home/results/valis-wing-motion/rig-export.json"))["parts"];
            var source = parts.SelectMany(p => p["vertices"].Select(V)).ToArray();
            var rest = r.sharedMesh.vertices;
            var scale = rest[1].x / source[1].x;
            var center = source[0] - rest[0] / scale;
            for (var i = 0; i < joints.arraySize; i++)
            {
                var j = joints.GetArrayElementAtIndex(i);
                var bone = (Transform)j.FindPropertyRelative("bone").objectReferenceValue;
                bone.localPosition = j.FindPropertyRelative("pivot").vector3Value + j.FindPropertyRelative("translation").vector3Value;
                bone.localRotation = j.FindPropertyRelative("forwardRotation").quaternionValue;
            }
            r.BakeMesh(baked, false);
            var actual = baked.vertices;
            var max = 0f;
            var offset = 0;
            foreach (var part in parts)
            {
                var posed = part["forward"].Select(V).ToArray();
                for (var i = 0; i < posed.Length; i++)
                    for (var contour = 0; contour < 2; contour++)
                    {
                        var error = Vector3.Distance(actual[offset + i + contour * posed.Length], (posed[i] - center) * scale);
                        max = Mathf.Max(max, error);
                        if (error > .00002f) throw new Exception($"Forward pose mismatch {part["name"]}: {error}, expected {(posed[i] - center) * scale}, actual {actual[offset+i]}, local {r.transform.localScale}, lossy {r.transform.lossyScale}");
                    }
                offset += posed.Length * 2;
            }
            var report = $"All {actual.Length} Unity surface/contour vertices match the user's Blender forward pose, maximum error {max}.";
            File.WriteAllText("D:/amind/git/astronomical-home/results/valis-wing-motion/vertex-proof.txt", report);
            return report;
        }
        finally { UnityEngine.Object.DestroyImmediate(baked); PrefabUtility.UnloadPrefabContents(root); }
    }
}


