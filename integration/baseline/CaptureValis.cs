using System.IO;
using UnityEngine;
using UnityEditor;
public static class CaptureValis {
 public static string Main(){
  var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/Valis.prefab");
  var source=prefab.transform.Find("Valis VisualRig/Valis");
  var hull=new GameObject("Valis preview");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(hull,scene);hull.AddComponent<MeshFilter>().sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;hull.AddComponent<MeshRenderer>().sharedMaterials=source.GetComponent<MeshRenderer>().sharedMaterials;
  var light=new GameObject("Preview light").AddComponent<Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(55,-50,0);
  var cam=new GameObject("Preview camera").AddComponent<Camera>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cam.gameObject,scene);cam.scene=scene;cam.orthographic=true;cam.orthographicSize=1.3f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.022f,.032f,.044f);cam.nearClipPlane=.1f;cam.farClipPlane=40;
  foreach(var view in new[]{"top","quarter","game-scale"}){
   cam.transform.position=view=="quarter"?new Vector3(3,2,-5):new Vector3(0,0,-6);cam.transform.LookAt(Vector3.zero,Vector3.up);cam.orthographicSize=view=="game-scale"?9:1.3f;
   var rt=new RenderTexture(1000,1000,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1000,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1000,1000),0,0);tex.Apply();File.WriteAllBytes("D:/amind/git/agent-2/results/valis-integration/unity-"+view+".png",tex.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
  }
  UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
  return "Rendered Unity top, quarter, game-scale";
 }
}

