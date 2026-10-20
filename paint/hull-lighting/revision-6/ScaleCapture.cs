using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
public static class ScaleCapture
{
    public static string Diagnose()
    {
        string report=Setup();
        Directory.CreateDirectory(Output+"revision-6");
        light.transform.rotation=Quaternion.identity;
        light.shadows=LightShadows.None;
        camera.orthographicSize=.85f;
        var mesh=ship.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Valis").sharedMesh;
        report+="; mesh bounds "+mesh.bounds;
        string source=File.ReadAllText(Output+"v5.shader");
        var sheet=new Texture2D(2048,640,TextureFormat.RGB24,false);
        for(int arm=0;arm<4;arm++)
        {
            string variant=source.Replace("Astronomical/Drawn/Surface", "Hidden/BrushSeamProbe"+arm);
            if(arm==2) variant=variant.Replace("clip(_DebrisVisibility - InterleavedGradientNoise(input.positionCS.xy, 0));", "return half4(frac(input.uv*3.3),0,1);");
            if(arm==3) variant=variant.Replace("clip(_DebrisVisibility - InterleavedGradientNoise(input.positionCS.xy, 0));", "return half4(normalize(input.normalWS)*.5+.5,1);");
            var shader=ShaderUtil.CreateShaderAsset(variant);
            foreach(var paint in paints) { paint.shader=shader; paint.SetFloat("_PaintBrushStrength",arm==1?0:3.2f); paint.EnableKeyword("_LIGHT_DRIVEN_PAINT"); }
            sheet.SetPixels(arm*512,0,512,640,Pixels(512,640));
        }
        sheet.Apply(); File.WriteAllBytes(Output+"revision-6/seam-diagnosis.png",sheet.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(sheet);
        return report;
    }
    const string Output = "D:/amind/git/agent-4/scratch/hull-lighting/";
    static GameObject ship;
    static Camera camera;
    static Light light;
    static Shader previousShader;
    static Shader currentShader;
    static Texture currentBrush;
    static Texture previousBrush;
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
        previousShader = null;
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
        currentShader = paints[0].shader;
        currentBrush = paints[0].GetTexture("_PaintBrushMap");
        var tile = new Texture2D(2,2,TextureFormat.RGB24,true,true);
        tile.LoadImage(File.ReadAllBytes("D:/amind/git/agent-4/scratch/hull-lighting/revision-5/bake/Brush.png"));
        previousBrush = tile;
        previousBrush.wrapMode = TextureWrapMode.Clamp;
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
    static void UseArm(int arm)
    {
        foreach(var paint in paints)
        {
            paint.shader=arm==0?previousShader:currentShader;
            paint.EnableKeyword("_LIGHT_DRIVEN_PAINT");
            paint.SetTexture("_PaintBrushMap",arm==0?previousBrush:currentBrush);
            paint.SetTextureScale("_PaintBrushMap",Vector2.one*(1f));
            paint.SetTextureOffset("_PaintBrushMap",Vector2.zero);
        }
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
            UseArm(arm);
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
        var wingBones=ship.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Valis").bones.Skip(1).ToArray();
        var rest=wingBones.Select(b=>b.localRotation).ToArray();
        for(int frame=0;frame<160;frame++)
        {
            int stage=frame/40; float phase=2*Mathf.PI*(frame%40)/40f;
            for(int j=0;j<wingBones.Length;j++) wingBones[j].localRotation=Quaternion.Slerp(rest[j],Quaternion.identity,stage==3?(1-Mathf.Cos(phase))*.5f:0);
            Frame("motion/"+frame.ToString("D3"),stage==0?Mathf.Sin(phase)*65:0,
                stage==0?Mathf.Sin(phase*2)*25:0,stage==1?Mathf.Sin(phase)*100:0,
                stage==2?Mathf.Sin(phase)*35:0);
        }
        return "120 matched frames: ship motion, light sweep, camera orbit";
    }
    public static string Revision()
    {
        string report=Setup(); Directory.CreateDirectory(Output+"revision-6/motion");
        previousShader=ShaderUtil.CreateShaderAsset(File.ReadAllText(Output+"v5.shader").Replace("Astronomical/Drawn/Surface", "Hidden/HullPrevious"));
        var wingBones=ship.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Valis").bones.Skip(1).ToArray();
        var rest=wingBones.Select(b=>b.localRotation).ToArray();
        for(int frame=0;frame<160;frame++)
        {
            int stage=frame/40; float phase=2*Mathf.PI*(frame%40)/40f;
            for(int j=0;j<wingBones.Length;j++) wingBones[j].localRotation=Quaternion.Slerp(rest[j],Quaternion.identity,stage==3?(1-Mathf.Cos(phase))*.5f:0);
            Frame("revision-6/motion/"+frame.ToString("D3"),stage==0?Mathf.Sin(phase)*65:0,
                stage==0?Mathf.Sin(phase*2)*25:0,stage==1?Mathf.Sin(phase)*100:0,
                stage==2?Mathf.Sin(phase)*35:0);
        }
        ship.transform.rotation=Quaternion.identity;
        camera.transform.position=new Vector3(0,0,-30); camera.transform.rotation=Quaternion.identity;
        camera.orthographicSize=.85f;
        var sheet=new Texture2D(1024,640,TextureFormat.RGB24,false);
        light.transform.rotation=Quaternion.Euler(20,-25,0);
        for(int arm=0;arm<2;arm++)
        {
            UseArm(arm);
            sheet.SetPixels(arm*512,0,512,640,Pixels(512,640));
        }
        sheet.Apply(); File.WriteAllBytes(Output+"revision-6/closeup.png",sheet.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(sheet);
        return report+"; quality "+QualitySettings.names[QualitySettings.GetQualityLevel()]+"; previous shader: "+string.Join(" | ",ShaderUtil.GetShaderMessages(previousShader).Select(m=>m.message));
    }
    public static string Ablation()
    {
        Setup();
        string source=File.ReadAllText(Output+"v5.shader");
        var output=new Texture2D(2048,640,TextureFormat.RGB24,false);
        string report="";
        for(int arm=0;arm<4;arm++)
        {
            string variant=source.Replace("Astronomical/Drawn/Surface", "Hidden/HullProbe"+arm);
            if(arm>=1) variant=variant.Replace("relit * brush", "relit");
            if(arm==1) variant=variant.Replace("(broadLight - broadShadow) * _PaintLightingBias", "(saturate(paintedLight) - saturate(shadow)) * _PaintLightingBias * saturate(1 - abs(ndl))");
            if(arm>=2) variant=variant.Replace("(broadLight - broadShadow) * _PaintLightingBias", "0");
            if(arm>=1) variant=variant.Replace("smoothstep(0.65h, 0.9h, abs(input.paintFacing))", "1");
            light.shadows=arm>=2?LightShadows.None:LightShadows.Soft;
            if(arm==2) variant=variant.Replace("half3 normal = DrawnWorldNormal(input.normalWS, input.tangentWS, input.uv);", "half3 normal = DrawnWorldNormal(input.normalWS, input.tangentWS, input.uv); return half4(normal * .5 + .5, 1);");
            if(arm==3) variant=variant.Replace("half3 normal = DrawnWorldNormal(input.normalWS, input.tangentWS, input.uv);", "half3 normal = DrawnWorldNormal(input.normalWS, input.tangentWS, input.uv); return _PaperColor;");
            if(arm==3) foreach(var paint in paints) paint.SetFloat("_PaintInkStrength",0);
            var shader=ShaderUtil.CreateShaderAsset(variant);
            foreach(var paint in paints) { paint.shader=shader; paint.EnableKeyword("_LIGHT_DRIVEN_PAINT"); }
            camera.orthographicSize=.85f; camera.transform.position=new Vector3(0,0,-30);
            output.SetPixels(arm*512,0,512,640,Pixels(512,640));
            report+="arm "+arm+": "+string.Join(" | ",ShaderUtil.GetShaderMessages(shader).Select(m=>m.message))+"; ";
        }
        output.Apply(); File.WriteAllBytes(Output+"ablation-normals.png",output.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(output); return report;
    }
    public static string Direct()
    {
        string report=Setup();
        previousShader=ShaderUtil.CreateShaderAsset(File.ReadAllText(Output+"v5.shader").Replace("Astronomical/Drawn/Surface", "Hidden/HullPreviousDirect"));
        camera.orthographicSize=.85f;
        light.transform.rotation=Quaternion.identity;
        var sheet=new Texture2D(1024,640,TextureFormat.RGB24,false);
        for(int arm=0;arm<2;arm++)
        {
            UseArm(arm);
            sheet.SetPixels(arm*512,0,512,640,Pixels(512,640));
        }
        sheet.Apply(); File.WriteAllBytes(Output+"revision-6/direct-light.png",sheet.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(sheet);
        return report;
    }
    public static string BrushOptions()
    {
        string report=Setup(); Directory.CreateDirectory(Output+"revision-6");
        light.transform.rotation=Quaternion.identity;
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Visuals/Ships/_Shared/Paint/Brush.png");
        if(texture==null) throw new InvalidOperationException("Brush import unfinished");
        var sheet=new Texture2D(1920,640,TextureFormat.RGB24,false);
        camera.orthographicSize=.85f;
        float[] scales={.5f,.8f,1.2f};
        for(int arm=0;arm<3;arm++)
        {
            foreach(var paint in paints)
            {
                paint.EnableKeyword("_LIGHT_DRIVEN_PAINT"); paint.SetTexture("_PaintBrushMap",texture);
                paint.SetFloat("_PaintBrushStrength",3.2f); paint.SetTextureScale("_PaintBrushMap",Vector2.one*scales[arm]);
            }
            sheet.SetPixels(arm*640,0,640,640,Pixels(640,640));
        }
        sheet.Apply(); File.WriteAllBytes(Output+"revision-6/brush-options.png",sheet.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(sheet);
        return report;
    }
    public static string Materials()
    {
        Setup();
        var renderer=ship.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Valis");
        var mats=renderer.sharedMaterials;
        Color[] colors={Color.red,Color.green,Color.blue,Color.yellow,Color.cyan,Color.magenta,Color.white,Color.gray};
        string report="";
        for(int i=0;i<mats.Length;i++)
        {
            report+=i+"="+mats[i].name+" ";
            mats[i]=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mats[i].SetColor("_BaseColor",colors[i]);
        }
        renderer.sharedMaterials=mats;
        camera.orthographicSize=.85f; camera.transform.position=new Vector3(0,0,-30);
        var output=new Texture2D(512,640,TextureFormat.RGB24,false);
        output.SetPixels(Pixels(512,640)); output.Apply();
        File.WriteAllBytes(Output+"materials.png",output.EncodeToPNG());
        return report;
    }
    public static string First()
    {
        string result=Setup(); Frame("front"); Frame("bank",55,15); Frame("dark-key",0,0,90); return result;
    }
}
