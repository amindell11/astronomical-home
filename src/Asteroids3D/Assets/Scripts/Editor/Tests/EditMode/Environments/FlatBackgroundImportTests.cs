using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Substrate.Services.Environment.Flat;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Environments
{
    [Category("Sectors")]
    public sealed class FlatBackgroundImportTests
    {
        private string folder;
        private string assetPath;

        [SetUp]
        public void SetUp()
        {
            string name = "FlatBackgroundImportTests-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            folder = "Assets/" + name;
            assetPath = folder + "/candidate.flatbg";
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void Reimport_ReplacesDraftWithFinalAndKeepsAssetIdentitiesAndHdr()
        {
            WriteBundle("draft");
            Import();
            var draft = AssetDatabase.LoadAssetAtPath<FlatBackgroundAsset>(assetPath);
            Assert.That(draft, Is.Not.Null);
            Assert.That(draft.IsFinal, Is.False);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(draft, out string assetGuid, out long assetId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(draft.Texture, out string textureGuid, out long textureId);

            WriteBundle("final");
            Import();
            var final = AssetDatabase.LoadAssetAtPath<FlatBackgroundAsset>(assetPath);
            Assert.That(final.IsFinal, Is.True);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(final, out string newAssetGuid, out long newAssetId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(final.Texture, out string newTextureGuid, out long newTextureId);
            Assert.That(newAssetGuid, Is.EqualTo(assetGuid));
            Assert.That(newAssetId, Is.EqualTo(assetId));
            Assert.That(newTextureGuid, Is.EqualTo(textureGuid));
            Assert.That(newTextureId, Is.EqualTo(textureId));
            Assert.That(final.Texture.width, Is.EqualTo(3));
            Assert.That(final.Texture.height, Is.EqualTo(2));
            Assert.That(final.Texture.format, Is.EqualTo(TextureFormat.RGBAHalf));
            Assert.That(final.Texture.isDataSRGB, Is.False);
            Assert.That(final.Texture.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(final.Texture.wrapModeV, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(final.Texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(final.Texture.mipmapCount, Is.GreaterThan(1));
            Assert.That(final.Texture.GetPixel(0, 0).r, Is.EqualTo(2));
            Assert.That(final.Texture.GetPixel(0, 1).r, Is.EqualTo(4));
            Assert.That(final.BaseColor, Is.EqualTo(new Color(0.125f, 0.25f, 0.5f, 1)));
            Assert.That(final.PrimaryColor.r, Is.EqualTo(2));
            Assert.That(final.SecondaryColor.g, Is.EqualTo(2));
            Assert.That(final.AccentColor.b, Is.EqualTo(2));
            StringAssert.Contains("\"seed\":679", final.ManifestJson);
        }

        [TestCase("hash")]
        [TestCase("length")]
        [TestCase("nonfinite")]
        [TestCase("palette")]
        [TestCase("schema")]
        [TestCase("exr")]
        public void MalformedBundle_RejectsImportWithoutPublishingAssets(string defect)
        {
            WriteBundle("draft", defect);
            LogAssert.Expect(LogType.Error, new Regex("Invalid flat background bundle:"));
            Import();
            Assert.That(AssetDatabase.LoadAssetAtPath<FlatBackgroundAsset>(assetPath), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath), Is.Null);
        }

        private void Import() => AssetDatabase.ImportAsset(assetPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        private void WriteBundle(string stage, string defect = null)
        {
            var pixels = new byte[3 * 2 * 8];
            for (int pixel = 0; pixel < 6; pixel++)
            {
                pixels[pixel * 8 + 1] = pixel < 3 ? (byte)0x40 : (byte)0x44;
                pixels[pixel * 8 + 7] = 0x3c;
            }
            if (defect == "nonfinite")
                pixels[1] = 0x7c;
            if (defect == "length")
                Array.Resize(ref pixels, pixels.Length - 2);
            using var sha = SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(pixels)).Replace("-", "").ToLowerInvariant();
            if (defect == "hash")
                hash = new string('0', 64);
            string manifest = "{\"schema_version\":" + (defect == "schema" ? "2" : "1") +
                ",\"stage\":\"" + stage + "\",\"width\":3,\"height\":2,\"pixel_sha256\":\"" + hash +
                "\",\"palette\":{\"base\":[0.125,0.25,0.5],\"primary\":[2,0,0]," +
                "\"secondary\":[0,2,0],\"accent\":" + (defect == "palette" ? "[0,2]" : "[0,0,2]") +
                "},\"provenance\":{\"seed\":679}}";
            var exrTexture = new Texture2D(3, 2, TextureFormat.RGBAHalf, false, true);
            exrTexture.SetPixels(new[] { Color.red * 2, Color.black, Color.black,
                Color.red * 4, Color.black, Color.black });
            exrTexture.Apply();
            byte[] exr = exrTexture.EncodeToEXR();
            Object.DestroyImmediate(exrTexture);
            using var output = File.Create(assetPath);
            using var archive = new ZipArchive(output, ZipArchiveMode.Create);
            WriteEntry(archive, "manifest.json", Encoding.UTF8.GetBytes(manifest));
            WriteEntry(archive, "image.rgba16f", pixels);
            if (defect != "exr")
                WriteEntry(archive, "image.exr", exr);
        }

        private static void WriteEntry(ZipArchive archive, string name, byte[] bytes)
        {
            using var output = archive.CreateEntry(name).Open();
            output.Write(bytes, 0, bytes.Length);
        }
    }
}
