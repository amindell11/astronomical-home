// Evidence-only batch renderer for the Nightshade paint conversion (#868). Copied into the slot's
// Assets/Editor/ for the run and removed afterwards; never committed.
// -executeMethod NightshadeLineup.Capture -lineupOut <dir> [-lineupVariants]
// Fleet lineup and Nightshade top view follow Crimson's conversion captures (evidence/crimson,
// paint/2026-10-19-grayscale/CrimsonLineup.cs and CrimsonPaintCapture.cs): identity pose, hull
// renderers only, white key at Euler(20,-25,0) intensity 1.6 with soft shadows, flat ambient 0.24,
// Performant quality. Variants are in-memory material clones of the imported Nightshade materials.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class NightshadeLineup
{
    static string output;
    static readonly StringBuilder log = new StringBuilder();
    static readonly string[] Fleet = { "Valis", "Crimson", "Vanguard", "Nightshade" };
    const string NightshadeMaterials = "Assets/Visuals/Ships/Nightshade/Materials/";

    public static void Capture()
    {
        var args = Environment.GetCommandLineArgs();
        output = args[Array.IndexOf(args, "-lineupOut") + 1];
        bool variants = Array.IndexOf(args, "-lineupVariants") >= 0;
        Directory.CreateDirectory(output);
        bool priorAsync = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            log.AppendLine("quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            FleetLineup(false);
            FleetLineup(true);
            ShipTop("nightshade", null);
            if (variants)
            {
                ShipTop("production-numbers", ProductionNumbers);
                ShipTop("paper-proposal", PaperProposal);
                ShipTop("light-colours", LightColours);
                int masksArg = Array.IndexOf(args, "-lineupMasks");
                string masks = masksArg >= 0 ? args[masksArg + 1] : null;
                if (masks != null) ShipTop("signed-space", m => LoadMasks(m, masks));
                ShipTop("nightshade-game-light", null, true);
                ShipTop("production-numbers-game-light", ProductionNumbers, true);
                ShipTop("paper-proposal-game-light", PaperProposal, true);
                ShipTop("light-colours-game-light", LightColours, true);
                if (masks != null) ShipTop("signed-space-game-light", m => LoadMasks(m, masks), true);
                foreach (var texture in loadedMasks) UnityEngine.Object.DestroyImmediate(texture);
                loadedMasks.Clear();
                maskCache.Clear();
            }
            File.WriteAllText(Path.Combine(output, "capture.txt"), log.ToString());
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = priorAsync;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    static GameObject Spawn(string name, Vector3 position, List<Renderer> visible)
    {
        var ship = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/" + name + ".prefab"));
        ship.transform.position = position;
        ship.transform.rotation = Quaternion.identity;
        foreach (var canvas in ship.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
        foreach (var particles in ship.GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach (var renderer in ship.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = AnimationUtility.CalculateTransformPath(renderer.transform, ship.transform).StartsWith("ShipBaseRig/Hull/");
            if (renderer.enabled) visible.Add(renderer);
        }
        return ship;
    }

    static void NewScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.24f, .24f, .24f);
        RenderSettings.fog = false;
    }

    static Light KeyLight()
    {
        var light = new GameObject("LineupLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.6f;
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(20, -25, 0);
        RenderSettings.sun = light;
        return light;
    }

    static Camera CaptureCamera(Bounds bounds, float aspect)
    {
        var camera = new GameObject("LineupCamera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.aspect = aspect;
        camera.orthographicSize = Mathf.Max(bounds.extents.x / aspect, bounds.extents.y) * 1.18f;
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -30);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .04f, .05f);
        camera.nearClipPlane = .01f;
        camera.farClipPlane = 100;
        camera.allowHDR = false;
        return camera;
    }

    static Bounds Encapsulate(List<Renderer> visible)
    {
        var bounds = visible[0].bounds;
        foreach (var renderer in visible) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static void FleetLineup(bool gameLight)
    {
        NewScene();
        var visible = new List<Renderer>();
        for (int i = 0; i < Fleet.Length; i++) Spawn(Fleet[i], new Vector3((i - 1.5f) * 3.2f, 0, 0), visible);
        var bounds = Encapsulate(visible);
        string suffix = gameLight ? "-game-light" : "";
        if (gameLight) GameLight(); else KeyLight();
        var camera = CaptureCamera(bounds, 4f);
        foreach (int width in new[] { 2048, 1024 })
            Render(camera, Path.Combine(output, "fleet" + suffix + "-" + width + ".png"), width, width / 4);
        log.AppendLine("fleet" + suffix + ": left to right " + string.Join(", ", Fleet) + "; unchanged prefab scales; ortho " + camera.orthographicSize);
    }

    static void GameLight()
    {
        var lighting = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/Lighting.prefab"));
        foreach (var light in lighting.GetComponentsInChildren<Light>(true)) light.shadows = LightShadows.None;
        RenderSettings.ambientLight = new Color(.42f, .44f, .48f);
    }

    static void ShipTop(string label, Action<Material> adjust, bool gameLight = false)
    {
        NewScene();
        var visible = new List<Renderer>();
        Spawn("Nightshade", Vector3.zero, visible);
        var clones = new List<Material>();
        if (adjust != null)
        {
            var cloneByPath = new Dictionary<string, Material>();
            foreach (var renderer in visible)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                {
                    string path = m ? AssetDatabase.GetAssetPath(m) : "";
                    if (!path.StartsWith(NightshadeMaterials) || m.name == "Contour") return m;
                    if (!cloneByPath.TryGetValue(path, out var clone))
                    {
                        clone = new Material(m) { name = m.name, hideFlags = HideFlags.DontSave };
                        adjust(clone);
                        cloneByPath[path] = clone;
                        clones.Add(clone);
                    }
                    return clone;
                }).ToArray();
        }
        var bounds = Encapsulate(visible);
        if (gameLight) GameLight(); else KeyLight();
        var camera = CaptureCamera(bounds, 1f);
        foreach (int size in new[] { 1024, 256 })
            Render(camera, Path.Combine(output, label + "-" + size + ".png"), size, size);
        log.AppendLine(label + ": ortho " + camera.orthographicSize + "; materials " + string.Join(" | ",
            visible.SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => AssetDatabase.GetAssetPath(m)).Where(p => p != "").Distinct()));
        foreach (var clone in clones) UnityEngine.Object.DestroyImmediate(clone);
    }

    static readonly List<Texture2D> loadedMasks = new List<Texture2D>();
    static readonly Dictionary<string, Texture2D> maskCache = new Dictionary<string, Texture2D>();

    // Another bake's masks (a layers/ folder) on the imported materials: linear, mipmapped, clamped.
    static void LoadMasks(Material m, string folder)
    {
        foreach (var mask in new[] { "Shadow", "Light", "Ink", "Emission" })
        {
            string file = Path.Combine(folder, mask + ".png");
            if (!File.Exists(file)) continue;
            if (!maskCache.TryGetValue(file, out var texture))
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                texture.LoadImage(File.ReadAllBytes(file));
                texture.Apply(true, false);
                maskCache[file] = texture;
                loadedMasks.Add(texture);
            }
            m.SetTexture(mask == "Emission" ? "_EmissionMap" : "_Paint" + mask + "Map", texture);
        }
    }

    // Round 2 depth-study variant (c): today's production material numbers on the tool's masks.
    static void ProductionNumbers(Material m)
    {
        m.SetFloat("_PaintShadowStrength", 1.531f);
        m.SetFloat("_PaintSaturation", 1.25f);
        m.SetFloat("_PaletteLighting", 0.954f);
        m.SetFloat("_ShadowSoftness", 0.263f);
        m.SetFloat("_ShadowThreshold", -0.063f);
        m.SetFloat("_AmbientStrength", 0.486f);
        m.SetFloat("_CastShadowStrength", 0.213f);
        m.SetColor("_ShadowColor", new Color(0.9162735f, 0.93574095f, 0.9776915f));
    }

    // Round 2 base-coat study: proposed _PaperColor (sRGB) on three regions, masks unchanged.
    static void PaperProposal(Material m)
    {
        switch (m.name)
        {
            case "Nightshade Midnight": m.SetColor("_PaperColor", new Color(.64f, .72f, .84f)); break;
            case "Nightshade Violet": m.SetColor("_PaperColor", new Color(.32f, .36f, .54f)); break;
            case "Nightshade Recess": m.SetColor("_PaperColor", new Color(.12f, .15f, .22f)); break;
        }
    }

    // Main's per-region painted-light colours before the conversion; Midnight keeps the tool's lift
    // because its production value was darker than its base coat.
    static void LightColours(Material m)
    {
        switch (m.name)
        {
            case "Nightshade Violet": m.SetColor("_PaintLightColor", new Color(0.7798742f, 0.7798742f, 0.7798742f)); break;
            case "Nightshade Plum": m.SetColor("_PaintLightColor", new Color(0.7490195f, 0.43921563f, 0.87450975f)); break;
            case "Nightshade Edge": m.SetColor("_PaintLightColor", new Color(0.32156855f, 0.39607838f, 0.5098039f)); break;
            case "Nightshade Recess": m.SetColor("_PaintLightColor", new Color(0.06666665f, 0.10980389f, 0.17254901f)); break;
            case "Nightshade Glass": m.SetColor("_PaintLightColor", new Color(0.78823525f, 0.74509794f, 1f)); break;
        }
    }

    static void Render(Camera camera, string file, int width, int height)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        var prior = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
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
