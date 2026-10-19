using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class InspectDebris
{
    public static string Run()
    {
        var rows = new System.Collections.Generic.List<object>();
        foreach (string path in new[] { "Assets/Prefabs/Ships/Nightshade.prefab", "Assets/Visuals/Ships/Nightshade/Breakup/NightshadeBreakup.prefab", "Assets/Visuals/Ships/Valis/Breakup/ValisBreakup.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = filter.sharedMesh;
                    var materials = filter.GetComponent<Renderer>().sharedMaterials;
                    for (int i = 0; i < mesh.subMeshCount; i++)
                    {
                        if (mesh.GetSubMesh(i).indexCount == 0 || !(materials[i].name.Contains("Glass") || materials[i].shader.name.EndsWith("Canopy"))) continue;
                        using var data = MeshUtility.AcquireReadOnlyMeshData(mesh);
                        using var verts = new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount, Unity.Collections.Allocator.Temp);
                        using var indices = new Unity.Collections.NativeArray<int>(data[0].GetSubMesh(i).indexCount, Unity.Collections.Allocator.Temp);
                        data[0].GetVertices(verts); data[0].GetIndices(indices, i);
                        var bounds = new Bounds(verts[indices[0]], Vector3.zero);
                        foreach (int index in indices) bounds.Encapsulate(verts[index]);
                        rows.Add(new { prefab = path, renderer = AnimationUtility.CalculateTransformPath(filter.transform, root.transform), submesh = i, material = AssetDatabase.GetAssetPath(materials[i]), center = bounds.center.ToString("F9"), size = bounds.size.ToString("F9"), position = filter.transform.position.ToString("F9"), rotation = filter.transform.rotation.eulerAngles.ToString("F9"), scale = filter.transform.lossyScale.ToString("F9") });
                    }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(rows, Newtonsoft.Json.Formatting.Indented);
        System.IO.File.WriteAllText("D:/amind/git/agent-4/scratch/canopy-reflections/debris-inspection.json", json);
        return json;
    }
}
