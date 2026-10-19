var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var oldFog=UnityEngine.RenderSettings.fog;UnityEngine.RenderSettings.fog=false;var root=new UnityEngine.GameObject("Bulwark texture lineup");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
int index=0;
foreach(var name in new[]{"Crimson","Nightshade","Bulwark","Vanguard","Valis"}){
var path=name=="Bulwark"?"Assets/Visuals/Ships/Bulwark/Bulwark Visual.prefab":"Assets/Prefabs/Ships/"+name+".prefab";
var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
var group=new UnityEngine.GameObject(name);group.transform.SetParent(root.transform);
foreach(var r in asset.GetComponentsInChildren<UnityEngine.Renderer>(true)){
var mf=r.GetComponent<UnityEngine.MeshFilter>();var skin=r as UnityEngine.SkinnedMeshRenderer;if((!mf&&!skin)||r.name.Contains("Minimap"))continue;
var go=new UnityEngine.GameObject(r.name);go.transform.SetParent(group.transform);go.transform.position=r.transform.position;go.transform.rotation=r.transform.rotation;go.transform.localScale=r.transform.lossyScale;
UnityEngine.Mesh mesh=mf?mf.sharedMesh:null;if(skin){var clone=UnityEngine.Object.Instantiate(asset);var sk=clone.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();mesh=new UnityEngine.Mesh();sk.BakeMesh(mesh);UnityEngine.Object.DestroyImmediate(clone);}go.AddComponent<UnityEngine.MeshFilter>().sharedMesh=mesh;go.AddComponent<UnityEngine.MeshRenderer>().sharedMaterials=r.sharedMaterials;}
if(name=="Valis")group.transform.rotation=UnityEngine.Quaternion.Euler(90,0,0);var rs=group.GetComponentsInChildren<UnityEngine.MeshRenderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
foreach(UnityEngine.Transform t in group.transform)t.position-=bounds.center;
group.transform.localScale=UnityEngine.Vector3.one*(3.6f/bounds.size.z);
if(name=="Bulwark")group.transform.rotation=UnityEngine.Quaternion.Euler(0,180,0);
group.transform.position=new UnityEngine.Vector3((index-2)*3.8f,0,0);index++;
}
var lg=new UnityEngine.GameObject("Key");lg.transform.SetParent(root.transform);var light=lg.AddComponent<UnityEngine.Light>();light.type=UnityEngine.LightType.Directional;light.intensity=1f;lg.transform.rotation=UnityEngine.Quaternion.Euler(55,-30,0);
var cg=new UnityEngine.GameObject("Camera");cg.transform.SetParent(root.transform);var cam=cg.AddComponent<UnityEngine.Camera>();cam.scene=scene;cam.orthographic=true;cam.orthographicSize=2.65f;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;cam.backgroundColor=new UnityEngine.Color(.035f,.045f,.065f);cam.nearClipPlane=.1f;cam.farClipPlane=100;
cg.transform.position=new UnityEngine.Vector3(0,20,0);cg.transform.rotation=UnityEngine.Quaternion.Euler(90,0,0);
var rt=new UnityEngine.RenderTexture(2400,640,24);cam.targetTexture=rt;cam.Render();UnityEngine.RenderTexture.active=rt;var tex=new UnityEngine.Texture2D(2400,640,UnityEngine.TextureFormat.RGB24,false);tex.ReadPixels(new UnityEngine.Rect(0,0,2400,640),0,0);tex.Apply();System.IO.File.WriteAllBytes("D:/amind/git/agent-3/results/bulwark/unity/2026-10-19/lineup-top.png",UnityEngine.ImageConversion.EncodeToPNG(tex));UnityEngine.RenderTexture.active=null;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);UnityEngine.RenderSettings.fog=oldFog;return "captured five current Unity visuals at equal display length";



