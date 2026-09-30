using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Substrate;
using Substrate.Services.Locales;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Tests.EditMode.Rendering
{
    [Category("Sectors")]
    public class LocaleSkyWiringEditModeTests
    {
        private static readonly string[] CandidateLayers = { "Background", "FarNebula", "StarField", "CloseNebula" };
        private static readonly int[] CandidateQueues = { 2900, 2940, 2950, 2990 };

        [TestCase("Assets/Scenes/InitScene.unity")]
        [TestCase("Assets/Scenes/Locales/Locale_1.unity")]
        [TestCase("Assets/Scenes/Locales/Locale_2.unity")]
        [TestCase("Assets/Scenes/Locales/Locale_3.unity")]
        [TestCase("Assets/Scenes/EditScene.unity")]
        public void LocaleScene_HasExactlyOneActiveLocaleSky(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                Assert.That(scene.GetRootGameObjects().Count(g => g.activeSelf && g.GetComponent<LocaleSky>()),
                    Is.EqualTo(1), "Two active LocaleSky roots would draw every sky layer twice.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase("Assets/Scenes/Locales/Locale_1.unity", "nebula-glow-warm-flat-final")]
        [TestCase("Assets/Scenes/Locales/Locale_2.unity", "nebula-glow-flat-final")]
        [TestCase("Assets/Scenes/Locales/Locale_3.unity", "illustrated-blue-final")]
        [TestCase("Assets/Scenes/InitScene.unity", "nebula-glow-flat-final")]
        [TestCase("Assets/Scenes/EditScene.unity", "nebula-glow-flat-final", "Locale_2")]
        public void SkyRoot_DrawsPerLocaleVariantsOfItsBackground_InLockedOrder(
            string scenePath, string background, string localeFolder = null)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            localeFolder ??= scene.name;
            try
            {
                var roots = scene.GetRootGameObjects();
                var root = roots.Single(g => g.name == "Sky");
                Assert.IsTrue(root.GetComponent<LocaleSky>());
                Assert.IsTrue(root.activeSelf, "The palette-driven sky is the live root.");
                var layers = root.GetComponentsInChildren<MeshRenderer>().Select(r => r.transform).ToArray();
                var illustrated = scene.name == "Locale_3";
                var names = illustrated
                    ? new[] { "Background", "DistantStars", "FarNebula", "StarField", "CloseNebula" }
                    : CandidateLayers;
                var queues = illustrated ? new[] { 2900, 2930, 2940, 2950, 2990 } : CandidateQueues;
                Assert.That(layers.Select(l => l.name), Is.EqualTo(names));

                var sidecar = $"{FlatBackgroundImport.Folder}/{background}.json";
                var expectedParents = new[]
                {
                    $"{FlatBackgroundImport.Folder}/{background}.mat",
                    PaletteParents.PathFor(sidecar, "FarNebula"),
                    "Assets/Visuals/Locales/Sky/StarFieldMaterial.mat",
                    PaletteParents.PathFor(sidecar, "CloseNebula"),
                };
                if (illustrated)
                    expectedParents = new[] { expectedParents[0], expectedParents[2], expectedParents[1],
                        expectedParents[2], expectedParents[3] };
                for (var i = 0; i < layers.Length; i++)
                {
                    var material = layers[i].GetComponent<MeshRenderer>().sharedMaterial;
                    Assert.That(layers[i].gameObject.layer, Is.EqualTo(LayerIds.Sky), $"{layers[i].name} layer");
                    Assert.That(AssetDatabase.GetAssetPath(material),
                        Does.StartWith($"Assets/Visuals/Locales/Sky/Locales/{localeFolder}/"),
                        $"The {layers[i].name} sky layer must use a per-locale material variant.");
                    Assert.That(AssetDatabase.GetAssetPath(material.parent), Is.EqualTo(expectedParents[i]));
                    Assert.That(material.renderQueue, Is.EqualTo(queues[i]), $"{layers[i].name} draw order");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase("Assets/Scenes/InitScene.unity")]
        [TestCase("Assets/Scenes/Locales/Locale_1.unity")]
        [TestCase("Assets/Scenes/Locales/Locale_2.unity")]
        [TestCase("Assets/Scenes/Locales/Locale_3.unity")]
        [TestCase("Assets/Scenes/EditScene.unity")]
        public void Scene_HasNoSkyboxRollbackRootOrLightingData(string scenePath)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                Assert.IsNull(RenderSettings.skybox, "The flat background and sky layers are the only backdrop.");
                Assert.That(scene.GetRootGameObjects().Select(g => g.name), Has.No.Member("Sky (Original)"));
                Assert.That(LightingDataReference().objectReferenceInstanceIDValue, Is.Zero,
                    "Stored lighting data, even a missing asset, would override the flat ambient and custom reflection.");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // Lightmapping.lightingDataAsset reads a missing asset as null; the serialized reference still names it.
        private static SerializedProperty LightingDataReference()
        {
            var getter = typeof(LightmapEditorSettings).GetMethod("GetLightmapSettings",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(getter, "test premise: LightmapEditorSettings.GetLightmapSettings exists");
            return new SerializedObject((Object)getter.Invoke(null, null)).FindProperty("m_LightingDataAsset");
        }

        [TestCase("Assets/Scenes/Locales/Locale_1.unity")]
        [TestCase("Assets/Scenes/Locales/Locale_2.unity")]
        [TestCase("Assets/Scenes/Locales/Locale_3.unity")]
        [TestCase("Assets/Scenes/InitScene.unity")]
        [TestCase("Assets/Scenes/EditScene.unity", "Locale_2")]
        public void Scene_LightsFromFlatAmbientAndALocaleReflectionCubemap(string scenePath, string localeFolder = null)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            localeFolder ??= scene.name;
            try
            {
                SceneManager.SetActiveScene(scene);
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat),
                    "Ships are lit by the locale's flat ambient colour.");
                Assert.That(RenderSettings.defaultReflectionMode, Is.EqualTo(DefaultReflectionMode.Custom));
                Assert.That(RenderSettings.customReflectionTexture, Is.InstanceOf<Cubemap>());
                Assert.That(AssetDatabase.GetAssetPath(RenderSettings.customReflectionTexture),
                    Does.StartWith($"Assets/Visuals/Locales/Sky/Locales/{localeFolder}/"));
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
