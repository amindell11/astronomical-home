var baseName = "__BASE__";
var name = baseName + "Minion";
var basePath = "Assets/Prefabs/Ships/" + baseName + ".prefab";
var path = "Assets/Prefabs/Ships/" + name + ".prefab";
var fbx = "Assets/Visuals/Ships/" + name + "/" + name + ".fbx";
var log = new System.Text.StringBuilder();
if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(path) == null) {
    var instance = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(UnityEditor.AssetDatabase.LoadMainAssetAtPath(basePath));
    instance.name = name;
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(instance, path);
    UnityEngine.Object.DestroyImmediate(instance);
    log.Append("created; ");
}
var meshes = new System.Collections.Generic.Dictionary<string, UnityEngine.Mesh>();
foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(fbx)) { var m = o as UnityEngine.Mesh; if (m) meshes[m.name] = m; }
var model = (UnityEngine.GameObject)UnityEditor.AssetDatabase.LoadMainAssetAtPath(fbx);
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
try {
    foreach (var filter in root.transform.Find("ShipBaseRig/Hull").GetComponentsInChildren<UnityEngine.MeshFilter>(true)) {
        var role = filter.sharedMesh.name;
        filter.sharedMesh = meshes[role];
        var renderer = filter.GetComponent<UnityEngine.MeshRenderer>();
        if (renderer.sharedMaterials.Length != meshes[role].subMeshCount) {
            renderer.sharedMaterials = model.transform.Find(role).GetComponent<UnityEngine.Renderer>().sharedMaterials;
            log.Append(role + " materials -> " + renderer.sharedMaterials.Length + "; ");
        }
        log.Append(filter.name + "=" + role + "; ");
    }
    root.transform.Find("Mesh").GetComponent<UnityEngine.MeshCollider>().sharedMesh = meshes["Collider"];
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
} finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
var saved = (UnityEngine.GameObject)UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
log.Append("type " + UnityEditor.PrefabUtility.GetPrefabAssetType(saved) + " source " + UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(saved)) + " name " + saved.name);
return log.ToString();
