var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Visuals/Ships/Nightshade/Nightshade.fbx");
var before = importer.useFileScale;
importer.useFileScale = false;
importer.SaveAndReimport();
var names = new System.Collections.Generic.List<string>();
foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Visuals/Ships/Nightshade/Nightshade.fbx")) { var m = o as UnityEngine.Mesh; if (m) names.Add(m.name + " ext " + m.bounds.extents.ToString("F4")); }
return "useFileScale " + before + " -> " + importer.useFileScale + " | " + string.Join("; ", names);
