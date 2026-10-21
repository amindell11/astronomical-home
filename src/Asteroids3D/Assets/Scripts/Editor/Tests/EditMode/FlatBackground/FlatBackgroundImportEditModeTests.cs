using System;
using System.IO;
using NUnit.Framework;
using Substrate.Services.Locales;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.FlatBackground
{
    [Category("Sectors")]
    public class FlatBackgroundImportEditModeTests
    {
        private string folder;

        [SetUp]
        public void SetUp()
        {
            folder = FlatBackgroundImport.Folder + "/test-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(folder);

        [Test]
        public void Reimport_KeepsTheMaterialIdentityAndItsHandSetTuning()
        {
            var texturePath = folder + "/clouds-final.exr";
            var materialPath = Path.ChangeExtension(texturePath, ".mat");
            Publish(texturePath, new Color(0.2f, 0.4f, 1.5f));

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.IsTrue(material, $"The import must build {materialPath}.");
            Assert.AreEqual("Locales/Flat Background", material.shader.name);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath), material.mainTexture);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            Assert.IsFalse(importer.sRGBTexture, "HDR clouds are scene-linear.");
            Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapMode, "The background repeats in both axes.");
            var identity = AssetDatabase.AssetPathToGUID(materialPath);
            material.SetFloat("_TextureStrength", 0.37f);
            AssetDatabase.SaveAssetIfDirty(material);

            Publish(texturePath, new Color(1.2f, 0.3f, 0.1f));

            Assert.AreEqual(identity, AssetDatabase.AssetPathToGUID(materialPath));
            var reimported = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.AreEqual(0.37f, reimported.GetFloat("_TextureStrength"), 1e-6f,
                "A resend must not reset tuning made in the Inspector.");
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath), reimported.mainTexture);
        }

        private static void Publish(string path, Color colour)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBAFloat, false);
            try
            {
                var pixels = new Color[64];
                Array.Fill(pixels, colour);
                texture.SetPixels(pixels);
                File.WriteAllBytes(path, texture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
    }
}
