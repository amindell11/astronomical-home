using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
public static class CrimsonLineup
{
    public static string Capture()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        bool priorAsync = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.24f,.24f,.24f);
        RenderSettings.fog = false;
        string[] names = {"Valis","Crimson","Vanguard","Nightshade"};
        var all = new List<Renderer>();
        for (int i=0; i<names.Length; i++)
        {
            var ship = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/"+names[i]+".prefab"));
            ship.transform.position = new Vector3((i-1.5f)*3.2f,0,0);
            ship.transform.rotation = Quaternion.identity;
            foreach (var canvas in ship.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            foreach (var particles in ship.GetComponentsInChildren<ParticleSystem>(true)) particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var renderer in ship.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = AnimationUtility.CalculateTransformPath(renderer.transform,ship.transform).StartsWith("ShipBaseRig/Hull/");
                if (renderer.enabled) all.Add(renderer);
            }
        }
        var bounds=all[0].bounds;
        foreach(var renderer in all) bounds.Encapsulate(renderer.bounds);
        var light=new GameObject("LineupLight").AddComponent<Light>();
        light.type=LightType.Directional; light.intensity=1.6f; light.shadows=LightShadows.Soft;
        light.transform.rotation=Quaternion.Euler(20,-25,0); RenderSettings.sun=light;
        var camera=new GameObject("LineupCamera").AddComponent<Camera>();
        camera.orthographic=true; camera.aspect=4;
        camera.orthographicSize=Mathf.Max(bounds.extents.x/4,bounds.extents.y)*1.18f;
        camera.transform.position=new Vector3(bounds.center.x,bounds.center.y,-30);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.035f,.04f,.05f);
        camera.nearClipPlane=.01f; camera.farClipPlane=100; camera.allowHDR=false;
        foreach(int width in new[]{2048,1024})
        {
            int height=width/4;
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
            var prior=RenderTexture.active;
            try
            {
                camera.targetTexture=target; camera.Render(); camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
                File.WriteAllBytes("D:/amind/git/agent-5/scratch/crimson-paint/lineup-"+width+".png",pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=prior; camera.targetTexture=null;
                UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target);
            }
        }
        ShaderUtil.allowAsyncCompilation=priorAsync;
        string manifest="Left to right: "+string.Join(", ",names)+"; unchanged prefab scales; quality="+QualitySettings.names[QualitySettings.GetQualityLevel()];
        File.WriteAllText("D:/amind/git/agent-5/scratch/crimson-paint/lineup.txt",manifest);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        return manifest;
    }
}
