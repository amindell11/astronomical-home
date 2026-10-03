var previousScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var locale=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(UnityEditor.AssetDatabase.GUIDToAssetPath("1e74bde8878130343920c6da8b10638f"),UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(locale);
try {
var output="D:/amind/git/agent-4/results/drawn-texture-isolation"; System.IO.Directory.CreateDirectory(output);
foreach(var treatment in new[]{2,3}) {
var type=System.Type.GetType("Tests.PlayMode.Scenarios.Drawn.Drawn"+(treatment==2?"Contour":"Exploration")+"Scenario, Tests.PlayMode");
var scenario=System.Activator.CreateInstance(type); var baseType=type.BaseType; var apply=baseType.GetMethod("ApplyTreatment",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
var owned=(System.Collections.Generic.List<UnityEngine.Object>)baseType.GetField("owned",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(scenario);
var root=new UnityEngine.GameObject("Look probe");
try {
var ship=UnityEditor.AssetDatabase.LoadAssetAtPath<Ships.Ship>("Assets/Prefabs/Ships/Ship_1.prefab");
var stage=UI.HangarPreviewStage.Create(false,root.transform); stage.Show(new Ships.Loadout.ShipLoadout(ship,ship.Engine,ship.Shield,null,null));
var rig=stage.GetComponentInChildren<Ships.Presentation.ShipVisualRig>(); rig.transform.localScale=ship.GetComponentInChildren<Ships.Presentation.ShipVisualRig>(true).transform.lossyScale;
var anchor=stage.transform.Find("Anchor"); anchor.rotation=UnityEngine.Quaternion.Euler(0,240,0);
foreach(var renderer in stage.GetComponentsInChildren<UnityEngine.MeshRenderer>()) apply.Invoke(scenario,new object[]{renderer,false});
foreach(var r in stage.GetComponentsInChildren<UnityEngine.MeshRenderer>()) {var m=r.sharedMaterial;if(m.shader.name=="Astronomical/Comparison/Drawn Surface"){m.SetFloat("_TextureStrength",0);m.SetFloat("_PigmentPreservation",0);m.SetFloat("_LineStrength",0);m.SetFloat("_SpecularStrength",0);}}
var camera=stage.GetComponentInChildren<UnityEngine.Camera>(); camera.backgroundColor=new UnityEngine.Color(.025f,.035f,.06f,1);
var pixels=new UnityEngine.Texture2D(768,768,UnityEngine.TextureFormat.RGB24,false); owned.Add(pixels);
camera.Render(); var prior=UnityEngine.RenderTexture.active; UnityEngine.RenderTexture.active=(UnityEngine.RenderTexture)stage.Texture; pixels.ReadPixels(new UnityEngine.Rect(0,0,768,768),0,0); pixels.Apply(); UnityEngine.RenderTexture.active=prior; System.IO.File.WriteAllBytes(output+"/ship-"+treatment+".png",pixels.EncodeToPNG());
} finally { UnityEngine.Object.DestroyImmediate(root); foreach(var item in owned) if(item) UnityEngine.Object.DestroyImmediate(item); }
}
return output;

} finally {UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);UnityEditor.SceneManagement.EditorSceneManager.CloseScene(locale,true);}
