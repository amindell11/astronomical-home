using Substrate.Services.Locales;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ReviseLocales
{
    private const string Sky = "Assets/Visuals/Locales/Sky";

    public static void Run()
    {
        ImportSurface(Sky + "/Planets/GreenGasGiant.png");
        ImportSurface(Sky + "/Planets/BlueGasGiant.png");
        for (var id = 4; id <= 7; id++) Revise(id);
        AssetDatabase.SaveAssets();
    }

    private static void Revise(int id)
    {
        var index = id - 4;
        var folder = Sky + "/Locales/Locale_" + id;
        var background = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Background.mat");
        background.SetColor("_BaseColor", new[] { C(.26f, .06f, .025f), C(.012f, .07f, .10f),
            C(.04f, .015f, .075f), C(.035f, .04f, .055f) }[index]);
        background.SetFloat("_TextureStrength", id == 4 ? .05f : .025f);
        EditorUtility.SetDirty(background);

        var far = AssetDatabase.LoadAssetAtPath<Material>(folder + "/FarNebula.mat");
        far.SetFloat("_NebulaSpeed", new[] { .003f, .004f, .002f, .002f }[index]);
        far.SetFloat("_NebulaZoomResponse", 0);
        far.SetFloat("_NebulaScale", new[] { .30f, .28f, .24f, .24f }[index]);
        far.SetFloat("_NebulaStrength", new[] { .68f, .72f, .55f, .46f }[index]);
        far.SetFloat("_CloudCoverage", new[] { .61f, .60f, .59f, .50f }[index]);
        far.SetFloat("_CloudDirection", new[] { -26f, 42f, 24f, -18f }[index]);
        far.SetFloat("_CloudStretch", new[] { 2.6f, 2f, 3.2f, 3.8f }[index]);
        far.SetFloat("_CloudGlow", new[] { .22f, .22f, .55f, .35f }[index]);
        far.SetFloat("_CloudGlowSpread", id < 6 ? .12f : .20f);
        far.SetColor("_CloudShadow", new[] { C(.28f, .07f, .05f), C(.012f, .10f, .15f),
            C(.07f, .025f, .18f), C(.05f, .035f, .025f) }[index]);
        far.SetColor("_NebulaCool", new[] { C(.85f, .22f, .09f), C(.015f, .44f, .63f),
            C(.22f, .10f, .48f), C(.26f, .14f, .055f) }[index]);
        far.SetColor("_NebulaWarm", new[] { C(.75f, .21f, .24f), C(.015f, .35f, .20f),
            C(.38f, .08f, .52f), C(.38f, .19f, .065f) }[index]);
        far.SetColor("_CloudGlowCool", new[] { C(1.4f, .5f, .14f), C(.01f, .9f, 1.2f),
            C(.28f, .10f, .8f), C(.5f, .23f, .055f) }[index]);
        far.SetColor("_CloudGlowWarm", new[] { C(1.15f, .55f, .5f), C(.02f, 1.2f, .1f),
            C(.7f, .12f, .95f), C(.7f, .34f, .08f) }[index]);
        EditorUtility.SetDirty(far);
        var close = AssetDatabase.LoadAssetAtPath<Material>(folder + "/CloseNebula.mat");
        close.SetFloat("_NebulaSpeed", .003f);
        close.SetFloat("_NebulaStrength", id < 6 ? .008f : .004f);
        EditorUtility.SetDirty(close);
        if (id >= 6) Planet(folder, id == 6);

        var path = "Assets/Prefabs/Locales/Locale_" + id + ".prefab";
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var serialized = new SerializedObject(contents.GetComponent<LocaleSky>());
            var lighting = serialized.FindProperty("lighting");
            lighting.FindPropertyRelative("ambientMode").intValue = (int)AmbientMode.Flat;
            lighting.FindPropertyRelative("ambientSkyColor").colorValue = C(.24f, .24f, .24f);
            lighting.FindPropertyRelative("ambientEquatorColor").colorValue = C(.24f, .24f, .24f);
            lighting.FindPropertyRelative("ambientGroundColor").colorValue = C(.24f, .24f, .24f);
            lighting.FindPropertyRelative("ambientIntensity").floatValue = 1;
            lighting.FindPropertyRelative("fog").boolValue = false;
            lighting.FindPropertyRelative("reflectionIntensity").floatValue = .35f;
            lighting.FindPropertyRelative("reflection").objectReferenceValue = Reflection(folder);
            var sun = (Light)lighting.FindPropertyRelative("sun").objectReferenceValue;
            foreach (var light in contents.GetComponentsInChildren<Light>(true))
            {
                light.color = Color.white;
                light.useColorTemperature = false;
                light.intensity = light == sun ? 1.15f : .35f;
                light.transform.rotation = light == sun ? Quaternion.Euler(30, 0, 0) : Quaternion.Euler(-20, 145, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
        Debug.Log("REVISED=Locale_" + id);
    }

    private static void Planet(string folder, bool green)
    {
        var material = new Material(Shader.Find("Locales/Planet")) { name = "Planet" };
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(
            Sky + "/Planets/" + (green ? "GreenGasGiant.png" : "BlueGasGiant.png")));
        material.SetVector("_PlanetCenter", green ? new Vector4(56000, 0, 100000, 0) : new Vector4(75000, 6000, 100000, 0));
        material.SetFloat("_PlanetRadius", green ? 60000 : 57000);
        material.SetVector("_LightDirection", green ? new Vector4(-.9f, .25f, -.4f, 0) : new Vector4(-.8f, .15f, .6f, 0));
        material.SetFloat("_AxialTilt", green ? -22 : 28);
        material.SetFloat("_RotationSpeed", green ? .003f : .002f);
        material.SetColor("_AtmosphereColor", green ? C(.018f, .095f, .075f) : C(.018f, .08f, .16f));
        material.SetColor("_Tint", green ? C(.95f, .95f, .95f) : C(.9f, .9f, .9f));
        var existing = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Planet.mat");
        EditorUtility.CopySerialized(material, existing);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(material);
    }

    private static Cubemap Reflection(string folder)
    {
        var cube = new Cubemap(16, TextureFormat.RGBAHalf, true) { name = "ReflectionCubemap" };
        for (var face = 0; face < 6; face++)
        {
            var pixels = new Color[256];
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
            {
                var value = Mathf.Lerp(.04f, face % 2 == 0 ? .32f : .2f, y / 15f);
                pixels[y * 16 + x] = C(value, value, value).linear;
            }
            cube.SetPixels(pixels, (CubemapFace)face);
        }
        cube.Apply();
        var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(folder + "/ReflectionCubemap.asset");
        EditorUtility.CopySerialized(cube, existing);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(cube);
        return existing;
    }

    private static void ImportSurface(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.wrapModeU = TextureWrapMode.Repeat;
        importer.wrapModeV = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 2048;
        importer.filterMode = FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static Color C(float r, float g, float b) => new Color(r, g, b, 1);
}
