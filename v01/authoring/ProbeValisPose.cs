using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
public static class ProbeValisPose
{
    public static string Main()
    {
        var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/Valis.prefab"));
        var debris = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Visuals/Ships/Valis/Breakup/ValisBreakup.prefab"));
        var baked = new Mesh();
        try
        {
            var hull = source.GetComponentInChildren<SkinnedMeshRenderer>();
            debris.transform.SetPositionAndRotation(hull.transform.position, hull.transform.rotation);
            debris.transform.localScale = hull.transform.lossyScale;
            hull.BakeMesh(baked, false);
            var geometry = debris.GetComponentsInChildren<MeshFilter>().SelectMany(m => m.sharedMesh.vertices.Select(m.transform.TransformPoint)).ToArray();
            var bakedVertices = baked.vertices;
            var rawVertices = hull.sharedMesh.vertices;
            var boneWeights = hull.sharedMesh.boneWeights;
            var boneMatrices = hull.bones.Select((b, i) => b.localToWorldMatrix * hull.sharedMesh.bindposes[i]).ToArray();
            var result = Enumerable.Range(0, 1).Select(i => new { i, weight = boneWeights[i].boneIndex0, raw = rawVertices[i].ToString("F7"), boneWorld = boneMatrices[boneWeights[i].boneIndex0].MultiplyPoint3x4(rawVertices[i]).ToString("F7"), baked = bakedVertices[i].ToString("F7"), world = hull.transform.TransformPoint(bakedVertices[i]).ToString("F7"), nearest = geometry.OrderBy(p => Vector3.Distance(p, hull.transform.TransformPoint(bakedVertices[i]))).First().ToString("F7"), distance = geometry.Min(p => Vector3.Distance(p, hull.transform.TransformPoint(bakedVertices[i]))) }).ToArray();
            var second = new Mesh();
            hull.BakeMesh(second, true);
            var scaleProbe = new { bakedNoScale = baked.vertices[0].ToString("F7"), bakedWithScale = second.vertices[0].ToString("F7"), raw = hull.sharedMesh.vertices[0].ToString("F7"), lossyScale = hull.transform.lossyScale.ToString("F7") };
            Object.DestroyImmediate(second);
            var output = JsonConvert.SerializeObject(new { result, scaleProbe });
            System.IO.File.WriteAllText("D:/amind/git/agent-1/scratch/valis-breakup/pose-probe.json", output);
            return output;
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(debris); Object.DestroyImmediate(baked); }
    }
}



