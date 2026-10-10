using System.Linq;
using UnityEngine;
using UnityEditor;
public static class SootProbe
{
    public static object Main() => new[] {"Crimson", "Nightshade"}.Select(ship => new {
        ship,
        meshes = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Visuals/Ships/" + ship + "/Breakup/" + ship + "Breakup.prefab").GetComponentsInChildren<MeshFilter>(true).Select(f => new {
            f.name,
            vertices = f.sharedMesh.vertexCount,
            colors = f.sharedMesh.colors.Length,
            redMin = f.sharedMesh.colors.Length == 0 ? -1f : f.sharedMesh.colors.Min(c => c.r),
            redMax = f.sharedMesh.colors.Length == 0 ? -1f : f.sharedMesh.colors.Max(c => c.r),
            redMean = f.sharedMesh.colors.Length == 0 ? -1f : f.sharedMesh.colors.Average(c => c.r)
        }).ToArray()
    }).ToArray();
}