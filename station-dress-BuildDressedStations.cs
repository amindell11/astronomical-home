using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Substrate.Sectors.Elements.Stations;
using Substrate.Sectors.Elements.Stations.Visual;
using Substrate.Sectors.Factions;
using UnityEditor;
using UnityEngine;

// Scratch editor tool for #1593, extending gate4d-BuildOutpostStations.cs (#1587): the nine faction-dressed
// station models (materials, import, model prefabs), StationModel on the three plain models, each station
// body's model reference, the faction station dress from fd7-dress-colour.json, and the service lights.
public static class BuildDressedStations
{
    const string Root = "Assets/Visuals/Environment/Stations";
    const string Shared = Root + "/Outpost";
    const string SurfaceShader = "Astronomical/Drawn/Surface";
    const string Ink = "#0A0911";
    static readonly string[] Roles = { "bone", "canvas", "slate", "ochre", "brick", "blue" };

    static readonly (string name, StationKind kind, string prefab)[] Kinds =
    {
        ("Repair Station", StationKind.Repair, "Assets/Prefabs/Objectives/Repair Station.prefab"),
        ("Mod Station", StationKind.Mod, "Assets/Prefabs/Objectives/Mod Station.prefab"),
        ("Armory", StationKind.Armory, "Assets/Prefabs/Objectives/Armory.prefab"),
    };

    // fd7-dress-colour.json on evidence/outpost-kit at 9823b48: name, six hull roles, window target, brush.
    static readonly Dictionary<string, (string name, string[] hull, string window, float brush)[]> Palettes = new()
    {
        ["Shoal"] = new[]
        {
            ("Shoal", new[] { "#A8714A", "#6E4B36", "#261E1B", "#E8862A", "#D8C29A", "#5F7468" }, "#FF9548", 1.2f),
        },
        ["Veil"] = new[]
        {
            ("Veil", new[] { "#525864", "#3A3F4A", "#111216", "#2B2F37", "#9AA3B3", "#3D4866" }, "#BA82FF", 0.45f),
        },
        ["Bastion"] = new[]
        {
            ("Red hull", new[] { "#7A2734", "#561A26", "#241F22", "#EBCB9E", "#4A1520", "#B8A07E" }, "#D7E6FF", 0.7f),
            ("Bone hull", new[] { "#EBCFA4", "#BFA57C", "#241F22", "#8A2A38", "#6A1E2A", "#561A26" }, "#D7E6FF", 0.7f),
        },
    };

    // Linear emission per light target, calibrated against the flight camera's grading and bloom. Filled from the
    // calibration run; until then the target's own linear colour.
    public static readonly Dictionary<string, Vector3> Emission = new();

    static readonly StringBuilder Log = new();

    public static void Run()
    {
        try
        {
            Log.Clear();
            LoadCalibration();
            foreach (var (name, _, _) in Kinds)
                foreach (var faction in Palettes.Keys)
                {
                    ImportDressed(name, faction);
                    PrefabDressed(name, faction);
                }
            foreach (var (name, _, prefab) in Kinds)
            {
                WirePlainModel(name);
                WireBody(prefab);
            }
            foreach (var faction in Palettes.Keys)
                FillFaction(faction);
            Service();
            AssetDatabase.SaveAssets();
            Log.AppendLine("DONE");
        }
        catch (Exception e)
        {
            Log.AppendLine("FAILED " + e);
            throw;
        }
        finally
        {
            File.WriteAllText(LogPath, Log.ToString());
        }
    }

    public static string LogPath =>
        Environment.GetEnvironmentVariable("DRESS_LOG") ?? Path.Combine(Path.GetTempPath(), "build-dressed-stations.log");

    // DRESS_CALIBRATION names a JSON file of {"target hex": [r, g, b]} linear emissions.
    static void LoadCalibration()
    {
        Emission.Clear();
        var path = Environment.GetEnvironmentVariable("DRESS_CALIBRATION");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Log.AppendLine("no calibration: emissions are the targets' linear colours");
            return;
        }
        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4) continue;
            Emission[parts[0].ToUpperInvariant()] = new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
        }
        Log.AppendLine($"calibration: {string.Join("; ", Emission.Select(e => $"{e.Key} {e.Value:F4}"))}");
    }

    public static Vector3 EmissionFor(string hex)
    {
        if (Emission.TryGetValue(hex.ToUpperInvariant(), out var calibrated)) return calibrated;
        var lin = Hex(hex).linear;
        return new Vector3(lin.r, lin.g, lin.b);
    }

    static string Folder(string name, string faction) => $"{Root}/{name}/{faction}";

    static string Title(string key) => char.ToUpperInvariant(key[0]) + key.Substring(1);

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : throw new ArgumentException(hex);

    static void ImportDressed(string name, string faction)
    {
        var folder = Folder(name, faction);
        var first = Palettes[faction][0];
        var byName = new Dictionary<string, Material>
        {
            ["win"] = Load($"{Shared}/Materials/Outpost Window.mat"),
            ["core"] = Load($"{Shared}/Materials/Outpost Prize.mat"),
            ["contour"] = Load($"{Shared}/Materials/Outpost Contour.mat"),
            ["contour fine"] = Load($"{Shared}/Materials/Outpost Contour Fine.mat"),
            ["svc"] = Load($"{Root}/{name}/{name} Service.mat"),
        };
        var (shadow, light, ink) = Masks(folder);
        var template = Load($"{Root}/{name}/{name} Bone.mat");
        for (var i = 0; i < Roles.Length; i++)
            byName[Roles[i]] = PaintMaterial($"{folder}/{name} {faction} {Title(Roles[i])}.mat", template, first.hull[i],
                first.brush, shadow, light, ink);
        Import($"{folder}/{name} {faction}.fbx", byName);
    }

    static Material Load(string path) =>
        AssetDatabase.LoadAssetAtPath<Material>(path) ?? throw new InvalidOperationException("missing " + path);

    static (Texture2D shadow, Texture2D light, Texture2D ink) Masks(string folder)
    {
        Texture2D Mask(string mask)
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
        return (Mask("Shadow"), Mask("Light"), Mask("Ink"));
    }

    // A copy of the plain station's role material, so shading values stay the plain one's, with this model's masks,
    // the faction's first palette colour and its brush.
    static Material PaintMaterial(string path, Material template, string hex, float brush, Texture2D shadow,
        Texture2D light, Texture2D ink)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(template);
            AssetDatabase.CreateAsset(mat, path);
            Log.AppendLine("created " + path);
        }
        else
        {
            mat.CopyPropertiesFromMaterial(template);
        }
        var paper = Hex(hex);
        mat.SetColor("_PaperColor", paper);
        mat.SetTexture("_PaintShadowMap", shadow);
        mat.SetTexture("_PaintLightMap", light);
        mat.SetTexture("_PaintInkMap", ink);
        mat.SetFloat("_PaintShadowStrength", brush);
        mat.SetFloat("_PaintLightStrength", brush);
        mat.SetFloat("_PaintInkStrength", 1f);
        var lin = paper.linear;
        mat.SetColor("_PaintLightColor",
            new Color(Mathf.Min(1f, 2f * lin.r), Mathf.Min(1f, 2f * lin.g), Mathf.Min(1f, 2f * lin.b), 1f).gamma);
        mat.SetColor("_PaintInkColor", Hex(Ink));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void Import(string path, Dictionary<string, Material> byName)
    {
        AssetDatabase.ImportAsset(path);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path) ?? throw new InvalidOperationException(path);
        var plain = (ModelImporter)AssetImporter.GetAtPath($"{Root}/{Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)))}/" +
                                                            $"{Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)))}.fbx");
        importer.globalScale = plain ? plain.globalScale : 1f;
        importer.useFileScale = plain ? plain.useFileScale : true;
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
        Log.AppendLine($"imported {path}; scale {importer.globalScale}; embedded (unmapped) materials: {string.Join(", ", unmapped)}");
    }

    // The dressed export names the station root "<kind>-<faction>-kit" where the plain one is "<kind>".
    static Transform Find(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name)
        ?? root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(name + "-") && t.name.EndsWith("-kit"))
        ?? throw new InvalidOperationException($"{root.name} has no {name}");

    static void PrefabDressed(string name, string faction)
    {
        var folder = Folder(name, faction);
        var plainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{name}/{name} Model.prefab");
        var plainMotion = plainPrefab.GetComponent<StationMotion>();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name} {faction}.fbx");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            var motion = go.AddComponent<StationMotion>();
            using (var plain = new SerializedObject(plainMotion))
            using (var so = new SerializedObject(motion))
            {
                so.FindProperty("feel").objectReferenceValue = plain.FindProperty("feel").objectReferenceValue;
                var plainSpins = plain.FindProperty("spins");
                var spins = so.FindProperty("spins");
                spins.arraySize = plainSpins.arraySize;
                for (var i = 0; i < plainSpins.arraySize; i++)
                {
                    var from = plainSpins.GetArrayElementAtIndex(i);
                    var to = spins.GetArrayElementAtIndex(i);
                    var part = (Transform)from.FindPropertyRelative("part").objectReferenceValue;
                    to.FindPropertyRelative("part").objectReferenceValue = Find(go.transform, part.name);
                    to.FindPropertyRelative("localAxis").vector3Value = from.FindPropertyRelative("localAxis").vector3Value;
                    to.FindPropertyRelative("rate").floatValue = from.FindPropertyRelative("rate").floatValue;
                }

                var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
                var steady = renderers.Where(r => r.sharedMaterial &&
                                                  (r.sharedMaterial.name == "Outpost Window" || r.sharedMaterial.name == "Outpost Prize")).ToArray();
                var bands = renderers.Where(r => r.name.Contains("/ svc / chase "))
                    .GroupBy(r => int.Parse(r.name.Substring(r.name.LastIndexOf(' ') + 1)))
                    .OrderBy(g => g.Key).Select(g => g.ToArray()).ToArray();
                SetArray(so.FindProperty("steadyLights"), steady);
                var chase = so.FindProperty("chase");
                chase.arraySize = bands.Length;
                for (var b = 0; b < bands.Length; b++)
                    SetArray(chase.GetArrayElementAtIndex(b).FindPropertyRelative("lamps"), bands[b]);
                so.ApplyModifiedPropertiesWithoutUndo();
                Log.AppendLine($"{name} {faction}: spins {spins.arraySize}; steady {steady.Length}; bands {string.Join("/", bands.Select(b => b.Length))}");
            }

            WireRoles(go, r => r.StartsWith($"{name} {faction} "), $"{name} {faction} ");
            var path = $"{folder}/{name} {faction} Model.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Log.AppendLine("saved " + path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void SetArray(SerializedProperty property, Renderer[] renderers)
    {
        property.arraySize = renderers.Length;
        for (var i = 0; i < renderers.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
    }

    // Role renderers by their role material, "<prefix><Role>"; windows by the shared window material.
    static void WireRoles(GameObject go, Func<string, bool> isRole, string prefix)
    {
        var model = go.GetComponent<StationModel>();
        if (!model) model = go.AddComponent<StationModel>();
        var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
        using var so = new SerializedObject(model);
        var counts = new List<string>();
        foreach (var role in Roles)
        {
            var matched = renderers.Where(r => r.sharedMaterial && r.sharedMaterial.name == prefix + Title(role)).ToArray();
            SetArray(so.FindProperty(role), matched);
            counts.Add($"{role} {matched.Length}");
        }
        var windows = renderers.Where(r => r.sharedMaterial && r.sharedMaterial.name == "Outpost Window").ToArray();
        SetArray(so.FindProperty("windows"), windows);
        so.ApplyModifiedPropertiesWithoutUndo();
        var unassigned = renderers.Where(r => r.sharedMaterial && isRole(r.sharedMaterial.name) &&
                                              !Roles.Any(role => r.sharedMaterial.name == prefix + Title(role))).ToArray();
        Log.AppendLine($"  roles: {string.Join(", ", counts)}, windows {windows.Length}; unassigned role-looking {unassigned.Length}");
    }

    static void WirePlainModel(string name)
    {
        var path = $"{Root}/{name}/{name} Model.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Log.AppendLine($"plain {name}:");
            WireRoles(root, r => r.StartsWith($"{name} "), $"{name} ");
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void WireBody(string prefabPath)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var body = root.GetComponentInChildren<FindStationBody>(true);
            var models = body.GetComponentsInChildren<StationModel>(true);
            if (models.Length != 1) throw new InvalidOperationException($"{prefabPath}: {models.Length} station models");
            using var so = new SerializedObject(body);
            so.FindProperty("model").objectReferenceValue = models[0];
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Log.AppendLine($"{prefabPath}: body model -> {models[0].name}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void FillFaction(string faction)
    {
        var path = $"Assets/Settings/Factions/{faction}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<Faction>(path) ?? throw new InvalidOperationException(path);
        using var so = new SerializedObject(asset);
        var dress = so.FindProperty("stationDress");
        var palettes = dress.FindPropertyRelative("palettes");
        var list = Palettes[faction];
        palettes.arraySize = list.Length;
        for (var p = 0; p < list.Length; p++)
        {
            var (name, hull, window, brush) = list[p];
            var element = palettes.GetArrayElementAtIndex(p);
            element.FindPropertyRelative("name").stringValue = name;
            for (var i = 0; i < Roles.Length; i++)
                element.FindPropertyRelative(Roles[i]).colorValue = Hex(hull[i]);
            var glow = EmissionFor(window);
            element.FindPropertyRelative("windowLight").colorValue = new Color(glow.x, glow.y, glow.z, 1f).gamma;
            element.FindPropertyRelative("brush").floatValue = brush;
        }
        foreach (var (name, kind, _) in Kinds)
        {
            var model = AssetDatabase.LoadAssetAtPath<StationModel>($"{Folder(name, faction)}/{name} {faction} Model.prefab")
                        ?? throw new InvalidOperationException($"no {faction} {name} model");
            var field = kind switch { StationKind.Repair => "repair", StationKind.Mod => "mod", _ => "armory" };
            dress.FindPropertyRelative(field).objectReferenceValue = model;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        Log.AppendLine($"{path}: {list.Length} palettes, windows {string.Join(" ", list.Select(l => EmissionFor(l.window).ToString("F4")))}");
    }

    // Each kind's service light takes its find colour, as calibrated emission.
    static void Service()
    {
        foreach (var (name, kind, _) in Kinds)
        {
            var hex = "#" + ColorUtility.ToHtmlStringRGB(FindColours.Of(kind));
            var mat = Load($"{Root}/{name}/{name} Service.mat");
            var glow = EmissionFor(hex);
            mat.SetColor("_EmissionColor", new Color(glow.x, glow.y, glow.z, 1f).gamma);
            EditorUtility.SetDirty(mat);
            Log.AppendLine($"{name} Service: {hex} -> {glow:F4}");
        }
    }
}
