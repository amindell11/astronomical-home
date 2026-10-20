using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class CrimsonPaintCapture
{
    const string Output = "D:/amind/git/agent-5/scratch/crimson-paint/";
    public static string Baseline() { return Capture("baseline"); }
    public static string Converted() { AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); return Capture("converted"); }

    public static string PaletteProof() { return Capture("palette-proof"); }
    public static string Matched()
    {
        bool prior = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Capture("baseline", true);
            Capture("baseline-repeat", true);
            return Capture("converted");
        }
        finally { ShaderUtil.allowAsyncCompilation = prior; }
    }

    static string Capture(string label, bool useAtlas = false)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.24f, .24f, .24f);
        RenderSettings.fog = false;
        var ship = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/Crimson.prefab"));
        ship.transform.position = Vector3.zero;
        ship.transform.rotation = Quaternion.identity;
        foreach (var canvas in ship.GetComponentsInChildren<Canvas>(true))
            canvas.gameObject.SetActive(false);
        foreach (var particles in ship.GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var visible = new List<Renderer>();
        foreach (var renderer in ship.GetComponentsInChildren<Renderer>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(renderer.transform, ship.transform);
            renderer.enabled = path.StartsWith("ShipBaseRig/Hull/");
            if (renderer.enabled) visible.Add(renderer);
        }
        if (visible.Count == 0) throw new InvalidOperationException("No Crimson hull renderers.");
        Material atlasMaterial = null;
        if (useAtlas)
        {
            var painted = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Ships/Crimson/Hull paint.mat");
            atlasMaterial = new Material(painted);
            atlasMaterial.DisableKeyword("_PAINT_LAYERS");
            atlasMaterial.SetFloat("_PaintLayers", 0);
            atlasMaterial.SetFloat("_TextureStrength", 1);
            atlasMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                AssetDatabase.GUIDToAssetPath("1401db79b2767154dbf4de2b045cef36")));
            atlasMaterial.SetColor("_PaperColor", new Color(.65f, .7f, .75f));
            foreach (var renderer in visible)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m == painted ? atlasMaterial : m).ToArray();
        }
        if (label == "palette-proof")
        {
            var painted = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Ships/Crimson/Hull paint.mat");
            atlasMaterial = new Material(painted);
            foreach (int region in new[] { 3, 5, 6 })
                atlasMaterial.SetColor("_PaintRegionColor" + region, new Color(.03f, .5f, .65f));
            foreach (var renderer in visible)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m == painted ? atlasMaterial : m).ToArray();
        }
        var bounds = visible[0].bounds;
        foreach (var renderer in visible) bounds.Encapsulate(renderer.bounds);
        var light = new GameObject("CaptureLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.6f;
        light.shadows = LightShadows.Soft;
        RenderSettings.sun = light;
        var camera = new GameObject("CaptureCamera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.18f;
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -30);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .04f, .05f);
        camera.nearClipPlane = .01f;
        camera.farClipPlane = 100;
        camera.allowHDR = false;
        Directory.CreateDirectory(Output + label);
        string[] names = { "top", "bank", "underside", "side-light" };
        Vector3[] poses = { Vector3.zero, new Vector3(0, 35, 25), new Vector3(0, 180, 0), Vector3.zero };
        for (int i = 0; i < names.Length; i++)
        {
            ship.transform.rotation = Quaternion.Euler(poses[i]);
            light.transform.rotation = Quaternion.Euler(i == 3 ? new Vector3(65, 55, 0) : new Vector3(20, -25, 0));
            foreach (int size in new[] { 1024, 256 })
                Render(camera, Output + label + "/" + names[i] + "-" + size + ".png", size);
        }
        string manifest = "Quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()]
            + "\nCamera=" + camera.transform.position + "\nOrthoSize=" + camera.orthographicSize
            + "\nBounds=" + bounds + "\nLightIntensity=" + light.intensity
            + "\nMaterials=" + string.Join("\n", visible.SelectMany(r => r.sharedMaterials)
                .Select(m => AssetDatabase.GetAssetPath(m)).Distinct());
        File.WriteAllText(Output + label + "/capture.txt", manifest);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (atlasMaterial != null) UnityEngine.Object.DestroyImmediate(atlasMaterial);
        return manifest;
    }

    static void Render(Camera camera, string file, int size)
    {
        var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var pixels = new Texture2D(size, size, TextureFormat.RGB24, false);
        var prior = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(file, pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = prior;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
