using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Substrate.Services.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Skyboxes
{
    [Category("Sectors")]
    public class SkyboxAuthoringEditModeTests
    {
        private string folder;
        private Scene previous;
        private readonly List<Scene> scenes = new();
        private readonly List<Material> materials = new();

        [SetUp]
        public void SetUp()
        {
            folder = SkyboxImport.Folder + "/test-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            previous = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(previous, folder + "/base.unity");
        }

        [TearDown]
        public void TearDown()
        {
            SceneManager.SetActiveScene(previous);
            foreach (var scene in scenes)
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            scenes.Clear();
            foreach (var material in materials)
                if (material && !EditorUtility.IsPersistent(material))
                    Object.DestroyImmediate(material);
            materials.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(folder);
            Undo.ClearAll();
        }

        private Scene NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            scenes.Add(scene);
            EditorSceneManager.SaveScene(scene, folder + "/scene-" + scenes.Count + ".unity");
            SceneManager.SetActiveScene(scene);
            return scene;
        }

        private Material NewMaterial()
        {
            var material = new Material(Shader.Find("Skybox/Panoramic"));
            materials.Add(material);
            return material;
        }

        private string ImportSky(string name, int width = 16)
        {
            var path = folder + "/" + name + ".hdr";
            using (var output = new BinaryWriter(File.Create(path)))
            {
                output.Write(Encoding.ASCII.GetBytes($"#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y {width/2} +X {width}\n"));
                using var row = new MemoryStream();
                row.Write(new byte[] { 2, 2, (byte)(width >> 8), (byte)(width & 255) }, 0, 4);
                foreach (var channel in new byte[] { 128, 64, 32, 131 })
                    for (var remaining = width; remaining > 0;)
                    {
                        var count = Math.Min(127, remaining);
                        row.WriteByte((byte)(128 + count));
                        row.WriteByte(channel);
                        remaining -= count;
                    }
                var bytes = row.ToArray();
                for (var y = 0; y < width/2; y++)
                    output.Write(bytes);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return path;
        }

        [Test]
        public void ImportAndReimport_PrepareHdrMaterialAndKeepItsIdentityAndTuning()
        {
            var path = ImportSky("candidate-draft");
            var material = SkyboxImport.MaterialFor(path);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Skybox/Panoramic"));
            Assert.That(material.mainTexture, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Texture2D>(path)));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.sRGBTexture, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(8192));
            var identity = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material));
            material.SetFloat("_Rotation", 37);
            AssetDatabase.SaveAssetIfDirty(material);
            ImportSky("candidate-draft");
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(SkyboxImport.MaterialFor(path))), Is.EqualTo(identity));
            Assert.That(SkyboxImport.MaterialFor(path).GetFloat("_Rotation"), Is.EqualTo(37));
            Assert.That(SkyboxImport.IsFinal(material), Is.False);
        }

        [Test]
        public void Preview_OriginalCandidateAndDisposeRestoreTheCapturedScene()
        {
            var target = NewScene();
            var original = NewMaterial();
            var candidate = NewMaterial();
            RenderSettings.skybox = original;
            var preview = new SkyboxPreviewSession(candidate);
            Assert.That(RenderSettings.skybox, Is.EqualTo(candidate));
            preview.ShowCandidate(false);
            Assert.That(RenderSettings.skybox, Is.EqualTo(original));
            preview.ShowCandidate(true);
            var other = NewScene();
            var otherSky = NewMaterial();
            RenderSettings.skybox = otherSky;
            preview.Dispose();
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(other));
            Assert.That(RenderSettings.skybox, Is.EqualTo(otherSky));
            SceneManager.SetActiveScene(target);
            Assert.That(RenderSettings.skybox, Is.EqualTo(original));
        }

        [Test]
        public void ApplyFinal_TargetsChosenSceneAndSupportsUndo()
        {
            var texture = ImportSky("candidate-8k", 8192);
            var final = SkyboxImport.MaterialFor(texture);
            Assert.That(SkyboxImport.IsFinal(final), Is.True);
            var target = NewScene();
            var original = NewMaterial();
            AssetDatabase.CreateAsset(original, folder + "/original.mat");
            RenderSettings.skybox = original;
            RenderSettings.fogDensity = 0.123f;
            var scenePath = folder + "/environment.unity";
            EditorSceneManager.SaveScene(target, scenePath);
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            var other = NewScene();
            var otherSky = NewMaterial();
            RenderSettings.skybox = otherSky;
            SkyboxPreviewWindow.ApplyToEnvironment(sceneAsset, final);
            Undo.FlushUndoRecordObjects();
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(other));
            Assert.That(RenderSettings.skybox, Is.EqualTo(otherSky));
            SceneManager.SetActiveScene(target);
            Assert.That(RenderSettings.skybox, Is.EqualTo(final));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(0.123f));
            Assert.That(target.isDirty, Is.True);
            Undo.PerformUndo();
            Assert.That(RenderSettings.skybox, Is.EqualTo(original));
        }

        [Test]
        public void SmallHdrCannotBeAppliedAsFinalDespiteItsFilename()
        {
            var material = SkyboxImport.MaterialFor(ImportSky("fake-8k"));
            Assert.That(SkyboxImport.IsFinal(material), Is.False);
            Assert.That(SkyboxImport.IsSkybox("Assets/Elsewhere/sky-8k.hdr"), Is.False);
        }
    }
}
