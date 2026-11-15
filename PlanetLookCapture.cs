using System;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Evidence-only, issue 1481: stills of the two shader-planet locales before and after the owner's settings
// are written into their materials. Lives in the slot only for the batch run; its home is the evidence branch.
public static class PlanetLookCapture
{
    private const int Width = 1600;
    private const int Height = 900;
    private const int SequenceFrames = 16;
    private const int SequenceGapMs = 750;
    private static readonly string[] Locales = { "VerdantGiant", "BlueCrescent" };
    private static readonly float[] RotationSpeeds = { 0.009f, 0.006f };
    private static readonly string[] Names =
    {
        "_BandStreaming", "_BandCount", "_StormChurn", "_StormCellScale", "_FlowCycle",
        "_RimPower", "_RimIntensity", "_HaloWidth", "_HaloIntensity", "_HaloFalloff"
    };
    private static readonly float[] Values = { 0.008f, 7f, 0.015f, 9.5f, 14f, 5f, 2.5f, 0.03f, 2.95f, 2f };

    public static void Run()
    {
        var outDir = Argument("-planetOut");
        Directory.CreateDirectory(outDir);
        ShaderUtil.allowAsyncCompilation = false;
        var log = new StringWriter();
        foreach (var locale in Locales)
            log.WriteLine(Line(locale + "-before", Render(Stage(locale), Path.Combine(outDir, locale + "-before.png"))));

        for (var index = 0; index < Locales.Length; index++)
        {
            var path = $"Assets/Visuals/Locales/Sky/Locales/{Locales[index]}/Planet.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) throw new InvalidOperationException("missing material " + path);
            for (var i = 0; i < Names.Length; i++)
            {
                if (!material.HasProperty(Names[i])) throw new InvalidOperationException(path + " lacks " + Names[i]);
                material.SetFloat(Names[i], Values[i]);
            }
            material.SetFloat("_RotationSpeed", RotationSpeeds[index]);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();

        foreach (var locale in Locales)
        {
            var camera = Stage(locale);
            log.WriteLine(Line(locale + "-after", Render(camera, Path.Combine(outDir, locale + "-after.png"))));
            for (var frame = 0; frame < SequenceFrames; frame++)
            {
                Thread.Sleep(SequenceGapMs);
                var name = $"{locale}-seq-{frame:00}";
                log.WriteLine(Line(name, Render(camera, Path.Combine(outDir, name + ".png"))));
            }
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        File.WriteAllText(Path.Combine(outDir, "capture-log.txt"), log.ToString());
    }

    private static Camera Stage(string locale)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Instantiate($"Assets/Prefabs/Locales/{locale}.prefab");
        var camera = Instantiate("Assets/Prefabs/Cameras/Main Camera.prefab").GetComponentInChildren<Camera>();
        if (camera == null) throw new InvalidOperationException("the Main Camera prefab has no camera");
        camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
        return camera;
    }

    private static GameObject Instantiate(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("missing prefab " + path);
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    private static float Render(Camera camera, string path)
    {
        var target = new RenderTexture(Width, Height, 24);
        camera.targetTexture = target;
        camera.Render();
        camera.Render();
        var shaderTime = Shader.GetGlobalVector("_Time").y;
        RenderTexture.active = target;
        var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        camera.targetTexture = null;
        File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(image);
        return shaderTime;
    }

    private static string Line(string name, float shaderTime) =>
        name + " shaderTime=" + shaderTime.ToString("F3", CultureInfo.InvariantCulture) +
        " realtime=" + Time.realtimeSinceStartup.ToString("F3", CultureInfo.InvariantCulture);

    private static string Argument(string name)
    {
        var arguments = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(arguments, name);
        if (index < 0 || index + 1 >= arguments.Length) throw new InvalidOperationException("missing argument " + name);
        return arguments[index + 1];
    }
}
