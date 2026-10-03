var folder="Assets/Visuals/Environment/Asteroids/DrawnStudy/";
var output="D:/amind/git/agent-4/results/asteroid-painted-turntable";
System.IO.Directory.CreateDirectory(output+"/before");System.IO.Directory.CreateDirectory(output+"/after");
var root=new UnityEngine.GameObject("Asteroid paint turntable");
var resources=new System.Collections.Generic.List<UnityEngine.Object>();
var type=System.Type.GetType("Tests.PlayMode.Scenarios.Drawn.DrawnExplorationScenario, Tests.PlayMode");var scenario=System.Activator.CreateInstance(type);var baseType=type.BaseType;
var owned=(System.Collections.Generic.List<UnityEngine.Object>)baseType.GetField("owned",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(scenario);
var settings=UnityEditor.AssetDatabase.LoadAssetAtPath<Asteroids.Spawning.AsteroidSpawnSettings>("Assets/Settings/Asteroids/SpawnSettings.asset");
var rocks=new UnityEngine.GameObject[2];
for(var k=0;k<2;k++) {
var rock=new UnityEngine.GameObject(k==0?"Before":"After",typeof(UnityEngine.MeshFilter),typeof(UnityEngine.MeshRenderer));rocks[k]=rock;rock.transform.SetParent(root.transform,false);rock.layer=31;
var mesh=k==0?settings.meshInfos[0].mesh:UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(folder+"AsteroidPaintStudy.fbx").GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
rock.GetComponent<UnityEngine.MeshFilter>().sharedMesh=mesh;
if(k==0){rock.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial=settings.asteroidPrefab.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial;baseType.GetMethod("ApplyTreatment",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(scenario,new object[]{rock.GetComponent<UnityEngine.MeshRenderer>(),true});}
else {rock.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(folder+"AsteroidPaint.mat");baseType.GetMethod("AddContour",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(scenario,new object[]{rock.GetComponent<UnityEngine.MeshRenderer>()});}
rock.SetActive(false);
}
var light=new UnityEngine.GameObject("Fixed key",typeof(UnityEngine.Light)).GetComponent<UnityEngine.Light>();light.transform.SetParent(root.transform,false);light.type=UnityEngine.LightType.Directional;light.intensity=1.2f;light.transform.rotation=UnityEngine.Quaternion.Euler(35,-35,0);light.cullingMask=1<<31;
var camera=new UnityEngine.GameObject("Turntable camera",typeof(UnityEngine.Camera)).GetComponent<UnityEngine.Camera>();camera.transform.SetParent(root.transform,false);camera.transform.position=new UnityEngine.Vector3(0,0,6);camera.transform.LookAt(UnityEngine.Vector3.zero);camera.cullingMask=1<<31;camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;camera.backgroundColor=new UnityEngine.Color(.10f,.13f,.19f,1);camera.allowMSAA=false;camera.allowHDR=false;
var target=new UnityEngine.RenderTexture(768,768,24);resources.Add(target);camera.targetTexture=target;
var pixels=new UnityEngine.Texture2D(768,768,UnityEngine.TextureFormat.RGB24,false);resources.Add(pixels);
var frame=0;UnityEditor.EditorApplication.CallbackFunction tick=null;
System.Action cleanup=()=>{UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.DestroyImmediate(root);foreach(var r in resources)UnityEngine.Object.DestroyImmediate(r);foreach(var r in owned)if(r)UnityEngine.Object.DestroyImmediate(r);};
tick=()=>{
var prior=UnityEngine.RenderTexture.active;
try {
for(var k=0;k<2;k++){var rock=rocks[k];rock.SetActive(true);rock.transform.rotation=UnityEngine.Quaternion.Euler(20,frame*2.5f,12);camera.Render();UnityEngine.RenderTexture.active=target;pixels.ReadPixels(new UnityEngine.Rect(0,0,768,768),0,0);pixels.Apply();System.IO.File.WriteAllBytes(output+(k==0?"/before/":"/after/")+"f_"+frame.ToString("D5")+".png",pixels.EncodeToPNG());rock.SetActive(false);}
frame++;
if(frame==144){foreach(var version in new[]{"before","after"})System.IO.File.WriteAllText(output+"/"+version+"/manifest.json","{\"width\":768,\"height\":768,\"suggestedFps\":24,\"fixedDeltaTime\":0.041666667,\"everyFixedSteps\":1,\"steps\":144}");System.IO.File.WriteAllText(output+"/complete.txt","144 matching poses; 360 degrees; 24 fps; fixed light and camera. No internal screen ink.");cleanup();}
}catch(System.Exception e){System.IO.File.WriteAllText(output+"/error.txt",e.ToString());cleanup();}
finally{UnityEngine.RenderTexture.active=prior;}
};
UnityEditor.EditorApplication.update+=tick;
return output;
