var path = "Assets/Prefabs/Ships/Nightshade.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
try {
    var so = new UnityEditor.SerializedObject(root.transform);
    UnityEditor.PrefabUtility.RevertPropertyOverride(so.FindProperty("m_ConstrainProportionsScale"), UnityEditor.InteractionMode.AutomatedAction);
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
} finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
UnityEditor.AssetDatabase.Refresh();
return "ok";
