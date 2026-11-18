var path = "Assets/Prefabs/Ships/Nightshade.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
var log = new System.Text.StringBuilder();
try {
    var auto = UnityEditor.InteractionMode.AutomatedAction;
    foreach (UnityEngine.Transform child in root.transform.Find("ShipBaseRig/Hull")) { child.localScale = UnityEngine.Vector3.one; log.Append("scale1 " + child.name + "; "); }
    var mesh = root.transform.Find("Mesh");
    UnityEditor.PrefabUtility.RevertObjectOverride(mesh, auto);
    UnityEditor.PrefabUtility.RevertObjectOverride(mesh.gameObject, auto);
    var weapons = new UnityEditor.SerializedObject(root.GetComponent<Ships.Weapons.WeaponsController>());
    UnityEditor.PrefabUtility.RevertPropertyOverride(weapons.FindProperty("secondaryMount"), auto);
    var rig = root.transform.Find("ShipBaseRig");
    UnityEditor.PrefabUtility.RevertObjectOverride(rig.Find("MinimapMarker").GetComponent<UnityEngine.MeshRenderer>(), auto);
    UnityEditor.PrefabUtility.RevertObjectOverride(rig.GetComponent<Ships.Visuals.HullDamageEffects>(), auto);
    UnityEngine.Object debris = null;
    foreach (var added in UnityEditor.PrefabUtility.GetAddedComponents(root)) {
        var so = new UnityEditor.SerializedObject(added.instanceComponent);
        debris = so.FindProperty("debrisPrefab").objectReferenceValue;
        log.Append("added " + added.instanceComponent.GetType().Name + " hull=" + so.FindProperty("hull").objectReferenceValue.name + "; ");
        added.Revert(auto);
    }
    foreach (var removed in UnityEditor.PrefabUtility.GetRemovedComponents(root)) { log.Append("restore " + removed.assetComponent.GetType().Name + "; "); removed.Revert(auto); }
    var breakup = new UnityEditor.SerializedObject(rig.GetComponent<Ships.Visuals.Breakup.ShipBreakupVisual>());
    log.Append("base hull=" + breakup.FindProperty("hull").objectReferenceValue.name + "; ");
    breakup.FindProperty("debrisPrefab").objectReferenceValue = debris;
    breakup.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
} finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
return log.ToString();
