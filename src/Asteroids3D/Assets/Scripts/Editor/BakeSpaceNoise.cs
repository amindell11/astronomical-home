using UnityEditor;
using UnityEngine;

public static class BakeSpaceNoise
{
    private const int RESOLUTION = 2048;
    private const string OUTPUT_PATH = "Assets/Noise/SpaceNoise.png";

    [MenuItem("Tools/Bake Space Background Noise")]    
    private static void Bake()
    {
        // Ensure destination folder exists
        System.IO.Directory.CreateDirectory("Assets/Noise");

        // Create a temporary RenderTexture
        var rt = new RenderTexture(RESOLUTION, RESOLUTION, 0, RenderTextureFormat.R16)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        // Use the hidden baking shader (see instructions)
        var mat = new Material(Shader.Find("Hidden/SpaceNoiseBake"));
        if (mat.shader == null)
        {
            Debug.LogError("Hidden/SpaceNoiseBake shader not found – please add it before baking.");
            return;
        }

        // Bake into the RenderTexture
        Graphics.Blit(null, rt, mat);

        // Read pixels back into Texture2D
        var tex = new Texture2D(RESOLUTION, RESOLUTION, TextureFormat.R16, true, true);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, RESOLUTION, RESOLUTION), 0, 0);
        tex.Apply();

        // Encode texture to PNG and write to disk
        var pngBytes = tex.EncodeToPNG();
        System.IO.File.WriteAllBytes(OUTPUT_PATH, pngBytes);
        Debug.Log($"Noise texture baked to {OUTPUT_PATH}");

        // Clean up
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(mat);

        // Refresh AssetDatabase and tweak import settings
        AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath(OUTPUT_PATH);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.sRGBTexture = false; // keep linear
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
        }
    }
} 