using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Substrate.Services.Environment
{
    public sealed class SkyboxImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Visuals/Environment/Sky/Generated";
        public static event Action<Material> Imported;

        public static bool IsSkybox(string path) =>
            path.StartsWith(Folder + "/", StringComparison.Ordinal) &&
            (path.EndsWith("-draft.hdr", StringComparison.Ordinal) ||
             path.EndsWith("-8k.hdr", StringComparison.Ordinal));

        public static Material MaterialFor(string texturePath) =>
            AssetDatabase.LoadAssetAtPath<Material>(Path.ChangeExtension(texturePath, ".mat"));

        public static bool IsFinal(Material material)
        {
            if (!material || !material.mainTexture)
                return false;
            var path = AssetDatabase.GetAssetPath(material.mainTexture);
            if (!IsSkybox(path) || !path.EndsWith("-8k.hdr", StringComparison.Ordinal))
                return false;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            return width == 8192 && height == 4096;
        }

        private void OnPreprocessTexture()
        {
            if (!IsSkybox(assetPath))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.maxTextureSize = 8192;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (!IsSkybox(path))
                    continue;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (!texture || texture.width != 2 * texture.height)
                    throw new InvalidDataException($"Skybox must be a 2:1 HDR panorama: {path}");
                var material = MaterialFor(path);
                if (!material)
                {
                    var shader = Shader.Find("Skybox/Panoramic");
                    if (!shader)
                        throw new InvalidOperationException("Skybox/Panoramic shader is unavailable.");
                    material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                    material.SetFloat("_Exposure", 1);
                    material.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
                    material.mainTexture = texture;
                    AssetDatabase.CreateAsset(material, Path.ChangeExtension(path, ".mat"));
                }
                else if (material.mainTexture != texture)
                {
                    material.mainTexture = texture;
                    EditorUtility.SetDirty(material);
                    AssetDatabase.SaveAssetIfDirty(material);
                }
                Imported?.Invoke(material);
            }
        }
    }
}
