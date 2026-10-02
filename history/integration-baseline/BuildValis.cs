using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Ships;
using Ships.Loadout;
using Ships.Visuals;
using Ships.Presentation;
using Newtonsoft.Json.Linq;
using Object=UnityEngine.Object;
public static class BuildValis
{
 [Serializable] public class Part { public string name,material; public Vector3[] vertices,normals; public Vector2[] uv; public int[] triangles; }
 [Serializable] public class Export { public Part[] parts; }
 const string Folder="Assets/Visuals/Ships/Valis";
 const string Work="D:/amind/git/agent-2/results/valis-integration";
 static string[] Regions={"Ivory","Cool gray","Lavender","Graphite","Edge","Canopy","Engine"};
 static T Save<T>(T asset,string path) where T:Object { var old=AssetDatabase.LoadAssetAtPath<T>(path); if(old){EditorUtility.CopySerialized(asset,old);Object.DestroyImmediate(asset);EditorUtility.SetDirty(old);return old;} AssetDatabase.CreateAsset(asset,path);return asset; }
 public static string Main()
 {
  Directory.CreateDirectory(Folder+"/Materials");Directory.CreateDirectory(Folder+"/Meshes");AssetDatabase.Refresh();
  var data=Newtonsoft.Json.JsonConvert.DeserializeObject<Export>(File.ReadAllText(Work+"/valis-meshes.json"));
  var palette=JObject.Parse(File.ReadAllText("D:/amind/git/agent-2/art/ships/valis/palettes.json"))["palettes"];
  var materials=new Dictionary<string,Material[]>();
  foreach(var p in ((JObject)palette).Properties()) {
   Directory.CreateDirectory(Folder+"/Materials/"+p.Name);AssetDatabase.Refresh();var mats=new List<Material>();
   foreach(var region in Regions) {
    var c=p.Value[region];var mat=new Material(Shader.Find("Astronomical/Comparison/Drawn Surface"));mat.name=region;
    mat.SetColor("_BaseColor",Color.white);mat.SetColor("_PaperColor",new Color((float)c[0],(float)c[1],(float)c[2],1).gamma);
    mat.SetFloat("_TextureStrength",0);mat.SetFloat("_LineStrength",0);mat.SetFloat("_WearStrength",0);mat.SetFloat("_SpecularStrength",0);mat.SetFloat("_PaletteLighting",1);mat.SetFloat("_AmbientStrength",.35f);mat.SetFloat("_CastShadowStrength",.6f);mat.SetFloat("_ShadowThreshold",.35f);mat.SetFloat("_ShadowSoftness",.1f);mat.SetColor("_ShadowColor",new Color(.5f,.6f,.72f));
    mats.Add(Save(mat,Folder+"/Materials/"+p.Name+"/"+region+".mat"));
   }
   materials[p.Name]=mats.ToArray();
  }
  var contour=new Material(Shader.Find("Astronomical/Comparison/Drawn Contour"));contour.SetFloat("_ContourPixels",1.2f);contour.SetFloat("_ContourMinimum",.65f);contour.SetFloat("_UniformWidth",1);contour.SetColor("_ContourColor",new Color(.007f,.012f,.018f));contour=Save(contour,Folder+"/Materials/Contour.mat");
  var previous=GameObject.Find("Valis");if(previous)Object.DestroyImmediate(previous);
  var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/Ship_3.prefab");
  var ship=(GameObject)PrefabUtility.InstantiatePrefab(template);PrefabUtility.UnpackPrefabInstance(ship,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);ship.name="Valis";
  ship.transform.position=Vector3.zero;ship.transform.rotation=Quaternion.identity;
  var oldCollider=ship.GetComponentInChildren<MeshCollider>();
  var legacy=oldCollider.sharedMesh.vertices.Select(v=>ship.transform.InverseTransformPoint(oldCollider.transform.TransformPoint(v))).ToArray();
  float targetLength=legacy.Max(v=>v.y)-legacy.Min(v=>v.y);
  var raw=data.parts.SelectMany(p=>p.vertices).ToArray();var minY=raw.Min(v=>v.y);var maxY=raw.Max(v=>v.y);float scale=targetLength/(maxY-minY);var center=new Vector3(0,(maxY+minY)*.5f,0);
  var verts=new List<Vector3>();var norms=new List<Vector3>();var uv=new List<Vector2>();var sub=Enumerable.Range(0,8).Select(_=>new List<int>()).ToArray();
  foreach(var part in data.parts) {
   
   var idx=Array.IndexOf(Regions,part.material);if(idx<0)throw new Exception(part.material);
   var offset=verts.Count;verts.AddRange(part.vertices.Select(v=>(v-center)*scale));norms.AddRange(part.normals);uv.AddRange(part.uv);sub[idx].AddRange(part.triangles.Select(t=>t+offset));
   var joined=new Dictionary<Vector3,Vector3>();for(int i=0;i<part.vertices.Length;i++){var v=part.vertices[i];joined.TryGetValue(v,out var n);joined[v]=n+part.normals[i];}
   offset=verts.Count;verts.AddRange(part.vertices.Select(v=>(v-center)*scale));norms.AddRange(part.vertices.Select(v=>joined[v].normalized));uv.AddRange(part.uv);sub[7].AddRange(part.triangles.Select(t=>t+offset));
  }
  var mesh=new Mesh{name="Valis surfaces and contours",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(verts);mesh.SetNormals(norms);mesh.SetUVs(0,uv);mesh.subMeshCount=8;for(int i=0;i<8;i++)mesh.SetTriangles(sub[i],i);mesh.RecalculateBounds();mesh.RecalculateTangents();mesh=Save(mesh,Folder+"/Meshes/Valis hull.asset");
  var rig=ship.GetComponentInChildren<ShipVisualRig>(true);rig.name="Valis VisualRig";
  var model=rig.transform.Find("Model");model.name="Valis";model.localPosition=Vector3.zero;model.localRotation=Quaternion.identity;model.localScale=Vector3.one;model.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=model.GetComponent<MeshRenderer>();renderer.sharedMaterials=materials["jade-iris"].Concat(new[]{contour}).ToArray();
  var hullVisual=rig.GetComponent<HullVisuals>();var hv=new SerializedObject(hullVisual);hv.FindProperty("hull").objectReferenceValue=renderer;hv.ApplyModifiedPropertiesWithoutUndo();
  var collider=BuildCollider(raw.Select(v=>(v-center)*scale).ToArray());collider=Save(collider,Folder+"/Meshes/Valis collider.asset");oldCollider.transform.localPosition=Vector3.zero;oldCollider.transform.localRotation=Quaternion.identity;oldCollider.transform.localScale=Vector3.one;oldCollider.sharedMesh=collider;oldCollider.convex=true;
  var marker=rig.transform.Find("MinimapMarker");marker.localPosition=Vector3.zero;marker.localRotation=Quaternion.identity;marker.localScale=Vector3.one*2;marker.GetComponent<MeshFilter>().sharedMesh=collider;
  var exhaust=data.parts.Single(p=>p.name=="64 exhaust rim").vertices;var exhaustCenter=exhaust.Aggregate(Vector3.zero,(a,b)=>a+b)/exhaust.Length;exhaustCenter=(exhaustCenter-center)*scale;
  var thrust=rig.transform.Find("Thruster");thrust.localPosition=exhaustCenter-new Vector3(0,-.19f*thrust.localScale.y,-.03f*thrust.localScale.z);
  foreach(var mount in ship.transform.Find("Hardpoints").Cast<Transform>())mount.localPosition=new Vector3(Mathf.Sign(mount.localPosition.x)*.4f,(maxY-center.y)*scale+.03f,0);
  var saved=PrefabUtility.SaveAsPrefabAsset(ship,"Assets/Prefabs/Ships/Valis.prefab");Object.DestroyImmediate(ship);
  var offer=AssetDatabase.LoadAssetAtPath<ItemSubset>("Assets/Settings/Ships/PlayerLoadout.asset");var valis=saved.GetComponent<Ship>();offer.ships=offer.ships.Where(s=>s && s.name!="Valis").Concat(new[]{valis}).ToArray();EditorUtility.SetDirty(offer);AssetDatabase.SaveAssets();
  File.WriteAllText(Work+"/unity-build.json",Newtonsoft.Json.JsonConvert.SerializeObject(new Report{scale=scale,targetLength=targetLength,vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,parts=data.parts.Length},Newtonsoft.Json.Formatting.Indented));
  return "Created Valis; scale="+scale+" length="+targetLength+" parts="+data.parts.Length+"; roster="+offer.ships.Length;
 }
 [Serializable] public class Report {public float scale,targetLength;public int vertices,triangles,parts;}
 static Mesh BuildCollider(Vector3[] v) {
  var pts=v.Select(p=>new Vector2(p.x,p.y)).Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToArray();var hull=new List<Vector2>();
  foreach(var p in pts){while(hull.Count>=2 && Cross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}int lower=hull.Count;
  for(int i=pts.Length-2;i>=0;i--){var p=pts[i];while(hull.Count>lower && Cross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}hull.RemoveAt(hull.Count-1);
  int n=hull.Count;float low=v.Min(p=>p.z),high=v.Max(p=>p.z);var points=hull.Select(p=>new Vector3(p.x,p.y,low)).Concat(hull.Select(p=>new Vector3(p.x,p.y,high))).ToArray();var tri=new List<int>();
  for(int i=1;i<n-1;i++){tri.AddRange(new[]{0,i+1,i,n,n+i,n+i+1});}for(int i=0;i<n;i++){int j=(i+1)%n;tri.AddRange(new[]{i,j,n+j,i,n+j,n+i});}
  var mesh=new Mesh{name="Valis convex footprint"};mesh.vertices=points;mesh.triangles=tri.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
 }
 static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
}




