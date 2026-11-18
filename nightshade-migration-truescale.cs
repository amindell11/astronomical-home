var log = new System.Text.StringBuilder();
var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Visuals/Ships/Nightshade/Canopy/Canopy.mat");
var b = mat.GetVector("_ReflectionBounds");
var nb = new UnityEngine.Vector4(b.x * 100f, b.y * 100f, b.z / 100f, b.w / 100f);
mat.SetVector("_ReflectionBounds", nb);
UnityEditor.EditorUtility.SetDirty(mat);
log.Append("bounds " + b.ToString("F6") + " -> " + nb.ToString("F6") + "; ");
var fbx = "Assets/Visuals/Ships/Nightshade/Breakup/NightshadeBreakup.fbx";
var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(fbx);
log.Append("breakup useFileScale " + importer.useFileScale + " globalScale " + importer.globalScale + "; ");
importer.useFileScale = false;
importer.SaveAndReimport();
var path = "Assets/Visuals/Ships/Nightshade/Breakup/NightshadeBreakup.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
try {
    var n = 0;
    foreach (var t in root.GetComponentsInChildren<UnityEngine.Transform>(true))
        if (t.name == "Surface") { if (t.localScale != UnityEngine.Vector3.one * 100f) throw new System.InvalidOperationException(t.parent.name + " Surface scale " + t.localScale); t.localScale = UnityEngine.Vector3.one; n++; }
    log.Append("surfaces " + n + "; ");
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
} finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
UnityEditor.AssetDatabase.SaveAssets();
return log.ToString();
