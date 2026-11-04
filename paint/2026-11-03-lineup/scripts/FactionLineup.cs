// Evidence-only batch renderer for the faction paint slice (#1329). Copied into the slot's
// Assets/Editor/ for the run and removed afterwards; never committed.
// -executeMethod FactionLineup.Capture -lineupOut <dir>
// Capture setup follows NightshadeLineup.cs (evidence/nightshade 836fd469): identity pose, hull renderers,
// white key Euler(20,-25,0) 1.6 soft shadows, flat ambient 0.24; second pass under Lighting.prefab.
// Rows: today's player paint, then each faction's Crimson, signature and elite signature. Faction skins are
// applied the way a skin slot would: hull material swap by folder, flame material + white start colour.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class FactionLineup
{
    static string output;
    static readonly StringBuilder log = new StringBuilder();
    const string Ships = "Assets/Visuals/Ships/";

    struct Entry { public string Ship, Palette, Flame, Label; public Entry(string s, string p, string f, string l) { Ship = s; Palette = p; Flame = f; Label = l; } }

    static readonly Entry[][] Rows =
    {
        new[] { new Entry("Crimson", null, null, "player"), new Entry("Valis", null, null, "player"), new Entry("Nightshade", null, null, "player"), new Entry("Bulwark", null, null, "player"), new Entry("Vanguard", null, null, "player") },
        new[] { new Entry("Crimson", "shoal", "Shoal", "shoal"), new Entry("Valis", "shoal", "Shoal", "shoal"), new Entry("Valis", "shoal-elite", "Elite", "shoal elite") },
        new[] { new Entry("Crimson", "veil", "Veil", "veil"), new Entry("Nightshade", "veil", "Veil", "veil"), new Entry("Nightshade", "veil-elite", "Elite", "veil elite") },
        new[] { new Entry("Crimson", "bastion", "Bastion", "bastion"), new Entry("Bulwark", "bastion", "Bastion", "bastion"), new Entry("Bulwark", "bastion-elite", "Elite", "bastion elite") },
    };

    public static void Capture()
    {
        var args = Environment.GetCommandLineArgs();
        output = args[Array.IndexOf(args, "-lineupOut") + 1];
        Directory.CreateDirectory(output);
        bool priorAsync = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            log.AppendLine("quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            Grid(false, false);
            Grid(true, false);
            Grid(false, true);
            Grid(true, true);
            File.WriteAllText(Path.Combine(output, "capture.txt"), log.ToString());
        }
        catch (Exception e)
        {
            log.AppendLine("EXCEPTION " + e);
            File.WriteAllText(Path.Combine(output, "capture.txt"), log.ToString());
            throw;
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = priorAsync;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    static Material Swap(Material m, Entry e)
    {
        if (!m || e.Palette == null) return m;
        string path = AssetDatabase.GetAssetPath(m);
        string file = Path.GetFileName(path);
        string candidate = null;
        if (e.Ship == "Crimson" && file == "Hull paint.mat") candidate = Ships + "Crimson/Materials/" + e.Palette + "/Hull paint.mat";
        else if (path.StartsWith(Ships + e.Ship + "/Materials/") && path.Count(c => c == '/') == 6) candidate = Ships + e.Ship + "/Materials/" + e.Palette + "/" + file;
        if (candidate == null) return m;
        var swapped = AssetDatabase.LoadAssetAtPath<Material>(candidate);
        if (swapped) log.AppendLine("  " + e.Ship + "/" + e.Palette + ": " + path + " -> " + candidate);
        return swapped ? swapped : m;
    }

    static void Spawn(Entry e, Vector3 position, List<Renderer> visible, bool flames)
    {
        var ship = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/" + e.Ship + ".prefab"));
        ship.transform.position = position;
        ship.transform.rotation = Quaternion.identity;
        foreach (var canvas in ship.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
        var flameMaterial = e.Flame == null ? null : AssetDatabase.LoadAssetAtPath<Material>(Ships + "_Shared/ThrusFlame_" + e.Flame + ".mat");
        foreach (var particles in ship.GetComponentsInChildren<ParticleSystem>(true))
        {
            bool thrust = particles.name.StartsWith("Thrust");
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (flames && thrust)
            {
                if (flameMaterial)
                {
                    renderer.sharedMaterial = flameMaterial;
                    var main = particles.main;
                    main.startColor = Color.white;
                }
                particles.Simulate(1.5f, false, true);
            }
            else particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach (var renderer in ship.GetComponentsInChildren<Renderer>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(renderer.transform, ship.transform);
            bool hull = path.StartsWith("ShipBaseRig/Hull/");
            bool flame = flames && renderer is ParticleSystemRenderer && renderer.name.StartsWith("Thrust");
            renderer.enabled = hull || flame;
            if (hull)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Swap(m, e)).ToArray();
                visible.Add(renderer);
            }
        }
    }

    static void Grid(bool gameLight, bool flames)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.24f, .24f, .24f);
        RenderSettings.fog = false;
        var visible = new List<Renderer>();
        const float dx = 3.6f, dy = 4.2f;
        for (int r = 0; r < Rows.Length; r++)
            for (int c = 0; c < Rows[r].Length; c++)
                Spawn(Rows[r][c], new Vector3((c - 2f) * dx, -r * dy, 0), visible, flames);
        var bounds = visible[0].bounds;
        foreach (var v in visible) bounds.Encapsulate(v.bounds);
        bounds.Encapsulate(new Vector3(-2f * dx - 1.6f, 0, 0));
        bounds.Encapsulate(new Vector3(2f * dx + 1.6f, 0, 0));
        if (gameLight)
        {
            var lighting = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/Lighting.prefab"));
            foreach (var light in lighting.GetComponentsInChildren<Light>(true)) light.shadows = LightShadows.None;
            RenderSettings.ambientLight = new Color(.42f, .44f, .48f);
        }
        else
        {
            var light = new GameObject("LineupLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(20, -25, 0);
            RenderSettings.sun = light;
        }
        float aspect = 5f / 4f;
        var camera = new GameObject("LineupCamera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.aspect = aspect;
        camera.orthographicSize = Mathf.Max(bounds.extents.x / aspect, bounds.extents.y) * 1.08f;
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -30);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .04f, .05f);
        camera.nearClipPlane = .01f;
        camera.farClipPlane = 100;
        camera.allowHDR = false;
        string name = "factions" + (gameLight ? "-game-light" : "") + (flames ? "-flames" : "");
        foreach (int width in new[] { 2500, 500 })
            Render(camera, Path.Combine(output, name + "-" + width + ".png"), width, width * 4 / 5);
        log.AppendLine(name + ": rows " + string.Join(" / ", Rows.Select(row => string.Join(", ", row.Select(e => e.Ship + "(" + e.Label + ")")))) + "; ortho " + camera.orthographicSize);
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
