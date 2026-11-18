var path = "Assets/Prefabs/Ships/Nightshade.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
try {
    root.transform.Find("Mesh").localRotation = root.transform.Find("ShipBaseRig/Hull/Nightshade").localRotation;
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
} finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
return "ok";
