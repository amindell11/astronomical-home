// SCRATCH stills capture for #1048 (cloud-bank tiles). Never commit.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class SkyStills
{
    public static void Capture()
    {
        var args = Environment.GetCommandLineArgs();
        var outDir = args[Array.IndexOf(args, "-stillsOut") + 1];
        var tag = args[Array.IndexOf(args, "-stillsTag") + 1];
        var texelsAt = Array.IndexOf(args, "-stillsTexels");
        var texels = texelsAt >= 0 ? int.Parse(args[texelsAt + 1]) : 0;
        Directory.CreateDirectory(outDir);
        foreach (var locale in new[] { "Locale_1", "Locale_3" })
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Scenes/Locales/{locale}.unity", OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            if (texels > 0) Rebake(texels);
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(3840, 2160) })
            foreach (var position in new[] { new Vector2(0, 0), new Vector2(400, -160) })
                Render(locale, tag, outDir, size, position);
        }
        Debug.Log("SKY_STILLS_DONE " + tag);
        EditorApplication.Exit(0);
    }

    public static void AuthorTiles()
    {
        var type = Type.GetType("Substrate.Services.Locales.CloudBankTile, Core");
        foreach (var path in new[] { "Assets/Scenes/Locales/Locale_1.unity", "Assets/Scenes/Locales/Locale_2.unity",
                     "Assets/Scenes/Locales/Locale_3.unity", "Assets/Scenes/EditScene.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var added = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (!material || !material.HasProperty("_CloudOpacity") || material.GetFloat("_CloudOpacity") <= 0) continue;
                if (renderer.GetComponent(type)) continue;
                renderer.gameObject.AddComponent(type);
                added++;
                Debug.Log($"SKY_TILES_ADDED {path} {renderer.name}");
            }
            if (added > 0) EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("SKY_TILES_AUTHORED");
        Capture();
    }

    private static void Rebake(int texels)
    {
        var type = Type.GetType("Substrate.Services.Locales.CloudBankTile, Core");
        var bake = type.GetMethod("Bake", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
        foreach (var tile in UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None))
            bake.Invoke(tile, new object[] { texels });
    }

    private static void Render(string locale, string tag, string outDir, Vector2Int size, Vector2 position)
    {
        var go = new GameObject("StillsCamera");
        var camera = go.AddComponent<Camera>();
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.volumeLayerMask = 1 << LayerMask.NameToLayer("Postprocess");
        camera.orthographic = true;
        camera.orthographicSize = 7;
        camera.cullingMask = 1 << LayerMask.NameToLayer("Sky");
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        go.transform.SetPositionAndRotation(new Vector3(position.x, position.y, -10), Quaternion.LookRotation(Vector3.forward, Vector3.up));
        var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = target;
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
        pixels.Apply();
        RenderTexture.active = previous;
        var file = Path.Combine(outDir, $"{locale}_{tag}_{size.y}p_{position.x:0}_{position.y:0}.png");
        File.WriteAllBytes(file, pixels.EncodeToPNG());
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(pixels);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(go);
    }
}
