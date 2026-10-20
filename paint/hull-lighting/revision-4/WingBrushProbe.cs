using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WingBrushProbe
{
    public static string Run()
    {
        const string root = "D:/amind/git/agent-4/scratch/hull-lighting/";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ShaderUtil.allowAsyncCompilation = false;
        var ship = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/Valis.prefab"));
        var renderer = ship.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Valis");
        var source = File.ReadAllText(root+"v4.shader").Replace("Astronomical/Drawn/Surface","Hidden/WingBrushProbe");
        source = source.Replace("output.positionCS = position.positionCS;", "output.positionCS = float4(input.uv * 2 - 1, 0, 1);");
        source = source.Replace("// The house brush tile treats 0.625 as neutral pigment.", "return half4(brush, brush, brush, 1);");
        source = source.Replace("Name \"DrawnSurface\"", "Name \"DrawnSurface\"\n Cull Off ZTest Always ZWrite Off");
        var shader = ShaderUtil.CreateShaderAsset(source);
        var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Ships/Valis/Materials/jade-iris/Ivory.mat");
        var material = new Material(original) { shader=shader };
        material.EnableKeyword("_PAINT_LAYERS"); material.EnableKeyword("_LIGHT_DRIVEN_PAINT");
        var rt = new RenderTexture(1024,1024,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var image = new Texture2D(1024,1024,TextureFormat.RGBA32,false,true);
        var baked = new Mesh();
        Color32[][] samples = new Color32[2][];
        for(int arm=0; arm<2; arm++)
        {
            if(arm==1) foreach(var bone in renderer.bones.Skip(1)) bone.localRotation=Quaternion.identity;
            renderer.BakeMesh(baked);
            RenderTexture.active=rt; GL.Clear(true,true,Color.clear);
            material.SetPass(0);
            for(int i=0;i<renderer.sharedMaterials.Length;i++)
                if(renderer.sharedMaterials[i].name=="Ivory" || renderer.sharedMaterials[i].name=="Cool gray")
                    Graphics.DrawMeshNow(baked,renderer.localToWorldMatrix,i);
            image.ReadPixels(new Rect(0,0,1024,1024),0,0); image.Apply();
            samples[arm]=image.GetPixels32();
            File.WriteAllBytes(root+"revision-4/wing-brush-"+arm+".png",image.EncodeToPNG());
        }
        int changed=0, covered=0, maximum=0;
        for(int i=0;i<samples[0].Length;i++) if(samples[0][i].a>0 && samples[1][i].a>0)
        {
            covered++;
            int delta=Math.Abs(samples[0][i].r-samples[1][i].r);
            maximum=Math.Max(maximum,delta);
            if(delta>2)changed++;
        }
        RenderTexture.active=null;
        UnityEngine.Object.DestroyImmediate(baked); UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(material);
        return "Same UV texels before/after actual wing-bone sweep: "+changed+" / "+covered+" changed by >2/255; maximum delta "+maximum+"/255; shader: "+string.Join(" | ",ShaderUtil.GetShaderMessages(shader).Select(m=>m.message));
    }
}
