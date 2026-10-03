var pipeline = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
var rendererData = pipeline.rendererDataList[0];
var inkMaterial = new UnityEngine.Material(UnityEngine.Shader.Find("Hidden/Astronomical/Comparison/Screen Ink"));
var feature = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.FullScreenPassRendererFeature>();
feature.name = "Drawn ink probe";
feature.passMaterial = inkMaterial; inkMaterial.SetFloat("_InkPixels",1.8f); inkMaterial.SetFloat("_DepthThreshold",.006f);
feature.injectionPoint = UnityEngine.Rendering.Universal.FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
feature.requirements = UnityEngine.Rendering.Universal.ScriptableRenderPassInput.Depth | UnityEngine.Rendering.Universal.ScriptableRenderPassInput.Normal;
rendererData.rendererFeatures.Add(feature);
rendererData.SetDirty();
try {
var previousScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var locale=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(UnityEditor.AssetDatabase.GUIDToAssetPath("1e74bde8878130343920c6da8b10638f"),UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(locale);
var output="D:/amind/git/agent-4/results/screen-rock-probe";System.IO.Directory.CreateDirectory(output);
var type=System.Type.GetType("Tests.PlayMode.Scenarios.Drawn.DrawnExplorationScenario, Tests.PlayMode");
var scenario=System.Activator.CreateInstance(type);var baseType=type.BaseType;
var owned=(System.Collections.Generic.List<UnityEngine.Object>)baseType.GetField("owned",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(scenario);
var root=new UnityEngine.GameObject("Original rock ink probe");
try {
var settings=UnityEditor.AssetDatabase.LoadAssetAtPath<Asteroids.Spawning.AsteroidSpawnSettings>("Assets/Settings/Asteroids/SpawnSettings.asset");
var rock=new UnityEngine.GameObject("Original asteroid",typeof(UnityEngine.MeshFilter),typeof(UnityEngine.MeshRenderer));rock.transform.SetParent(root.transform,false);rock.layer=31;
rock.GetComponent<UnityEngine.MeshFilter>().sharedMesh=settings.meshInfos[0].mesh;
rock.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial=settings.asteroidPrefab.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial;
rock.transform.rotation=new UnityEngine.Quaternion(.05013389f,.07380714f,.02924529f,-.9955822f);
baseType.GetMethod("ApplyTreatment",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(scenario,new object[]{rock.GetComponent<UnityEngine.MeshRenderer>(),true});
var camera=new UnityEngine.GameObject("Rock camera",typeof(UnityEngine.Camera)).GetComponent<UnityEngine.Camera>();camera.transform.SetParent(root.transform,false);camera.transform.position=new UnityEngine.Vector3(0,0,6);camera.transform.LookAt(UnityEngine.Vector3.zero);camera.cullingMask=1<<31;camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;camera.backgroundColor=new UnityEngine.Color(.2f,.24f,.3f,1);
var target=new UnityEngine.RenderTexture(768,768,24);owned.Add(target);camera.targetTexture=target;
var pixels=new UnityEngine.Texture2D(768,768,UnityEngine.TextureFormat.RGB24,false);owned.Add(pixels);
foreach(var enabled in new[]{false,true}) {inkMaterial.SetFloat("_InkStrength",enabled?1:0);camera.Render();var prior=UnityEngine.RenderTexture.active;UnityEngine.RenderTexture.active=target;pixels.ReadPixels(new UnityEngine.Rect(0,0,768,768),0,0);pixels.Apply();UnityEngine.RenderTexture.active=prior;System.IO.File.WriteAllBytes(output+"/"+(enabled?"on":"off")+".png",pixels.EncodeToPNG());}
return output;
} finally {UnityEngine.Object.DestroyImmediate(root);foreach(var item in owned)if(item)UnityEngine.Object.DestroyImmediate(item);UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);UnityEditor.SceneManagement.EditorSceneManager.CloseScene(locale,true);}
} finally {rendererData.rendererFeatures.Remove(feature);rendererData.SetDirty();feature.Dispose();UnityEngine.Object.DestroyImmediate(feature);UnityEngine.Object.DestroyImmediate(inkMaterial);}
