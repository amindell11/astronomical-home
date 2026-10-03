using UnityEditor;
using UnityEngine;
using System.Linq;
public static class InspectSkin
{
    public static string Main()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Ships/Valis.prefab");
        var r = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
        var mesh = new Mesh();
        r.BakeMesh(mesh, false);
        var result = $"source={r.sharedMesh.vertexCount}, baked={mesh.vertexCount}, weights={r.sharedMesh.boneWeights.Length}, bindposes={r.sharedMesh.bindposes.Length}, bones={r.bones.Length}, materials={r.sharedMaterials.Length}, active={r.gameObject.activeInHierarchy}, enabled={r.enabled}, root={r.rootBone}, bone names={string.Join(",",r.bones.Select(b=>b ? b.name : "MISSING"))}";
        Object.DestroyImmediate(mesh);
        PrefabUtility.UnloadPrefabContents(root);
        return result;
    }
}
