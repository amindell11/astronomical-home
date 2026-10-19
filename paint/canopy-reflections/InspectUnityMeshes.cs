using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class InspectUnityMeshes
{
    [Serializable] public class MeshDump
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector2[] uv;
        public int[] triangles;
        public BoneWeight[] boneWeights;
    }
    public static string Main()
    {
        var lines = new System.Text.StringBuilder();
        foreach (var ship in new[] { "Crimson", "Nightshade", "Valis", "Vanguard" })
        {
            var obj = GameObject.Find(ship);
            var renderer = obj.GetComponentsInChildren<Renderer>().First(r => ship == "Crimson" ? r.name == "Hull" : ship == "Valis" ? r.name == "Valis" : r.name == "Canopy");
            var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
            int submesh = ship == "Valis" ? 5 : 0;
            var triangles = mesh.GetTriangles(submesh);
            var vertices = mesh.vertices;
            var bounds = new Bounds(vertices[triangles[0]], Vector3.zero);
            foreach (int index in triangles) bounds.Encapsulate(vertices[index]);
            var dump = new MeshDump { vertices = vertices, normals = mesh.normals, uv = mesh.uv, triangles = triangles, boneWeights = mesh.boneWeights };
            File.WriteAllText("D:/amind/git/agent-4/scratch/canopy-reflections/" + ship + "-unity.json", JsonUtility.ToJson(dump));
            lines.AppendLine(ship + " center=" + bounds.center.ToString("F8") + " size=" + bounds.size.ToString("F8") + " triangles=" + triangles.Length / 3);
            lines.AppendLine("  matrix=" + renderer.transform.localToWorldMatrix);
            if (ship == "Valis") lines.AppendLine("  canopy bones=" + string.Join(",", triangles.Select(v => mesh.boneWeights[v].boneIndex0).Distinct()));
        }
        return lines.ToString();
    }
}
