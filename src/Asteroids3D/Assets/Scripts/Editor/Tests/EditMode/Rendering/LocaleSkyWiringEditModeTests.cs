using System.Linq;
using NUnit.Framework;
using Substrate;
using Substrate.Services.Environment;
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
        [TestCase("Assets/Scenes/Environments/Environment_1.unity")]
        [TestCase("Assets/Scenes/Environments/Environment_2.unity")]
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

        [TestCase("Assets/Scenes/Environments/Environment_1.unity", "nebula-glow-warm-flat-final")]
        [TestCase("Assets/Scenes/Environments/Environment_2.unity", "illustrated-blue-final")]
        public void SkyRoot_DrawsPerLocaleVariantsOfItsBackground_InLockedOrder(string scenePath, string background)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var root = roots.Single(g => g.name == "Sky");
                Assert.IsTrue(root.GetComponent<LocaleSky>());
                Assert.IsTrue(root.activeSelf, "The palette-driven sky is the live root.");
                Assert.IsFalse(roots.Single(g => g.name == "Sky (Original)").activeSelf,
                    "The original root stays inactive as a rollback.");
                var layers = root.GetComponentsInChildren<MeshRenderer>().Select(r => r.transform).ToArray();
                var illustrated = scene.name == "Environment_2";
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
                    "Assets/Visuals/Environment/Sky/StarFieldMaterial.mat",
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
                        Does.StartWith($"Assets/Visuals/Environment/Sky/Locales/{scene.name}/"),
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

        [TestCase("Assets/Scenes/Environments/Environment_1.unity")]
        [TestCase("Assets/Scenes/Environments/Environment_2.unity")]
        public void SectorLocale_LightsFromFlatAmbientAndItsOwnReflectionCubemap(string scenePath)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat),
                    "Skybox ambient would light ships from the retired skybox.");
                Assert.That(RenderSettings.defaultReflectionMode, Is.EqualTo(DefaultReflectionMode.Custom));
                Assert.That(RenderSettings.customReflectionTexture, Is.InstanceOf<Cubemap>());
                Assert.That(AssetDatabase.GetAssetPath(RenderSettings.customReflectionTexture),
                    Does.StartWith($"Assets/Visuals/Environment/Sky/Locales/{scene.name}/"));
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
