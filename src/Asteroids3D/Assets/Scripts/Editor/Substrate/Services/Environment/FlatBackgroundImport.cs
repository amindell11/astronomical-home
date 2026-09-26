using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Substrate.Services.Environment
{
    public sealed class FlatBackgroundImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Visuals/Environment/Flat/Generated";

        public static bool IsFlatBackground(string path) =>
            path.StartsWith(Folder + "/", StringComparison.Ordinal) &&
            (path.EndsWith("-draft.exr", StringComparison.Ordinal) ||
             path.EndsWith("-final.exr", StringComparison.Ordinal));

        private void OnPreprocessTexture()
        {
            if (!IsFlatBackground(assetPath))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                var materialPath = Path.ChangeExtension(path, ".mat");
                if (!IsFlatBackground(path) || AssetDatabase.LoadAssetAtPath<Material>(materialPath))
                    continue;
                var shader = Shader.Find("Environment/Flat Background");
                if (!shader)
                    throw new InvalidOperationException("Environment/Flat Background shader is unavailable.");
                var material = new Material(shader) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path) };
                AssetDatabase.CreateAsset(material, materialPath);
            }
        }
    }
}
