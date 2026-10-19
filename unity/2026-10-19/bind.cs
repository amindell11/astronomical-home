var path="Assets/Visuals/Ships/Bulwark/";
var importer=(UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path+"Bulwark.fbx");
var model=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path+"Bulwark.fbx");
var names=new System.Collections.Generic.HashSet<string>();
foreach(var r in model.GetComponentsInChildren<UnityEngine.MeshRenderer>())foreach(var m in r.sharedMaterials)names.Add(m.name);
if(!UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path+"Materials/Contour.mat"))UnityEditor.AssetDatabase.CopyAsset("Assets/Visuals/Ships/Valis/Materials/Contour.mat",path+"Materials/Contour.mat");
foreach(var name in names){var m=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path+(name=="Contour"?"Materials/Contour.mat":"Materials/plum-bone/"+name+".mat"));if(!m)throw new System.Exception(name);importer.AddRemap(new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material),name),m);}
importer.SaveAndReimport();
model=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path+"Bulwark.fbx");
var instance=(UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(model);
instance.name="Bulwark Visual";
UnityEditor.PrefabUtility.SaveAsPrefabAsset(instance,path+"Bulwark Visual.prefab");
UnityEngine.Object.DestroyImmediate(instance);
UnityEditor.AssetDatabase.SaveAssets();
return names;
