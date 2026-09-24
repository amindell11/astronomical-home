using System;
using System.IO;
using NUnit.Framework;
using Substrate.Services.Environment.Flat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Tests.EditMode.Environments
{
    [Category("Sectors")]
    public sealed class EnvironmentAuthoringEditModeTests
    {
        private string folder;
        private string scenePath;
        private Scene previousScene;
        private Scene locale;
        private SceneAsset localeAsset;
        private FlatBackgroundAsset first;
        private FlatBackgroundAsset second;
        private Light nativeLight;
        private Material sky;

        [SetUp]
        public void SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            string name = "EnvironmentAuthoringTests-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            folder = "Assets/" + name;
            previousScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(previousScene, folder + "/Base.unity");
            scenePath = folder + "/Locale.unity";
            locale = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(locale);
            nativeLight = new GameObject("Native Light").AddComponent<Light>();
            nativeLight.color = Color.cyan;
            nativeLight.intensity = 3.5f;
            sky = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(sky, folder + "/Sky.mat");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.magenta;
            EditorSceneManager.SaveScene(locale, scenePath);
            localeAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            SceneManager.SetActiveScene(previousScene);
            first = CreateAsset("First", true, Color.red);
            second = CreateAsset("Second", true, Color.green);
            Undo.IncrementCurrentGroup();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            SceneManager.SetActiveScene(previousScene);
            EditorSceneManager.CloseScene(locale, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void Apply_MarksLocaleDirtyWithoutSavingOrChangingNativeLightingAndUndoRemovesNewRoot()
        {
            byte[] savedScene = File.ReadAllBytes(scenePath);
            var environment = EnvironmentPreviewWindow.Apply(localeAsset, first);

            Assert.That(environment.gameObject.scene, Is.EqualTo(locale));
            Assert.That(environment.transform.parent, Is.Null);
            Assert.That(environment.gameObject.name, Is.EqualTo("Environment"));
            Assert.That(environment.Background, Is.SameAs(first));
            Assert.That(locale.isDirty, Is.True);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previousScene));
            Assert.That(File.ReadAllBytes(scenePath), Is.EqualTo(savedScene));
            Assert.That(nativeLight.enabled, Is.True);
            Assert.That(nativeLight.color, Is.EqualTo(Color.cyan));
            Assert.That(nativeLight.intensity, Is.EqualTo(3.5f));
            SceneManager.SetActiveScene(locale);
            Assert.That(RenderSettings.skybox == sky, Is.True);
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(Color.magenta));
            SceneManager.SetActiveScene(previousScene);

            Undo.PerformUndo();
            Assert.That(EnvironmentPreviewWindow.FindEnvironment(locale), Is.Null);
            Assert.That(nativeLight, Is.Not.Null);
        }

        [Test]
        public void Apply_DraftIsRejectedBeforeChangingLocale()
        {
            var draft = CreateAsset("Draft", false, Color.blue);
            Assert.Throws<ArgumentException>(() => EnvironmentPreviewWindow.Apply(localeAsset, draft));
            Assert.That(EnvironmentPreviewWindow.FindEnvironment(locale), Is.Null);
            Assert.That(locale.isDirty, Is.False);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previousScene));
        }

        [Test]
        public void Apply_UpdatesExistingRootPreservingMappingAndOverrideAndUndoRestoresSource()
        {
            var original = EnvironmentPreviewWindow.Apply(localeAsset, first);
            var serialized = new SerializedObject(original);
            serialized.FindProperty("repeatDistance").floatValue = 37000;
            serialized.FindProperty("viewHeightInTiles").floatValue = 0.75f;
            var primary = serialized.FindProperty("primaryColor");
            primary.FindPropertyRelative("useOverride").boolValue = true;
            primary.FindPropertyRelative("color").colorValue = Color.yellow;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var updated = EnvironmentPreviewWindow.Apply(localeAsset, second);
            Assert.That(updated, Is.SameAs(original));
            Assert.That(updated.Background, Is.SameAs(second));
            Assert.That(updated.RepeatDistance, Is.EqualTo(37000));
            Assert.That(updated.ViewHeightInTiles, Is.EqualTo(0.75f));
            Assert.That(updated.PrimaryColor, Is.EqualTo(Color.yellow));

            Undo.PerformUndo();
            Assert.That(EnvironmentPreviewWindow.FindEnvironment(locale), Is.SameAs(original));
            Assert.That(original.Background, Is.SameAs(first));
            Assert.That(original.PrimaryColor, Is.EqualTo(Color.yellow));
            Assert.That(original.RepeatDistance, Is.EqualTo(37000));
        }

        [Test]
        public void Palette_ReimportUpdatesDefaultsAndDisablingOverrideRestoresImportedColor()
        {
            var root = EnvironmentPreviewWindow.Apply(localeAsset, first);
            var serialized = new SerializedObject(root);
            var primary = serialized.FindProperty("primaryColor");
            primary.FindPropertyRelative("useOverride").boolValue = true;
            primary.FindPropertyRelative("color").colorValue = Color.yellow;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            first.Initialize(Texture2D.whiteTexture, true, Color.cyan, Color.magenta, Color.blue, Color.white, "{}");
            Assert.That(root.BaseColor, Is.EqualTo(Color.cyan));
            Assert.That(root.PrimaryColor, Is.EqualTo(Color.yellow));
            Assert.That(root.SecondaryColor, Is.EqualTo(Color.blue));
            Assert.That(root.AccentColor, Is.EqualTo(Color.white));

            serialized.Update();
            serialized.FindProperty("primaryColor").FindPropertyRelative("useOverride").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(root.PrimaryColor, Is.EqualTo(Color.magenta));
        }

        [Test]
        public void Offset_LongTravelInBothDirectionsAndRepeatedReturnsDoNotAccumulateDrift()
        {
            const float distance = 65536;
            var origin = new Vector3(128, 384, 0);
            var expected = EnvironmentAuthoring.Offset(origin, distance);
            for (int cycle = -5000; cycle <= 5000; cycle++)
            {
                var position = origin + new Vector3(cycle * distance, -cycle * distance, cycle);
                Assert.That(EnvironmentAuthoring.Offset(position, distance), Is.EqualTo(expected));
                Assert.That(EnvironmentAuthoring.Offset(origin, distance), Is.EqualTo(expected));
            }
            Assert.That(EnvironmentAuthoring.Offset(new Vector3(-128, -384, 0), distance),
                Is.EqualTo(Vector2.one - expected));
        }

        private FlatBackgroundAsset CreateAsset(string name, bool final, Color color)
        {
            var asset = ScriptableObject.CreateInstance<FlatBackgroundAsset>();
            asset.Initialize(Texture2D.whiteTexture, final, color, color, color, color, "{}");
            AssetDatabase.CreateAsset(asset, folder + "/" + name + ".asset");
            return asset;
        }
    }
}



