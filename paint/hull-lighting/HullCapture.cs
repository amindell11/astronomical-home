using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
public static class HullCapture
{
    const string Output = "D:/amind/git/agent-4/scratch/hull-lighting/";
    static GameObject ship;
    static Camera camera;
    static Light light;
    static System.Collections.Generic.List<Material> paints = new System.Collections.Generic.List<Material>();
    public static string Setup()
    {
        Directory.CreateDirectory(Output);
        AssetDatabase.ImportAsset("Assets/Visuals/Shaders/DrawnSurface.shader", ImportAssetOptions.ForceUpdate);
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.24f,.24f,.24f);
        RenderSettings.fog = false;
        paints.Clear();
        ship = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/Valis.prefab"));
        foreach(var canvas in ship.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
        foreach(var ps in ship.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach(var r in ship.GetComponentsInChildren<Renderer>(true))
        {
            if(!AnimationUtility.CalculateTransformPath(r.transform,ship.transform).StartsWith("ShipBaseRig/Hull/")) { r.enabled=false; continue; }
            var mats = r.sharedMaterials;
            for(int i=0;i<mats.Length;i++) if(mats[i] != null && (mats[i].name == "Ivory" || mats[i].name == "Cool gray")) { mats[i]=new Material(mats[i]); paints.Add(mats[i]); }
            r.sharedMaterials=mats;
        }
        light = new GameObject("Prototype key").AddComponent<Light>();
        light.type=LightType.Directional; light.intensity=1.6f; light.shadows=LightShadows.Soft;
        light.transform.rotation=Quaternion.Euler(20,-25,0); RenderSettings.sun=light;
        camera=new GameObject("Prototype camera").AddComponent<Camera>();
        camera.orthographic=true; camera.orthographicSize=1.95f; camera.transform.position=new Vector3(0,0,-30);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.035f,.04f,.05f);
        camera.nearClipPlane=.01f; camera.farClipPlane=100; camera.allowHDR=false; camera.allowMSAA=false;
        var mesh=ship.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Valis").sharedMesh;
        return "Normals abs z > .9: "+mesh.normals.Count(n=>Mathf.Abs(n.z)>.9f)+" / "+mesh.vertexCount+"; shader: "+string.Join(" | ",ShaderUtil.GetShaderMessages(paints[0].shader).Select(m=>m.message));
    }
    static Color[] Pixels(int width,int height)
    {
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active;
        camera.targetTexture=rt; camera.Render(); camera.Render(); RenderTexture.active=rt;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply(); var pixels=image.GetPixels();
        RenderTexture.active=previous; camera.targetTexture=null;
        UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt); return pixels;
    }
    public static string Frame(string name,float bank=0,float turn=0,float key=0,float cameraOrbit=0)
    {
        ship.transform.rotation=Quaternion.Euler(0,bank,turn);
        light.transform.rotation=Quaternion.Euler(20,-25+key,0);
        camera.transform.position=Quaternion.Euler(0,cameraOrbit,0)*new Vector3(0,0,-30);
        camera.transform.LookAt(Vector3.zero,Vector3.up);
        var sheet=new Texture2D(1280,800,TextureFormat.RGB24,false);
        for(int arm=0;arm<2;arm++)
        {
            foreach(var paint in paints) { if(arm==0)paint.DisableKeyword("_LIGHT_DRIVEN_PAINT");else paint.EnableKeyword("_LIGHT_DRIVEN_PAINT"); }
            camera.orthographicSize=1.95f;
            sheet.SetPixels(arm*640,160,640,640,Pixels(640,640));
            camera.orthographicSize=2.1f;
            sheet.SetPixels(arm*640,0,640,160,Pixels(640,160));
        }
        sheet.Apply(); File.WriteAllBytes(Output+name+".png",sheet.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(sheet);
        return name;
    }
    public static string Motion()
    {
        Setup(); Directory.CreateDirectory(Output+"motion");
        for(int frame=0;frame<120;frame++)
        {
            int stage=frame/40; float phase=2*Mathf.PI*(frame%40)/40f;
            Frame("motion/"+frame.ToString("D3"),stage==0?Mathf.Sin(phase)*65:0,
                stage==0?Mathf.Sin(phase*2)*25:0,stage==1?Mathf.Sin(phase)*100:0,
                stage==2?Mathf.Sin(phase)*35:0);
        }
        return "120 matched frames: ship motion, light sweep, camera orbit";
    }
    public static string First()
    {
        string result=Setup(); Frame("front"); Frame("bank",55,15); Frame("dark-key",0,0,90); return result;
    }
}
