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
        private const string CopperTurquoise = "Assets/Prefabs/Locales/CopperTurquoise.prefab";
        private const string NebulaGlow = "Assets/Prefabs/Locales/NebulaGlow.prefab";
        private const string IllustratedBlue = "Assets/Prefabs/Locales/IllustratedBlue.prefab";
        private const string CoralSunset = "Assets/Prefabs/Locales/CoralSunset.prefab";
        private const string CyanAurora = "Assets/Prefabs/Locales/CyanAurora.prefab";
        private const string VerdantGiant = "Assets/Prefabs/Locales/VerdantGiant.prefab";
        private const string BlueCrescent = "Assets/Prefabs/Locales/BlueCrescent.prefab";
        private const string EditScene = "Assets/Scenes/EditScene.unity";
        private static readonly string[] CandidateLayers = { "Background", "FarNebula", "StarField", "CloseNebula" };
        private static readonly int[] CandidateQueues = { 2900, 2940, 2950, 2990 };

        [TestCase(CopperTurquoise)]
        [TestCase(NebulaGlow)]
        [TestCase(IllustratedBlue)]
        [TestCase(CoralSunset)]
        [TestCase(CyanAurora)]
        [TestCase(VerdantGiant)]
        [TestCase(BlueCrescent)]
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

        [TestCase(CopperTurquoise, "copper-turquoise-final")]
        [TestCase(NebulaGlow, "nebula-glow-flat-final")]
        [TestCase(IllustratedBlue, "illustrated-blue-final")]
        [TestCase(EditScene, "nebula-glow-flat-final", "NebulaGlow")]
        public void SkyRoot_DrawsPerLocaleVariantsOfItsBackground_InLockedOrder(
            string path, string background, string localeFolder = null)
        {
            using var locale = new OpenLocale(path);
            localeFolder ??= locale.Name;
            var root = locale.Sky;
            Assert.IsTrue(locale.Root);
            Assert.IsTrue(root.gameObject.activeSelf, "The palette-driven sky is the live root.");
            var layers = root.GetComponentsInChildren<MeshRenderer>().Select(r => r.transform).ToArray();
            var illustrated = locale.Name == "CopperTurquoise" || locale.Name == "IllustratedBlue";
            var names = illustrated
                ? new[] { "Background", "DistantStars", "FarNebula", "StarField", "CloseNebula" }
                : CandidateLayers;
            var queues = illustrated ? new[] { 2900, 2930, 2940, 2950, 2990 } : CandidateQueues;

            if (locale.Name == "CopperTurquoise")
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
            if (locale.Name == "CopperTurquoise")
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

        [TestCase(CopperTurquoise)]
        [TestCase(NebulaGlow)]
        [TestCase(IllustratedBlue)]
        [TestCase(CoralSunset)]
        [TestCase(CyanAurora)]
        [TestCase(VerdantGiant)]
        [TestCase(BlueCrescent)]
        [TestCase(EditScene)]
        public void CloudBankLayers_UseZoomFreeTilesAndPreserveOriginalStationaryLocales(string path)
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
                if (path == CopperTurquoise || path == NebulaGlow || path == IllustratedBlue || path == EditScene)
                    Assert.That(material.GetFloat("_NebulaSpeed"), Is.Zero, $"{layer.name}: original clouds stay stationary.");
                else
                    Assert.That(material.GetFloat("_NebulaSpeed"), Is.GreaterThan(0), $"{layer.name}: new clouds evolve.");
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

        [TestCase(CopperTurquoise, false)]
        [TestCase(NebulaGlow, false)]
        [TestCase(IllustratedBlue, true)]
        [TestCase(CoralSunset, true)]
        [TestCase(CyanAurora, true)]
        [TestCase(VerdantGiant, true)]
        [TestCase(BlueCrescent, true)]
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
            Assert.That((bool)sun, Is.EqualTo(hasSun));
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
                    Does.StartWith("Assets/Visuals/Locales/Sky/Locales/NebulaGlow/"));
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
