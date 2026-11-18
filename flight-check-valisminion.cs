var path = "Assets/Prefabs/Ships/ValisMinion.prefab";
var fbx = "Assets/Visuals/Ships/ValisMinion/ValisMinion.fbx";
var forward = 0.076f;
var log = new System.Text.StringBuilder();
var valis = (UnityEngine.GameObject)UnityEditor.AssetDatabase.LoadMainAssetAtPath("Assets/Prefabs/Ships/Valis.prefab");
var bulwarkHull = ((UnityEngine.GameObject)UnityEditor.AssetDatabase.LoadMainAssetAtPath("Assets/Prefabs/Ships/Bulwark.prefab")).transform.Find("ShipBaseRig/Hull/Body/Hull");
if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(path) != null) throw new System.InvalidOperationException(path + " exists");
var instance = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(UnityEditor.AssetDatabase.LoadMainAssetAtPath("Assets/Prefabs/Ships/ShipBase.prefab"));
instance.name = "ValisMinion";
var mods = new System.Collections.Generic.List<UnityEditor.PropertyModification>(UnityEditor.PrefabUtility.GetPropertyModifications(instance));
var copied = 0;
foreach (var mod in UnityEditor.PrefabUtility.GetPropertyModifications(valis)) {
    if (UnityEditor.PrefabUtility.IsDefaultOverride(mod)) continue;
    var p = mod.propertyPath;
    if (p.StartsWith("silhouette") || p == "hull" || p.StartsWith("poseSources") || p == "m_Mesh") continue;
    mods.Add(mod); copied++;
}
UnityEditor.PrefabUtility.SetPropertyModifications(instance, mods.ToArray());
log.Append("copied " + copied + " overrides; ");
UnityEditor.PrefabUtility.SaveAsPrefabAsset(instance, path);
UnityEngine.Object.DestroyImmediate(instance);

var meshes = new System.Collections.Generic.Dictionary<string, UnityEngine.Mesh>();
foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(fbx)) { var m = o as UnityEngine.Mesh; if (m) meshes[m.name] = m; }
var model = (UnityEngine.GameObject)UnityEditor.AssetDatabase.LoadMainAssetAtPath(fbx);
System.Func<string, UnityEngine.Material> paint = n => {
    var file = n == "Canopy" ? "Assets/Visuals/Ships/Valis/Canopy/jade-iris.mat" : n == "Contour" ? "Assets/Visuals/Ships/Valis/Materials/Contour.mat" : "Assets/Visuals/Ships/Valis/Materials/jade-iris/" + n + ".mat";
    var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(file);
    if (!material) throw new System.InvalidOperationException("no material " + file);
    return material;
};
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
try {
    var slot = root.transform.Find("ShipBaseRig/Hull");
    var body = new UnityEngine.GameObject("Body").transform;
    body.SetParent(slot, false);
    body.localPosition = new UnityEngine.Vector3(0, -forward, 0);
    var hull = new UnityEngine.GameObject("Hull", typeof(UnityEngine.MeshFilter), typeof(UnityEngine.MeshRenderer));
    hull.layer = bulwarkHull.gameObject.layer; hull.tag = bulwarkHull.tag;
    hull.transform.SetParent(body, false);
    hull.transform.localPosition = new UnityEngine.Vector3(0, forward, 0);
    hull.transform.localRotation = bulwarkHull.localRotation;
    hull.GetComponent<UnityEngine.MeshFilter>().sharedMesh = meshes["Hull"];
    var renderer = hull.GetComponent<UnityEngine.MeshRenderer>();
    UnityEditor.EditorUtility.CopySerialized(bulwarkHull.GetComponent<UnityEngine.MeshRenderer>(), renderer);
    renderer.sharedMaterials = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(model.transform.Find("Hull").GetComponent<UnityEngine.Renderer>().sharedMaterials, m => paint(m.name)));
    var poses = new UnityEngine.Transform[4];
    var names = new[] { "Upper wing right pose", "Upper wing left pose", "Lower wing right pose", "Lower wing left pose" };
    for (var i = 0; i < 4; i++) { poses[i] = new UnityEngine.GameObject(names[i]).transform; poses[i].SetParent(body, false); }
    foreach (var socket in new[] { "Hardpoints/Centre", "Hardpoints/Left", "Hardpoints/Right", "ShipBaseRig/Thruster/EngineExhaust" }) {
        var t = root.transform.Find(socket);
        t.position = root.transform.TransformPoint(root.transform.InverseTransformPoint(t.position) - new UnityEngine.Vector3(0, forward, 0));
    }
    var collider = root.transform.Find("Mesh");
    collider.GetComponent<UnityEngine.MeshCollider>().sharedMesh = meshes["Collider"];
    collider.localRotation = bulwarkHull.localRotation;
    var rig = root.transform.Find("ShipBaseRig");
    var visual = new UnityEditor.SerializedObject(rig.GetComponent<Ships.Presentation.ShipVisualRig>());
    var silhouette = visual.FindProperty("silhouette"); silhouette.arraySize = 1; silhouette.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
    visual.ApplyModifiedPropertiesWithoutUndo();
    var breakup = new UnityEditor.SerializedObject(rig.GetComponent<Ships.Visuals.Breakup.ShipBreakupVisual>());
    breakup.FindProperty("hull").objectReferenceValue = body;
    var sources = breakup.FindProperty("poseSources"); sources.arraySize = 4;
    for (var i = 0; i < 4; i++) sources.GetArrayElementAtIndex(i).objectReferenceValue = poses[i];
    breakup.ApplyModifiedPropertiesWithoutUndo();
    log.Append("hull layer " + hull.layer + " tag " + hull.tag + " rot " + hull.transform.localEulerAngles + " mats " + string.Join(",", System.Linq.Enumerable.Select(renderer.sharedMaterials, m => m.name)) + "; ");
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
} finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
return log.ToString();
