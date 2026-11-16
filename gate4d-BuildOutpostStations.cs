using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Scratch editor tool for gate 4 of #1384: materials, import settings and model prefabs for the three outpost stations.
public static class BuildOutpostStations
{
    const string Root = "Assets/Visuals/Environment/Stations";
    const string Shared = Root + "/Outpost";
    const string SurfaceShader = "Astronomical/Drawn/Surface";
    const string ContourShader = "Astronomical/Drawn/Contour";

    // Linear emission, calibrated so the flight camera's ACES grading and bloom land each light on its accepted colour:
    // window #FEAC39, prize and mod station #FFE25A, armory #FF3B52; the repair green #5BFF86 lands darker at (90,212,103).
    static readonly Vector3 WindowGlow = new(2.45f, 0.30f, 0.003f);
    static readonly Vector3 GoldGlow = new(2.6f, 0.85f, 0f);
    static readonly Vector3 GreenGlow = new(0f, 0.6f, 0.05f);
    static readonly Vector3 RedGlow = new(1.92f, 0.014f, 0.06f);

    static readonly (string fbx, string kind, Vector3 service)[] Stations =
    {
        ("Repair Station", "gimbal", GreenGlow),
        ("Mod Station", "kite", GoldGlow),
        ("Armory", "drum", RedGlow),
    };

    static readonly Dictionary<string, string> Hull = new()
    {
        ["bone"] = "#DDD2BC", ["canvas"] = "#9C927F", ["slate"] = "#3B3F52",
        ["ochre"] = "#BB8925", ["brick"] = "#B0705A", ["blue"] = "#7088A6",
    };

    const string Ink = "#0A0911";
    // Brush strength from the settings block; the Ink mask carries the hatch strength and the marks at full ink.
    const float Brush = 0.86f;
    public static float HullContourPixels = 5f;
    public static float FineContourPixels = 1.6f;
    public static float ShadowThreshold = 0.55f;
    public static float ShadowSoftness = 0.18f;

    static readonly StringBuilder Log = new();

    static Dictionary<string, Material> Materials(out Texture2D glowTex)
    {
        EnsureFolder(Shared);
        EnsureFolder(Shared + "/Materials");
        glowTex = GlowTexture();
        var mats = new Dictionary<string, Material>();
        mats["win"] = LightMaterial($"{Shared}/Materials/Outpost Window.mat", WindowGlow, glowTex);
        mats["core"] = LightMaterial($"{Shared}/Materials/Outpost Prize.mat", GoldGlow, glowTex);
        mats["contour"] = ContourMaterial("Outpost Contour", HullContourPixels, 0.6f, 0f);
        mats["contour fine"] = ContourMaterial("Outpost Contour Fine", FineContourPixels, 1f, 1f);
        return mats;
    }

    public static string BuildMaterials()
    {
        Log.Clear();
        Materials(out _);
        foreach (var (name, _, service) in Stations)
            LightMaterial($"{Root}/{name}/{name} Service.mat", service, GlowTexture());
        AssetDatabase.SaveAssets();
        return Log.ToString();
    }

    public static string ImportStation(string name)
    {
        Log.Clear();
        var mats = Materials(out var glowTex);
        var (_, _, service) = Stations.First(s => s.fbx == name);
        var byName = new Dictionary<string, Material>(mats)
        {
            ["svc"] = LightMaterial($"{Root}/{name}/{name} Service.mat", service, glowTex)
        };
        var (shadow, light, ink) = Masks($"{Root}/{name}");
        foreach (var (key, hex) in Hull)
            byName[key] = PaintMaterial($"{Root}/{name}/{name} {Title(key)}.mat", hex, shadow, light, ink);
        Import($"{Root}/{name}/{name}.fbx", byName);
        AssetDatabase.SaveAssets();
        return Log.ToString();
    }

    public static string PrefabStation(string name)
    {
        Log.Clear();
        var (_, kind, _) = Stations.First(s => s.fbx == name);
        BuildPrefab($"{Root}/{name}", name, $"{Root}/{name}/{name}.fbx", kind);
        AssetDatabase.SaveAssets();
        return Log.ToString();
    }

    public static string RetireSharedHull()
    {
        Log.Clear();
        foreach (var path in Hull.Keys.Select(key => $"{Shared}/Materials/Outpost {Title(key)}.mat")
                     .Append($"{Shared}/Materials/Outpost Ink Mark.mat"))
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) && AssetDatabase.DeleteAsset(path))
                Log.AppendLine("deleted " + path);
        }
        return Log.ToString();
    }

    static (Texture2D shadow, Texture2D light, Texture2D ink) Masks(string folder)
    {
        Texture2D Load(string mask)
        {
            var path = $"{folder}/{mask}.png";
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path) ?? throw new InvalidOperationException(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 1;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return (Load("Shadow"), Load("Light"), Load("Ink"));
    }

    static Material PaintMaterial(string path, string hex, Texture2D shadow, Texture2D light, Texture2D ink)
    {
        var mat = LoadOrCreate(path, SurfaceShader);
        Shading(mat);
        var paper = Hex(hex);
        mat.SetColor("_PaperColor", paper);
        mat.SetTexture("_EmissionMap", null);
        mat.SetColor("_EmissionColor", Color.black);
        mat.SetFloat("_PaintLayers", 1f);
        mat.EnableKeyword("_PAINT_LAYERS");
        mat.SetTexture("_PaintShadowMap", shadow);
        mat.SetTexture("_PaintLightMap", light);
        mat.SetTexture("_PaintInkMap", ink);
        mat.SetFloat("_PaintShadowStrength", Brush);
        mat.SetFloat("_PaintLightStrength", Brush);
        mat.SetFloat("_PaintInkStrength", 1f);
        var lin = paper.linear;
        mat.SetColor("_PaintLightColor", new Color(Mathf.Min(1f, 2f * lin.r), Mathf.Min(1f, 2f * lin.g), Mathf.Min(1f, 2f * lin.b), 1f).gamma);
        mat.SetColor("_PaintInkColor", Hex(Ink));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static string Title(string key) => char.ToUpperInvariant(key[0]) + key.Substring(1);

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : throw new ArgumentException(hex);

    static Texture2D GlowTexture()
    {
        var path = $"{Shared}/Outpost Glow.png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray());
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material LoadOrCreate(string path, string shader)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat) return mat;
        mat = new Material(Shader.Find(shader) ?? throw new InvalidOperationException(shader));
        AssetDatabase.CreateAsset(mat, path);
        Log.AppendLine("created " + path);
        return mat;
    }

    static void Shading(Material mat)
    {
        mat.SetFloat("_TextureStrength", 0f);
        mat.SetFloat("_PigmentPreservation", 0f);
        mat.SetFloat("_PaletteLighting", 1f);
        mat.SetFloat("_AmbientStrength", 0.35f);
        mat.SetColor("_ShadowColor", new Color(0.18f, 0.18f, 0.31f).gamma);
        mat.SetFloat("_ShadowThreshold", ShadowThreshold);
        mat.SetFloat("_ShadowSoftness", ShadowSoftness);
        mat.SetFloat("_CastShadowStrength", 0.96f);
        mat.SetFloat("_SpecularStrength", 0f);
        mat.SetFloat("_LineStrength", 0f);
        mat.SetFloat("_WearStrength", 0f);
    }

    static Material LightMaterial(string path, Vector3 linear, Texture2D glow)
    {
        var mat = LoadOrCreate(path, SurfaceShader);
        Shading(mat);
        mat.SetColor("_PaperColor", Color.black);
        mat.SetTexture("_EmissionMap", glow);
        mat.SetColor("_EmissionColor", new Color(linear.x, linear.y, linear.z, 1f).gamma);
        mat.SetFloat("_EmissionStrength", 1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material ContourMaterial(string name, float pixels, float minimum, float uniform)
    {
        var mat = LoadOrCreate($"{Shared}/Materials/{name}.mat", ContourShader);
        mat.SetColor("_ContourColor", Hex(Ink));
        mat.SetFloat("_ContourPixels", pixels);
        mat.SetFloat("_ContourMinimum", minimum);
        mat.SetFloat("_UniformWidth", uniform);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void Import(string path, Dictionary<string, Material> byName)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path) ?? throw new InvalidOperationException(path);
        importer.bakeAxisConversion = true;
        importer.preserveHierarchy = true;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.None;
        importer.isReadable = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        foreach (var (name, mat) in byName)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
        importer.SaveAndReimport();
        var unmapped = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name).ToArray();
        Log.AppendLine($"imported {path}; embedded (unmapped) materials: {string.Join(", ", unmapped)}");
    }

    static Transform Find(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name)
        ?? throw new InvalidOperationException($"{root.name} has no {name}");

    static void BuildPrefab(string folder, string name, string fbxPath, string kind)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            var stationRoot = Find(go.transform, kind);
            var spins = new List<(Transform part, Vector3 axis, float seconds)>();
            if (kind == "gimbal")
            {
                var ring = Find(go.transform, "Inner ring");
                var axis = Find(go.transform, "Bearing A").position - Find(go.transform, "Bearing B").position;
                spins.Add((ring, ring.InverseTransformDirection(axis.normalized), 60f));
            }
            else
            {
                var (fore, aft) = kind == "kite" ? ("Docking collar fore", "Docking collar aft") : ("Ring fore", "Ring aft");
                var axis = (Find(go.transform, fore).position - Find(go.transform, aft).position).normalized;
                spins.Add((stationRoot, stationRoot.InverseTransformDirection(axis), 60f));
                if (kind == "kite")
                {
                    var prize = Find(go.transform, "Prize");
                    spins.Add((prize, prize.InverseTransformDirection(axis), -20f));
                }
            }

            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            var steady = renderers.Where(r => r.sharedMaterial && (r.sharedMaterial.name == "Outpost Window" || r.sharedMaterial.name == "Outpost Prize")).ToArray();
            var bands = renderers.Where(r => r.name.Contains("/ svc / chase "))
                .GroupBy(r => int.Parse(r.name.Substring(r.name.LastIndexOf(' ') + 1)))
                .OrderBy(g => g.Key).Select(g => g.ToArray()).ToArray();

            var motion = go.AddComponent(Type.GetType("Substrate.Sectors.Elements.Stations.Visual.StationMotion, Core")
                ?? throw new InvalidOperationException("StationMotion type not found"));
            var so = new SerializedObject(motion);
            var spinProp = so.FindProperty("spins");
            spinProp.arraySize = spins.Count;
            for (var i = 0; i < spins.Count; i++)
            {
                var e = spinProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("part").objectReferenceValue = spins[i].part;
                e.FindPropertyRelative("localAxis").vector3Value = spins[i].axis;
                e.FindPropertyRelative("secondsPerTurn").floatValue = spins[i].seconds;
            }
            var steadyProp = so.FindProperty("steadyLights");
            steadyProp.arraySize = steady.Length;
            for (var i = 0; i < steady.Length; i++)
                steadyProp.GetArrayElementAtIndex(i).objectReferenceValue = steady[i];
            var chaseProp = so.FindProperty("chase");
            chaseProp.arraySize = bands.Length;
            for (var b = 0; b < bands.Length; b++)
            {
                var lamps = chaseProp.GetArrayElementAtIndex(b).FindPropertyRelative("lamps");
                lamps.arraySize = bands[b].Length;
                for (var i = 0; i < bands[b].Length; i++)
                    lamps.GetArrayElementAtIndex(i).objectReferenceValue = bands[b][i];
            }
            so.FindProperty("darkColour").colorValue = Hex("#1E2030");
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefabPath = $"{folder}/{name} Model.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            var tris = go.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh ? f.sharedMesh.triangles.Length / 3 : 0);
            Log.AppendLine($"{prefabPath}: spins {string.Join("; ", spins.Select(s => $"{s.part.name} axis {s.axis:F3} {s.seconds}s"))}; " +
                           $"steady {steady.Length}; bands {string.Join("/", bands.Select(b => b.Length))}; renderers {renderers.Length}; tris {tris}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
