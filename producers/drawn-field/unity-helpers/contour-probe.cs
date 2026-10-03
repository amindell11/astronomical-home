var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<Asteroids.Spawning.AsteroidSpawnSettings>("Assets/Settings/Asteroids/SpawnSettings.asset");
var mesh = settings.meshInfos[0].mesh;
var output = "D:/amind/git/agent-4/results/contour-probe";
System.IO.Directory.CreateDirectory(output);
var root = new UnityEngine.GameObject("Contour probe");
var resources = new System.Collections.Generic.List<UnityEngine.Object>();
try {
var subject = new UnityEngine.GameObject("Subject", typeof(UnityEngine.MeshFilter), typeof(UnityEngine.MeshRenderer));
subject.transform.SetParent(root.transform); subject.layer = 31;
subject.GetComponent<UnityEngine.MeshFilter>().sharedMesh = mesh;
var white = new UnityEngine.Material(UnityEngine.Shader.Find("Astronomical/Comparison/Drawn Surface")); resources.Add(white); white.SetColor("_BaseColor", UnityEngine.Color.white); white.SetTexture("_EmissionMap",UnityEngine.Texture2D.whiteTexture); white.SetColor("_EmissionColor",UnityEngine.Color.white); white.SetFloat("_EmissionStrength",1);
subject.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial = white;
var outline = new UnityEngine.GameObject("Outline", typeof(UnityEngine.MeshFilter), typeof(UnityEngine.MeshRenderer)); outline.transform.SetParent(subject.transform,false); outline.layer=31;
outline.GetComponent<UnityEngine.MeshFilter>().sharedMesh=mesh;
var ink = new UnityEngine.Material(UnityEngine.Shader.Find("Astronomical/Comparison/Drawn Contour")); resources.Add(ink); ink.SetColor("_ContourColor",UnityEngine.Color.black); ink.SetFloat("_ContourMinimum",1); ink.SetFloat("_ContourPixels",3.2f);
outline.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial=ink;
var camera = new UnityEngine.GameObject("Probe camera",typeof(UnityEngine.Camera)).GetComponent<UnityEngine.Camera>(); camera.transform.SetParent(root.transform); camera.transform.position=new UnityEngine.Vector3(0,0,6); camera.transform.LookAt(UnityEngine.Vector3.zero); camera.cullingMask=1<<31; camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor; camera.backgroundColor=new UnityEngine.Color(.4f,.4f,.4f); camera.allowHDR=false; camera.allowMSAA=false;
var rt = new UnityEngine.RenderTexture(768,768,24); resources.Add(rt); camera.targetTexture=rt;
var pixels = new UnityEngine.Texture2D(768,768,UnityEngine.TextureFormat.RGB24,false); resources.Add(pixels);
var trace = System.IO.File.ReadAllLines("D:/amind/git/agent-4/results/capture/frames/20260924-143538-DrawnContourScenario/motion.csv");
foreach(var frame in new[]{450,600,750}) {
var row=trace[frame+1].Split(','); subject.transform.rotation=new UnityEngine.Quaternion(float.Parse(row[5],System.Globalization.CultureInfo.InvariantCulture),float.Parse(row[6],System.Globalization.CultureInfo.InvariantCulture),float.Parse(row[7],System.Globalization.CultureInfo.InvariantCulture),float.Parse(row[8],System.Globalization.CultureInfo.InvariantCulture));
foreach(var enabled in new[]{false,true}) {outline.SetActive(enabled); camera.Render(); var prior=UnityEngine.RenderTexture.active; UnityEngine.RenderTexture.active=rt; pixels.ReadPixels(new UnityEngine.Rect(0,0,768,768),0,0); pixels.Apply(); UnityEngine.RenderTexture.active=prior; System.IO.File.WriteAllBytes(output+"/"+frame+"-"+(enabled?"ink":"base")+".png",pixels.EncodeToPNG());}
}
var vertices=mesh.vertices; var normals=mesh.normals; var seen=new System.Collections.Generic.Dictionary<UnityEngine.Vector3,UnityEngine.Vector3>(); int conflicts=0;
for(int i=0;i<vertices.Length;i++){if(seen.TryGetValue(vertices[i],out var n)){if(UnityEngine.Vector3.Dot(n,normals[i])<.99f)conflicts++;}else seen.Add(vertices[i],normals[i]);}
return new {mesh=mesh.name,vertices=mesh.vertexCount,submeshes=mesh.subMeshCount,uniquePositions=seen.Count,splitNormalConflicts=conflicts,output};
} finally {UnityEngine.Object.DestroyImmediate(root); foreach(var resource in resources) UnityEngine.Object.DestroyImmediate(resource);}
