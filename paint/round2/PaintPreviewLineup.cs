using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Substrate;

public static class PaintPreviewLineup
{
    const string Preview = "Assets/Visuals/Ships/_PaintPreview";
    static readonly string[] Ships = { "Crimson", "Nightshade", "Valis" };
    static string output;
    static string masks;
    static readonly System.Text.StringBuilder log = new System.Text.StringBuilder();
    static GameObject[] today;
    static Vector3[] scales;
    const float Spacing = 4.5f;

    public static void Render()
    {
        var args = Environment.GetCommandLineArgs();
        output = args[Array.IndexOf(args, "-paintPreviewOut") + 1];
        masks = args[Array.IndexOf(args, "-paintPreviewMasks") + 1];
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var key = new GameObject("Neutral camera-side key").AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = Color.white;
        key.intensity = 1f;
        key.shadows = UnityEngine.LightShadows.None;
        key.transform.rotation = Quaternion.Euler(15f, -20f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.45f, .45f, .45f);
        log.AppendLine("LIGHT white directional intensity=1 Euler=(15,-20,0); flat ambient=(.45,.45,.45)");
        today = new GameObject[3]; scales = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            today[i] = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Ships/{Ships[i]}.prefab"));
            today[i].transform.rotation = GamePlane.Rotation;
            foreach (var t in today[i].GetComponentsInChildren<Transform>(true)) t.gameObject.SetActive(true);
            foreach (var canvas in today[i].GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            foreach (var r in today[i].GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = IsHull(r);
                if (r.enabled) r.sharedMaterials = r.sharedMaterials.Select(Clone).ToArray();
            }
            var largest = today[i].GetComponentsInChildren<Renderer>().Where(r => r.enabled).OrderByDescending(r => r.bounds.size.sqrMagnitude).First();
            scales[i] = largest.transform.lossyScale;
            log.AppendLine($"CURRENT {Ships[i]} root={today[i].transform.rotation} mesh={largest.transform.rotation} scale={scales[i]} bounds={Bounds(today[i]).size}");
            Place(today[i], new Vector3(i * Spacing, 0, 0));
        }
        var final = new GameObject[3];
        for (int i = 0; i < 3; i++) { final[i] = Candidate(i, "final"); Place(final[i], new Vector3(i * Spacing, -Spacing, 0)); }
        var centre = new Vector3(Spacing, -Spacing / 2, 0);
        Shoot("fleet-final.png", centre, 6.75f, 3000, 2000);
        Shoot("fleet-game-scale.png", centre, 6.75f, 480, 320);
        UnityEngine.Object.DestroyImmediate(key.gameObject);
        var lighting = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/Lighting.prefab");
        var lights = (GameObject)PrefabUtility.InstantiatePrefab(lighting);
        foreach (var light in lights.GetComponentsInChildren<Light>(true)) light.shadows = UnityEngine.LightShadows.None;
        RenderSettings.ambientLight = new Color(.42f, .44f, .48f);
        Shoot("fleet-game-light.png", centre, 6.75f, 3000, 2000);
        File.WriteAllText(Path.Combine(output,"round2-lineup.log"),log.ToString());
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    }

    static Material Clone(Material source)
    {
        var copy = new Material(source) {name=source.name,hideFlags=HideFlags.DontSave};
        if(copy.HasProperty("_CastShadowStrength")) copy.SetFloat("_CastShadowStrength",0);
        return copy;
    }
    static GameObject Candidate(int i,string version)
    {
        if(i==2) {var reference=UnityEngine.Object.Instantiate(today[2]);reference.SetActive(true);return reference;}
        var ship=Ships[i]; var lower=ship.ToLowerInvariant();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"{Preview}/{ship}/{ship}.fbx");
        if(prefab==null) throw new InvalidOperationException("Missing FBX "+ship);
        var go=UnityEngine.Object.Instantiate(prefab);
        go.transform.rotation=Quaternion.Euler(0,180,0);
        go.transform.localScale=scales[i] * (i == 1 ? 1f : 100f);
        var palette=Directory.GetDirectories($"{Preview}/{ship}/Materials").Single().Replace('\\','/');
        var maskRoot = Path.Combine(masks,lower);
        foreach(var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if(r.name=="Collider") {r.enabled=false;continue;}
            r.enabled=true;
            var mats=r.sharedMaterials;
            for(int slot=0;slot<mats.Length;slot++)
            {
                string name=mats[slot].name;
                Material source=AssetDatabase.LoadAssetAtPath<Material>($"{palette}/{name}.mat");
                if(name=="Contour") source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Ships/Nightshade/Materials/Contour.mat");
                if(source==null) throw new InvalidOperationException($"Missing region {ship}/{r.name}/{slot}/{name}");
                var material=Clone(source);
                if(name!="Contour")
                {
                    foreach(var mask in new[]{"Shadow","Light","Ink","Emission"})
                    {
                        var path=Path.Combine(maskRoot,mask+".png");
                        if(!File.Exists(path)) {if(mask=="Emission") {material.SetFloat("_PaintEmissionBlend",0);continue;}throw new FileNotFoundException(path);}
                        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
                        texture.LoadImage(File.ReadAllBytes(path));texture.wrapMode=TextureWrapMode.Clamp;
                        material.SetTexture(mask=="Emission"?"_EmissionMap":"_Paint"+mask+"Map",texture);
                    }
                    if(version=="flat") foreach(var knob in new[]{"_PaintShadowStrength","_PaintLightStrength","_PaintInkStrength","_PaintEmissionBlend"}) material.SetFloat(knob,0);
                }
                mats[slot]=material;
            }
            r.sharedMaterials=mats;
        }
        log.AppendLine($"CANDIDATE {ship} {version} rotation={go.transform.rotation} scale={go.transform.localScale} bounds={Bounds(go).size}");
        return go;
    }
    static bool IsHull(Renderer r) => (r is MeshRenderer || r is SkinnedMeshRenderer) && r.name!="Collider" && r.sharedMaterial!=null && r.sharedMaterial.shader!=null && new[]{"Astronomical/Drawn/Surface","Astronomical/Drawn/Canopy","Astronomical/Drawn/Contour"}.Contains(r.sharedMaterial.shader.name);
    static Bounds Bounds(GameObject go)
    {
        var renderers=go.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
        var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);return bounds;
    }
    static void Place(GameObject go,Vector3 centre) {go.transform.position+=centre-Bounds(go).center;}
    static void Shoot(string name,Vector3 centre,float halfWidth,int w,int h)
    {
        var go=new GameObject("capture");var cam=go.AddComponent<Camera>();cam.orthographic=true;
        cam.orthographicSize=halfWidth*h/w;cam.clearFlags=CameraClearFlags.SolidColor;
        cam.backgroundColor=new Color(.08f,.09f,.11f);cam.nearClipPlane=.1f;cam.farClipPlane=400;
        go.transform.position=centre-GamePlane.Normal*100;go.transform.rotation=Quaternion.LookRotation(GamePlane.Normal,GamePlane.Forward);
        var rt=new RenderTexture(w,h,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
        var image=new Texture2D(w,h,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,w,h),0,0);image.Apply();
        RenderTexture.active=null;cam.targetTexture=null;
        File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);
    }
}
