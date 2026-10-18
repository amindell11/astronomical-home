// Evidence-only lineup for #1014; slot-local, never committed. Row 0: today's chassis prefabs.
// Row 1: the ship_paint bundles on the re-exported role FBX. Renders two top-down PNGs.
using System;
using System.IO;
using System.Linq;
using Substrate;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PaintPreviewLineup
{
    private const string Preview = "Assets/Visuals/Ships/_PaintPreview";
    private static readonly string[] Ships = { "Crimson", "Nightshade", "Vanguard", "Valis" };
    private static readonly string[] Painted = { "Crimson", "Nightshade", "Vanguard" };
    // Single-slot atlas materials whose base map the composite replaces.
    private static readonly string[] AtlasSlots = { "Hull paint", "Vanguard vivid paint" };

    public static void Render()
    {
        var args = Environment.GetCommandLineArgs();
        string outDir = null;
        for (var i = 0; i < args.Length - 1; i++) if (args[i] == "-paintPreviewOut") outDir = args[i + 1];
        if (string.IsNullOrEmpty(outDir)) throw new InvalidOperationException("-paintPreviewOut missing");
        Directory.CreateDirectory(outDir);
        var log = new System.Text.StringBuilder();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var contour = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Ships/Nightshade/Materials/Contour.mat");

        // The game plane: ships lie in XY, the flight camera sits at -Z looking +Z with +Y up.
        var normal = GamePlane.Normal;
        var forward = GamePlane.Forward;
        var right = GamePlane.Right;

        // -paintPreviewLight game: the game's lights (shadow maps off, since no shadow pass runs here);
        // neutral: one white key light from the camera's side. Both with a flat ambient.
        var neutral = false;
        for (var i = 0; i < args.Length - 1; i++) if (args[i] == "-paintPreviewLight") neutral = args[i + 1] == "neutral";
        if (neutral)
        {
            var key = new GameObject("lineup light").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.2f;
            key.color = Color.white;
            key.transform.rotation = Quaternion.LookRotation((normal + right * 0.35f + forward * 0.25f).normalized, forward);
            key.shadows = LightShadows.None;
        }
        else
        {
            var lighting = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/Lighting.prefab");
            var lights = (GameObject)PrefabUtility.InstantiatePrefab(lighting);
            foreach (var light in lights.GetComponentsInChildren<Light>(true)) light.shadows = LightShadows.None;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f);

        // Today's prefabs, one column each; Crimson's FBX hull orients the painted row.
        var today = new GameObject[Ships.Length];
        var extents = new float[Ships.Length];
        Transform basis = null;
        for (var i = 0; i < Ships.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Ships/{Ships[i]}.prefab");
            today[i] = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            today[i].name = Ships[i] + " today";
            // The spawner poses a chassis root on the game plane; a prefab's stored root rotation is not the flight pose.
            today[i].transform.SetPositionAndRotation(Vector3.zero, GamePlane.Rotation);
            today[i].SetActive(true);
            foreach (var t in today[i].GetComponentsInChildren<Transform>(true)) t.gameObject.SetActive(true);
            foreach (var r in today[i].GetComponentsInChildren<Renderer>(true)) r.enabled = IsHull(r);
            foreach (var canvas in today[i].GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            var largest = Largest(today[i]);
            if (basis == null) basis = largest.transform;
            extents[i] = MeshExtent(today[i]);
            log.AppendLine($"{Ships[i]} today: hull renderers={Hulls(today[i]).Length} extent={extents[i]:F2} largest={largest.name} up={largest.transform.up} fwd={largest.transform.forward}");
            foreach (var r in today[i].GetComponentsInChildren<Renderer>(true))
                log.AppendLine($"    {(r.enabled ? "shown" : "hidden")} {r.name} ({r.GetType().Name}, {r.sharedMaterial?.shader?.name})");
        }
        var spacing = extents.Max() * 1.25f;
        for (var i = 0; i < Ships.Length; i++)
        {
            today[i].transform.position += right * (i * spacing) - Centre(today[i]);
            foreach (var r in Hulls(today[i])) r.sharedMaterials = r.sharedMaterials.Select(m => Lineup(m)).ToArray();
        }

        // Painted: a copy of today's instance one row below, materials swapped. Nightshade's hull already
        // carries the region slots, so it binds the bundle's _PaintLayers materials; Crimson's and
        // Vanguard's single-slot meshes show the tool's composite as their base map instead.
        for (var i = 0; i < Ships.Length; i++)
        {
            if (!Painted.Contains(Ships[i])) continue;
            var painted = UnityEngine.Object.Instantiate(today[i]);
            painted.name = Ships[i] + " painted";
            painted.transform.position += -forward * spacing;
            var paletteDir = Directory.GetDirectories(Path.Combine(Preview, Ships[i], "Materials")).First().Replace('\\', '/');
            var composite = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Preview}/{Ships[i]}/Composite.png");
            foreach (var renderer in Hulls(painted))
            {
                if (renderer.name == "Vanguard structural ink") { renderer.enabled = false; continue; }
                var materials = renderer.sharedMaterials;
                for (var m = 0; m < materials.Length; m++)
                {
                    var name = materials[m] != null ? materials[m].name : "";
                    var bundle = AssetDatabase.LoadAssetAtPath<Material>($"{paletteDir}/{name}.mat");
                    if (bundle != null) materials[m] = Lineup(bundle);
                    else if (composite != null && AtlasSlots.Contains(name)) materials[m] = Lineup(materials[m], composite);
                    else continue;
                    log.AppendLine($"  {Ships[i]} {renderer.name}[{m}] '{name}' -> {(bundle != null ? "bundle material" : "composite base map")}");
                }
                renderer.sharedMaterials = materials;
            }
        }

        var centre = right * (spacing * (Ships.Length - 1) / 2f) - forward * (spacing / 2f);
        Shoot(Path.Combine(outDir, "lineup-large.png"), centre, normal, forward, spacing * Ships.Length / 2f * 0.95f, 4000, 2000);
        Shoot(Path.Combine(outDir, "lineup-game-scale.png"), centre, normal, forward, spacing * Ships.Length / 2f * 0.95f, 500, 250);
        File.WriteAllText(Path.Combine(outDir, "lineup.log"), log.ToString());
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    // A lineup copy of a material: no cast-shadow dependence, optionally with the composite as base map.
    private static Material Lineup(Material source, Texture baseMap = null)
    {
        var copy = new Material(source) { name = source.name, hideFlags = HideFlags.DontSave };
        if (copy.HasProperty("_CastShadowStrength")) copy.SetFloat("_CastShadowStrength", 0f);
        if (baseMap != null)
        {
            copy.SetTexture("_BaseMap", baseMap);
            copy.SetFloat("_TextureStrength", 1f);
        }
        return copy;
    }

    private static float Extent(Renderer r)
    {
        var size = r.bounds.size;
        return Mathf.Max(size.x, size.y, size.z);
    }

    private static Renderer Largest(GameObject root) => Hulls(root).OrderByDescending(Extent).First();

    private static float MeshExtent(GameObject root) => Extent(Largest(root));

    private static Vector3 Centre(GameObject root) => Largest(root).bounds.center;

    private static readonly string[] HullShaders =
        { "Astronomical/Drawn/Surface", "Astronomical/Drawn/Canopy", "Astronomical/Drawn/Contour" };

    private static bool IsHull(Renderer r) =>
        (r is MeshRenderer || r is SkinnedMeshRenderer) && r.name != "Collider" && r.sharedMaterial != null
        && r.sharedMaterial.shader != null && HullShaders.Contains(r.sharedMaterial.shader.name);

    private static Renderer[] Hulls(GameObject root) =>
        root.GetComponentsInChildren<Renderer>().Where(r => r.enabled && IsHull(r)).ToArray();

    private static void Shoot(string path, Vector3 centre, Vector3 normal, Vector3 forward, float halfWidth, int w, int h)
    {
        var go = new GameObject("lineup camera");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = halfWidth * h / w;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 400f;
        go.transform.position = centre - normal * 100f;
        go.transform.rotation = Quaternion.LookRotation(normal, forward);
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        File.WriteAllBytes(path, ImageConversion.EncodeToPNG(tex));
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(go);
    }
}
