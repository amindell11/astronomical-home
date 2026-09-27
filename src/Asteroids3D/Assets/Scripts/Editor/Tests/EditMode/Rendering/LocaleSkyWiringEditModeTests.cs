using System.Linq;
using NUnit.Framework;
using Substrate;
using Substrate.Services.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
        [TestCase("Assets/Scenes/Environments/Environment_2.unity", "nebula-glow-flat-final")]
        public void CandidateRoot_DrawsPerLocaleVariantsOfItsBackground_InLockedOrder(string scenePath, string background)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().Single(g => g.name == "Sky (Candidate)");
                Assert.IsTrue(root.GetComponent<LocaleSky>());
                Assert.IsFalse(root.activeSelf, "The candidate root ships inactive beside the original.");
                var layers = root.transform.Cast<Transform>().ToArray();
                Assert.That(layers.Select(l => l.name), Is.EqualTo(CandidateLayers));

                var sidecar = $"{FlatBackgroundImport.Folder}/{background}.json";
                var expectedParents = new[]
                {
                    $"{FlatBackgroundImport.Folder}/{background}.mat",
                    PaletteParents.PathFor(sidecar, "FarNebula"),
                    "Assets/Visuals/Environment/Sky/StarFieldMaterial.mat",
                    PaletteParents.PathFor(sidecar, "CloseNebula"),
                };
                for (var i = 0; i < layers.Length; i++)
                {
                    var material = layers[i].GetComponent<MeshRenderer>().sharedMaterial;
                    Assert.That(layers[i].gameObject.layer, Is.EqualTo(LayerIds.Sky), $"{layers[i].name} layer");
                    Assert.That(AssetDatabase.GetAssetPath(material),
                        Does.StartWith($"Assets/Visuals/Environment/Sky/Locales/{scene.name}/"),
                        $"The candidate {layers[i].name} sky layer must use a per-locale material variant.");
                    Assert.That(AssetDatabase.GetAssetPath(material.parent), Is.EqualTo(expectedParents[i]));
                    Assert.That(material.renderQueue, Is.EqualTo(CandidateQueues[i]), $"{layers[i].name} draw order");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
