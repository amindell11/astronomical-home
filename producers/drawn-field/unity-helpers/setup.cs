var path = "Assets/Scripts/Editor/Tests/PlayMode/Scenarios/Drawn/DrawnComparisonSector.prefab";
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
var root = new UnityEngine.GameObject("DrawnComparisonSector");
root.AddComponent<Substrate.Sectors.Sector>();
UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
UnityEngine.Object.DestroyImmediate(root);
UnityEditor.AssetDatabase.Refresh();
return path;
