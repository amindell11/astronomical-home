using System;
using System.IO;
using Substrate.Sectors;
using Substrate.Services.Locales;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class AuthorLocales
{
    private const string SkyFolder = "Assets/Visuals/Locales/Sky";
    private const string FlatFolder = "Assets/Visuals/Locales/Flat/Generated";
    private const string PrefabFolder = "Assets/Prefabs/Locales";
    private const string ConfigFolder = "Assets/Settings/Game/Preview/Locales";

    public static void Coral() => Author(4);
    public static void Cyan() => Author(5);
    public static void Green() => Author(6);
    public static void Blue() => Author(7);

    public static void Run()
    {
        for (var id = 4; id <= 7; id++) Author(id);
    }

    private static void Author(int id)
    {
        var names = new[] { "coral-sunset", "cyan-aurora", "violet-planet", "amber-planet" };
        var labels = new[] { "Coral Sunset", "Cyan Aurora", "Verdant Giant", "Blue Crescent" };
        var index = id - 4;
        var name = "Locale_" + id;
        var folder = SkyFolder + "/Locales/" + name;
        Folder(folder);
        Folder(ConfigFolder);
        var generated = FlatFolder + "/" + names[index] + "-final";
        var background = Variant(generated + ".mat", folder + "/Background.mat");
        var far = Variant(generated + ".FarNebula.mat", folder + "/FarNebula.mat");
        var close = Variant(generated + ".CloseNebula.mat", folder + "/CloseNebula.mat");
        var distant = Variant(SkyFolder + "/StarFieldMaterial.mat", folder + "/DistantStars.mat",
            SkyFolder + "/Locales/Locale_3/DistantStars.mat");
        var stars = Variant(SkyFolder + "/StarFieldMaterial.mat", folder + "/StarField.mat",
            SkyFolder + "/Locales/Locale_3/StarField.mat");

        background.SetFloat("_RepeatDistance", 20000);
        background.SetFloat("_ViewHeightInTiles", 0.4f);
        background.SetFloat("_TextureStrength", id < 6 ? 0.16f : 0.055f);
        var bases = new[] { C(.52f, .22f, .15f), C(.02f, .16f, .26f), C(.065f, .025f, .15f), C(.05f, .035f, .021f) };
        background.SetColor("_BaseColor", bases[index]);

        far.SetFloat("_NebulaSpeed", 0);
        far.SetFloat("_NebulaZoomResponse", 0);
        far.SetFloat("_NebulaParallax", 0.018f);
        far.SetFloat("_NebulaScale", id == 5 ? .30f : .24f);
        far.SetFloat("_NebulaStrength", new[] { .9f, .85f, .65f, .55f }[index]);
        far.SetFloat("_CloudCoverage", new[] { .64f, .60f, .54f, .50f }[index]);
        far.SetFloat("_CloudOpacity", .94f);
        far.SetFloat("_CloudDirection", new[] { -26f, 68f, 24f, -18f }[index]);
        far.SetFloat("_CloudStretch", new[] { 2.6f, 3f, 3.2f, 3.8f }[index]);
        far.SetFloat("_CloudGlow", new[] { .85f, .65f, .85f, .65f }[index]);
        far.SetFloat("_CloudGlowSpread", .2f);
        far.SetFloat("_Seed", new[] { 53f, 71f, 91f, 113f }[index]);
        far.SetColor("_CloudShadow", new[] { C(.48f, .13f, .10f), C(.012f, .17f, .29f), C(.07f, .025f, .18f), C(.075f, .052f, .025f) }[index]);
        far.SetColor("_CloudGlowCool", new[] { C(2f, .55f, .2f), C(.015f, 1.5f, 2f), C(.24f, .065f, 1.2f), C(.72f, .34f, .09f) }[index]);
        far.SetColor("_CloudGlowWarm", new[] { C(2.2f, .85f, .55f), C(.025f, 1.7f, .15f), C(.8f, .08f, 1.4f), C(1f, .5f, .13f) }[index]);

        close.SetFloat("_NebulaStrength", id < 6 ? .016f : .008f);
        close.SetFloat("_NebulaSpeed", 0);
        close.SetFloat("_NebulaZoomResponse", 0);
        close.SetFloat("_NebulaParallax", .05f);
        close.SetFloat("_Seed", 29 + id * 13);
        var coolStars = new[] { C(1f, .72f, .5f), C(.12f, .8f, 1f), C(.65f, .6f, 1f), C(1f, .79f, .46f) };
        var warmStars = new[] { C(1f, .3f, .13f), C(.1f, 1f, .48f), C(1f, .54f, .25f), C(1f, .43f, .1f) };
        foreach (var material in new[] { distant, stars })
        {
            material.SetColor("_ColorCool", coolStars[index]);
            material.SetColor("_ColorWarm", warmStars[index]);
            material.SetColor("_ShootingColor", coolStars[index]);
            material.SetFloat("_Seed", material == distant ? id * 31 : id * 47);
            material.SetFloat("_WarmColorShare", id == 7 ? .75f : .2f);
        }
        distant.SetFloat("_StarDensity", id < 6 ? .19f : .14f);
        distant.SetFloat("_Brightness", id == 4 ? .65f : .45f);
        stars.SetFloat("_StarDensity", id < 6 ? .025f : .021f);
        stars.SetFloat("_Brightness", id == 4 ? 4f : 3f);
        stars.SetFloat("_StarSizeMax", .09f);
        stars.SetFloat("_ShootingBrightness", .05f);

        var contents = PrefabUtility.LoadPrefabContents(PrefabFolder + "/Locale_3.prefab");
        try
        {
            contents.name = name;
            var sky = contents.transform.Find("Sky");
            Assign(sky, "Background", background);
            Assign(sky, "FarNebula", far);
            Assign(sky, "CloseNebula", close);
            Assign(sky, "DistantStars", distant);
            Assign(sky, "StarField", stars);
            if (id >= 6)
            {
                var planet = new Material(Shader.Find("Locales/Planet")) { name = "Planet" };
                var texturePath = SkyFolder + "/Planets/" + (id == 6 ? "GreenGasGiant.png" : "BlueGasGiant.png");
                ImportPlanet(texturePath);
                planet.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                planet.SetVector("_PlanetCenter", new Vector4(id == 6 ? 15f : 16f, 1.5f, 0, 0));
                planet.SetFloat("_PlanetSize", id == 6 ? 34 : 30);
                planet.SetFloat("_Parallax", .0015f);
                planet = Save(planet, folder + "/Planet.mat");
                var planetObject = UnityEngine.Object.Instantiate(sky.Find("Background").gameObject, sky);
                planetObject.name = "Planet";
                planetObject.transform.SetSiblingIndex(4);
                planetObject.GetComponent<MeshRenderer>().sharedMaterial = planet;
            }

            var atmospherePath = folder + "/Atmosphere.asset";
            if (!AssetDatabase.LoadMainAssetAtPath(atmospherePath))
                AssetDatabase.CopyAsset(SkyFolder + "/Locales/Locale_3/Atmosphere.asset", atmospherePath);
            var volume = contents.GetComponentInChildren(Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime"), true);
            var volumeData = new SerializedObject(volume);
            volumeData.FindProperty("sharedProfile").objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(atmospherePath);
            volumeData.ApplyModifiedPropertiesWithoutUndo();
            var ambient = new[] { C(.37f, .25f, .22f), C(.19f, .3f, .35f), C(.26f, .25f, .34f), C(.24f, .27f, .32f) }[index];
            var key = new[] { C(1f, .78f, .58f), C(.65f, .94f, 1f), C(.86f, 1f, .69f), C(.66f, .83f, 1f) }[index];
            var fill = new[] { C(.85f, .35f, .27f), C(.12f, .55f, .8f), C(.48f, .24f, .77f), C(.65f, .4f, .19f) }[index];
            var locale = contents.GetComponent<LocaleSky>();
            var serialized = new SerializedObject(locale);
            var lighting = serialized.FindProperty("lighting");
            lighting.FindPropertyRelative("ambientSkyColor").colorValue = ambient;
            lighting.FindPropertyRelative("ambientEquatorColor").colorValue = ambient * .6f;
            lighting.FindPropertyRelative("ambientGroundColor").colorValue = ambient * .25f;
            lighting.FindPropertyRelative("fogColor").colorValue = ambient * .4f;
            lighting.FindPropertyRelative("reflection").objectReferenceValue = Reflection(folder, ambient, key, fill);
            var sun = (Light)lighting.FindPropertyRelative("sun").objectReferenceValue;
            foreach (var light in contents.GetComponentsInChildren<Light>(true))
            {
                light.color = light == sun ? key : fill;
                light.useColorTemperature = false;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(contents, PrefabFolder + "/" + name + ".prefab");
            var configPath = ConfigFolder + "/" + labels[index].Replace(" ", "") + ".asset";
            var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(configPath);
            if (!config)
            {
                config = ScriptableObject.CreateInstance<SectorSettings>();
                AssetDatabase.CreateAsset(config, configPath);
            }
            var configData = new SerializedObject(config);
            configData.FindProperty("displayName").stringValue = labels[index];
            configData.FindProperty("difficultySeed").intValue = id * 177;
            configData.FindProperty("locale").objectReferenceValue = prefab.GetComponent<LocaleSky>();
            configData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("AUTHORED=" + name + " CONFIG=" + configPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static Material Variant(string parentPath, string path, string sourcePath = null)
    {
        var parent = AssetDatabase.LoadAssetAtPath<Material>(parentPath);
        if (!parent) throw new InvalidOperationException("Missing generated material: " + parentPath);
        var source = sourcePath == null ? parent : AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
        return Save(new Material(source) { parent = parent }, path);
    }

    private static T Save<T>(T asset, string path) where T : UnityEngine.Object
    {
        asset.name = Path.GetFileNameWithoutExtension(path);
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!existing)
        {
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        EditorUtility.CopySerialized(asset, existing);
        UnityEngine.Object.DestroyImmediate(asset);
        return existing;
    }

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = path.Substring(0, path.LastIndexOf('/'));
        Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void Assign(Transform sky, string name, Material material) =>
        sky.Find(name).GetComponent<MeshRenderer>().sharedMaterial = material;

    private static Color C(float r, float g, float b) => new Color(r, g, b, 1);

    private static Cubemap Reflection(string folder, Color ambient, Color key, Color fill)
    {
        var cube = new Cubemap(16, TextureFormat.RGBAHalf, true);
        for (var face = 0; face < 6; face++)
        {
            var pixels = new Color[256];
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
            {
                var color = Color.Lerp(ambient * .35f, face % 2 == 0 ? key : fill, y / 15f);
                pixels[y * 16 + x] = color.linear;
            }
            cube.SetPixels(pixels, (CubemapFace)face);
        }
        cube.Apply();
        return Save(cube, folder + "/ReflectionCubemap.asset");
    }

    private static void ImportPlanet(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 2048;
        importer.filterMode = FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }
}

