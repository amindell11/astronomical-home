using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class VerifyReimport
{
    private const string Work = "D:/amind/git/astronomical-home/results/valis-wing-motion/v02/";
    private static Vector3 V(JToken t) => new Vector3((float)t["x"], (float)t["y"], (float)t["z"]);
    public static string Main()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Ships/Valis.prefab");
        root.transform.localScale = Vector3.one;
        var baked = new Mesh();
        try
        {
            var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer.bones.Length != 5 || renderer.sharedMaterials.Length != 8) throw new Exception("Unexpected rig/material layout.");
            foreach (var bone in renderer.bones.Skip(1)) bone.localRotation = Quaternion.identity;
            renderer.BakeMesh(baked, false);
            var actual = baked.vertices;
            var paint = renderer.sharedMesh.uv;
            var weights = renderer.sharedMesh.boneWeights;
            var report = JObject.Parse(File.ReadAllText(Work + "unity-build.json"));
            var scale = (float)report["scale"];
            var center = V(report["center"]);
            var parts = JObject.Parse(File.ReadAllText(Work + "approved-export.json"))["parts"];
            var offset = 0;
            var max = 0f;
            foreach (var part in parts)
            {
                var points = part["vertices"].Select(V).ToArray();
                var group = (string)part["group"];
                for (var contour = 0; contour < 2; contour++)
                    for (var i = 0; i < points.Length; i++)
                    {
                        var index = offset + i + contour * points.Length;
                        var error = Vector3.Distance(actual[index], (points[i] - center) * scale);
                        max = Mathf.Max(max, error);
                        if (error > .00002f) throw new Exception($"Source position mismatch: {part["name"]} {i}: {error}");
                        var expectedUV = new Vector2((float)part["uv"][i]["x"], (float)part["uv"][i]["y"]);
                        if (Vector2.Distance(paint[index], expectedUV) > .000001f) throw new Exception("Source UV mismatch.");
                        var expectedBone = group == null ? 0 : (group == "Upper wings" ? 1 : 3) + (points[i].x < 0f ? 1 : 0);
                        if (weights[index].boneIndex0 != expectedBone || weights[index].weight0 != 1f) throw new Exception("Incomplete rigid wing grouping.");
                    }
                offset += points.Length * 2;
            }
            if (offset != actual.Length) throw new Exception("Unexpected extra geometry.");
            var result = $"All {actual.Length} surface/contour vertices and UVs match the approved swept Blender source; maximum position error {max}. All four complete wings have rigid weights, and the fixed body uses its root bone. Eight existing material sections retained.";
            File.WriteAllText(Work + "vertex-proof.txt", result);
            return result;
        }
        finally { UnityEngine.Object.DestroyImmediate(baked); PrefabUtility.UnloadPrefabContents(root); }
    }
}
