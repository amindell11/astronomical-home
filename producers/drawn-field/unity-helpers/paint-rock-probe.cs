UnityEditor.AssetDatabase.Refresh();
var folder="Assets/Visuals/Environment/Asteroids/DrawnStudy/";
var model=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(folder+"AsteroidPaintStudy.fbx");
var mesh=model.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh;
var output="D:/amind/git/agent-4/results/paint-rock-probe";System.IO.Directory.CreateDirectory(output);
var root=new UnityEngine.GameObject("Paint rock probe");
var resources=new System.Collections.Generic.List<UnityEngine.Object>();
var priorTarget=UnityEngine.RenderTexture.active;
try {
var rock=new UnityEngine.GameObject("Painted asteroid",typeof(UnityEngine.MeshFilter),typeof(UnityEngine.MeshRenderer));rock.transform.SetParent(root.transform,false);rock.layer=31;
rock.GetComponent<UnityEngine.MeshFilter>().sharedMesh=mesh;
var material=new UnityEngine.Material(UnityEngine.Shader.Find("Astronomical/Comparison/Drawn Surface"));resources.Add(material);
material.SetTexture("_BaseMap",UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(folder+"AsteroidPaint.png"));
material.SetFloat("_TextureStrength",1);material.SetFloat("_LineStrength",0);material.SetFloat("_SpecularStrength",0);material.SetFloat("_EmissionStrength",0);material.SetFloat("_PaletteLighting",1);material.SetFloat("_AmbientStrength",.35f);material.SetColor("_ShadowColor",new UnityEngine.Color(.48f,.46f,.62f));material.SetFloat("_ShadowThreshold",-.1f);material.SetFloat("_ShadowSoftness",.3f);
material.SetColor("_BaseColor",new UnityEngine.Color(1.3f,1.3f,1.3f));
rock.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial=material;
var shell=new UnityEngine.GameObject("Outer ink",typeof(UnityEngine.MeshFilter),typeof(UnityEngine.MeshRenderer));shell.layer=31;shell.transform.SetParent(rock.transform,false);shell.GetComponent<UnityEngine.MeshFilter>().sharedMesh=mesh;
var ink=new UnityEngine.Material(UnityEngine.Shader.Find("Astronomical/Comparison/Drawn Contour"));resources.Add(ink);ink.SetFloat("_ContourPixels",4.5f);ink.SetFloat("_ContourMinimum",.7f);ink.SetColor("_ContourColor",new UnityEngine.Color(.018f,.015f,.03f));shell.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial=ink;shell.GetComponent<UnityEngine.MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
var light=new UnityEngine.GameObject("Rock key",typeof(UnityEngine.Light)).GetComponent<UnityEngine.Light>();light.transform.SetParent(root.transform,false);light.type=UnityEngine.LightType.Directional;light.intensity=1.2f;light.transform.rotation=UnityEngine.Quaternion.Euler(35,-35,0);light.cullingMask=1<<31;
var camera=new UnityEngine.GameObject("Rock camera",typeof(UnityEngine.Camera)).GetComponent<UnityEngine.Camera>();camera.transform.SetParent(root.transform,false);camera.transform.position=new UnityEngine.Vector3(0,0,6);camera.transform.LookAt(UnityEngine.Vector3.zero);camera.cullingMask=1<<31;camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;camera.backgroundColor=new UnityEngine.Color(.035f,.045f,.075f,1);camera.allowMSAA=false;camera.allowHDR=false;
var target=new UnityEngine.RenderTexture(768,768,24);resources.Add(target);camera.targetTexture=target;
var pixels=new UnityEngine.Texture2D(768,768,UnityEngine.TextureFormat.RGB24,false);resources.Add(pixels);
for(var i=0;i<8;i++) {rock.transform.rotation=UnityEngine.Quaternion.Euler(20,i*45,12);camera.Render();UnityEngine.RenderTexture.active=target;pixels.ReadPixels(new UnityEngine.Rect(0,0,768,768),0,0);pixels.Apply();System.IO.File.WriteAllBytes(output+"/pose-"+i+".png",pixels.EncodeToPNG());}
return new{output,vertices=mesh.vertexCount,bounds=mesh.bounds.size};
}finally{UnityEngine.RenderTexture.active=priorTarget;UnityEngine.Object.DestroyImmediate(root);foreach(var resource in resources)UnityEngine.Object.DestroyImmediate(resource);}
