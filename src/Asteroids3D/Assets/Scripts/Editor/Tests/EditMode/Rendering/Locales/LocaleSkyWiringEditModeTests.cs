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

namespace Tests.EditMode.Rendering.Locales
{
    [Category("Sectors")]
    public class LocaleSkyWiringEditModeTests
    {
        private const string Locale1 = "Assets/Prefabs/Locales/Locale_1.prefab";
        private const string Locale2 = "Assets/Prefabs/Locales/Locale_2.prefab";
        private const string Locale3 = "Assets/Prefabs/Locales/Locale_3.prefab";
        private const string EditScene = "Assets/Scenes/EditScene.unity";
        private static readonly string[] CandidateLayers = { "Background", "FarNebula", "StarField", "CloseNebula" };
        private static readonly int[] CandidateQueues = { 2900, 2940, 2950, 2990 };

        [TestCase(Locale1)]
        [TestCase(Locale2)]
        [TestCase(Locale3)]
        public void LocalePrefab_HasExactlyOneLocaleSky_AtItsRoot(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab.GetComponentsInChildren<LocaleSky>(true), Has.Length.EqualTo(1),
                "Two LocaleSky roots would draw every sky layer twice.");
            Assert.IsTrue(prefab.GetComponent<LocaleSky>(), "The locale service instantiates the prefab by its LocaleSky.");
        }

        [Test]
        public void EditScene_HasExactlyOneActiveLocaleSky()
        {
            var scene = EditorSceneManager.OpenScene(EditScene, OpenSceneMode.Additive);
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

        [TestCase(Locale1, "copper-turquoise-final")]
        [TestCase(Locale2, "nebula-glow-flat-final")]
        [TestCase(Locale3, "illustrated-blue-final")]
        [TestCase(EditScene, "nebula-glow-flat-final", "Locale_2")]
        public void SkyRoot_DrawsPerLocaleVariantsOfItsBackground_InLockedOrder(
            string path, string background, string localeFolder = null)
        {
            using var locale = new OpenLocale(path);
            localeFolder ??= locale.Name;
            var root = locale.Sky;
            Assert.IsTrue(locale.Root);
            Assert.IsTrue(root.gameObject.activeSelf, "The palette-driven sky is the live root.");
            var layers = root.GetComponentsInChildren<MeshRenderer>().Select(r => r.transform).ToArray();
            var illustrated = locale.Name == "Locale_1" || locale.Name == "Locale_3";
            var names = illustrated
                ? new[] { "Background", "DistantStars", "FarNebula", "StarField", "CloseNebula" }
                : CandidateLayers;
            var queues = illustrated ? new[] { 2900, 2930, 2940, 2950, 2990 } : CandidateQueues;

            if (locale.Name == "Locale_1")
            {
                names = new[] { "Background", "DistantStars", "FarNebula", "TurquoiseNebula", "StarField", "CloseNebula" };
                queues = new[] { 2900, 2930, 2940, 2945, 2950, 2990 };
            }
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
            if (locale.Name == "Locale_1")
                expectedParents = new[] { expectedParents[0], expectedParents[1], expectedParents[2],
                    expectedParents[2], expectedParents[3], expectedParents[4] };
            Assert.That(layers.Select(l => l.name), Is.EqualTo(names));
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

        [TestCase(Locale1)]
        [TestCase(Locale2)]
        [TestCase(Locale3)]
        [TestCase(EditScene)]
        public void CloudBankLayers_BakeAStaticZoomFreeTile(string path)
        {
            using var locale = new OpenLocale(path);
            foreach (var layer in locale.Root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var material = layer.sharedMaterial;
                var cloudBank = material.HasProperty("_CloudOpacity") && material.GetFloat("_CloudOpacity") > 0;
                var tile = layer.GetComponent<CloudBankTile>();
                Assert.That((bool)tile, Is.EqualTo(cloudBank),
                    $"{layer.name}: a cloud-bank sky layer samples a baked tile, so exactly those carry CloudBankTile.");
                if (!cloudBank) continue;
                Assert.That(material.GetFloat("_NebulaSpeed"), Is.Zero, $"{layer.name}: a baked tile cannot animate.");
                Assert.That(material.GetFloat("_NebulaZoomResponse"), Is.Zero,
                    $"{layer.name}: a baked tile cannot respond to zoom.");
            }
        }

        [TestCase("Assets/Scenes/InitScene.unity")]
        [TestCase(EditScene)]
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

        [Test]
        public void BootScene_CarriesNoSky()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/InitScene.unity", OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                Assert.That(scene.GetRootGameObjects().Where(g => g.GetComponentInChildren<LocaleSky>(true)), Is.Empty,
                    "The idle locale is the first sky the player sees; the boot scene holds the host and services.");
                Assert.IsNull(RenderSettings.customReflectionTexture, "The boot scene keeps no reflection cubemap.");
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

        [TestCase(Locale1, false)]
        [TestCase(Locale2, false)]
        [TestCase(Locale3, true)]
        public void LocalePrefab_LightsFromFlatAmbientAndItsOwnReflectionCubemap(string prefabPath, bool hasSun)
        {
            var locale = AssetDatabase.LoadAssetAtPath<LocaleSky>(prefabPath);
            var lighting = new SerializedObject(locale).FindProperty("lighting");
            Assert.That((AmbientMode)lighting.FindPropertyRelative("ambientMode").intValue, Is.EqualTo(AmbientMode.Flat),
                "Ships are lit by the locale's flat ambient colour.");
            Assert.That((DefaultReflectionMode)lighting.FindPropertyRelative("reflectionMode").intValue,
                Is.EqualTo(DefaultReflectionMode.Custom));
            var reflection = lighting.FindPropertyRelative("reflection").objectReferenceValue;
            Assert.That(reflection, Is.InstanceOf<Cubemap>());
            Assert.That(AssetDatabase.GetAssetPath(reflection),
                Does.StartWith($"Assets/Visuals/Locales/Sky/Locales/{locale.name}/"));
            var sun = (Light)lighting.FindPropertyRelative("sun").objectReferenceValue;
            Assert.That((bool)sun, Is.EqualTo(hasSun), "Only Locale_3 names a sun.");
            if (sun) Assert.IsTrue(sun.transform.IsChildOf(locale.transform), "A locale's sun is its own light.");
        }

        [Test]
        public void EditScene_LightsFromFlatAmbientAndALocaleReflectionCubemap()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(EditScene, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat),
                    "Ships are lit by the locale's flat ambient colour.");
                Assert.That(RenderSettings.defaultReflectionMode, Is.EqualTo(DefaultReflectionMode.Custom));
                Assert.That(RenderSettings.customReflectionTexture, Is.InstanceOf<Cubemap>());
                Assert.That(AssetDatabase.GetAssetPath(RenderSettings.customReflectionTexture),
                    Does.StartWith("Assets/Visuals/Locales/Sky/Locales/Locale_2/"));
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
