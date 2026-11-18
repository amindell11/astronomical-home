var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<Ships.Loadout.ItemCatalog>(Ships.Loadout.ItemCatalog.AssetPath);
var so = new UnityEditor.SerializedObject(catalog);
var chassis = so.FindProperty("chassis");
var log = new System.Text.StringBuilder();
foreach (var name in new[] { "ValisMinion", "BulwarkMinion", "NightshadeMinion" }) {
    var ship = UnityEditor.AssetDatabase.LoadAssetAtPath<Ships.Ship>("Assets/Prefabs/Ships/" + name + ".prefab");
    if (!ship) throw new System.InvalidOperationException("no ship " + name);
    var present = false;
    for (var i = 0; i < chassis.arraySize; i++) if (chassis.GetArrayElementAtIndex(i).objectReferenceValue == ship) present = true;
    if (present) continue;
    chassis.arraySize++;
    chassis.GetArrayElementAtIndex(chassis.arraySize - 1).objectReferenceValue = ship;
    log.Append("added " + name + "; ");
}
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(catalog);
var valis = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Visuals/Ships/ValisMinion/ValisMinion.fbx");
foreach (var n in new[] { "Graphite", "Lavender", "Ivory", "Cool gray", "Engine", "Edge" })
    valis.AddRemap(new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), n), UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Visuals/Ships/Valis/Materials/jade-iris/" + n + ".mat"));
valis.AddRemap(new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), "Canopy"), UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Visuals/Ships/Valis/Canopy/jade-iris.mat"));
valis.AddRemap(new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), "Contour"), UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Visuals/Ships/Valis/Materials/Contour.mat"));
valis.SaveAndReimport();
UnityEditor.AssetDatabase.SaveAssets();
log.Append("chassis " + chassis.arraySize + "; valis remaps " + valis.GetExternalObjectMap().Count);
return log.ToString();
