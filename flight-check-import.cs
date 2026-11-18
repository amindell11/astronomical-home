UnityEditor.AssetDatabase.Refresh();
var log = new System.Text.StringBuilder();
System.Action<string, string> apply = (src, dst) => {
    var s = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(src);
    var d = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(dst);
    var preset = new UnityEditor.Presets.Preset(s);
    var ok = preset.ApplyTo(d);
    UnityEngine.Object.DestroyImmediate(preset);
    log.AppendLine(dst + " preset " + ok);
};
apply("Assets/Visuals/Ships/Bulwark/Bulwark.fbx", "Assets/Visuals/Ships/BulwarkMinion/BulwarkMinion.fbx");
apply("Assets/Visuals/Ships/Nightshade/Nightshade.fbx", "Assets/Visuals/Ships/NightshadeMinion/NightshadeMinion.fbx");
apply("Assets/Visuals/Ships/Bulwark/Bulwark.fbx", "Assets/Visuals/Ships/ValisMinion/ValisMinion.fbx");
var valis = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Visuals/Ships/ValisMinion/ValisMinion.fbx");
valis.globalScale = 0.229f;
foreach (var pair in valis.GetExternalObjectMap()) valis.RemoveRemap(pair.Key);
foreach (var path in new[] { "Assets/Visuals/Ships/BulwarkMinion/BulwarkMinion.fbx", "Assets/Visuals/Ships/NightshadeMinion/NightshadeMinion.fbx", "Assets/Visuals/Ships/ValisMinion/ValisMinion.fbx" }) {
    var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
    importer.SaveAndReimport();
    log.AppendLine(path + " scale " + importer.globalScale + " fileScale " + importer.useFileScale + " weld " + importer.weldVertices + " matMode " + importer.materialImportMode + " remaps " + importer.GetExternalObjectMap().Count);
    var root = (UnityEngine.GameObject)UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
    foreach (UnityEngine.Transform child in root.transform) {
        var mf = child.GetComponent<UnityEngine.MeshFilter>(); var r = child.GetComponent<UnityEngine.Renderer>();
        log.AppendLine("   " + child.name + (mf ? " subs " + mf.sharedMesh.subMeshCount + " ext " + mf.sharedMesh.bounds.extents.ToString("F3") + " c " + mf.sharedMesh.bounds.center.ToString("F3") : "")
            + (r ? " mats [" + string.Join(", ", System.Linq.Enumerable.Select(r.sharedMaterials, m => m ? m.name + "@" + System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(UnityEditor.AssetDatabase.GetAssetPath(m))) : "null")) + "]" : ""));
    }
}
return log.ToString();
