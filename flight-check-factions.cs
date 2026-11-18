var log = new System.Text.StringBuilder();
foreach (var pair in new[] { ("Shoal", "Valis"), ("Bastion", "Bulwark"), ("Veil", "Nightshade") }) {
    var faction = UnityEditor.AssetDatabase.LoadAssetAtPath<Substrate.Sectors.Factions.Faction>("Assets/Settings/Factions/" + pair.Item1 + ".asset");
    var basePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<Ships.Ship>("Assets/Prefabs/Ships/" + pair.Item2 + ".prefab");
    var minionPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<Ships.Ship>("Assets/Prefabs/Ships/" + pair.Item2 + "Minion.prefab");
    if (faction.signature.ship != basePrefab) throw new System.InvalidOperationException(pair.Item1 + " signature is not " + pair.Item2);
    System.Func<UnityEngine.Renderer, string> role = r => { var s = r as UnityEngine.SkinnedMeshRenderer; return (s ? s.sharedMesh : r.GetComponent<UnityEngine.MeshFilter>().sharedMesh).name; };
    var baseSlots = new System.Collections.Generic.List<(string role, UnityEngine.Material authored, UnityEngine.Material worn)>();
    var index = 0;
    foreach (var r in basePrefab.VisualRig.Silhouette) foreach (var m in r.sharedMaterials) baseSlots.Add((role(r), m, faction.signature.hull[index++]));
    if (index != faction.signature.hull.Length) throw new System.InvalidOperationException(pair.Item1 + " hull has " + faction.signature.hull.Length + " slots, the base silhouette " + index);
    var worn = new System.Collections.Generic.List<UnityEngine.Material>();
    foreach (var r in minionPrefab.VisualRig.Silhouette) foreach (var m in r.sharedMaterials) {
        var sameRole = baseSlots.FindIndex(s => s.authored == m && s.role == role(r));
        var any = sameRole >= 0 ? sameRole : baseSlots.FindIndex(s => s.authored == m);
        if (any < 0) throw new System.InvalidOperationException(pair.Item2 + "Minion slot " + m.name + " has no base slot");
        worn.Add(baseSlots[any].worn);
    }
    var so = new UnityEditor.SerializedObject(faction);
    so.FindProperty("minion.ship").objectReferenceValue = minionPrefab;
    var hull = so.FindProperty("minion.hull");
    hull.arraySize = worn.Count;
    for (var i = 0; i < worn.Count; i++) hull.GetArrayElementAtIndex(i).objectReferenceValue = worn[i];
    so.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.EditorUtility.SetDirty(faction);
    log.AppendLine(pair.Item1 + ": " + minionPrefab.name + " hull " + worn.Count + " [" + string.Join(", ", System.Linq.Enumerable.Select(worn, m => System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(UnityEditor.AssetDatabase.GetAssetPath(m))) + "/" + m.name)) + "]");
}
UnityEditor.AssetDatabase.SaveAssets();
return log.ToString();
