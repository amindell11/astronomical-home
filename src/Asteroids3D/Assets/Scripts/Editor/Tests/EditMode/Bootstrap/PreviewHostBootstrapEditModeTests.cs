using System.Linq;
using Game;
using Game.Preview;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.EditMode.Bootstrap
{
    [Category("Bootstrap")]
    public class PreviewHostBootstrapEditModeTests
    {
        private PreviewHost host;

        [TearDown]
        public void TearDown()
        {
            if (host) Object.DestroyImmediate(host.gameObject);
        }

        [TestCase(PreviewMode.Inspection)]
        [TestCase(PreviewMode.Playtest)]
        public void BuildHost_AssignsModeTheShippedGameHostAndTheFixtureSectors(PreviewMode mode)
        {
            host = PreviewHostBootstrap.BuildHost(PreviewSubjectKind.RunRecap, mode);

            Assert.AreEqual(mode, host.Mode);
            Assert.IsTrue(host.gameObject.activeSelf, "the configured host is ready for play mode");
            Assert.AreEqual(PreviewHostBootstrap.GameHostPath, AssetDatabase.GetAssetPath(host.gameHostPrefab),
                "Playtest runs the GameHost prefab the boot scene instances");
            Assert.IsTrue(host.sessionProfile.presentation, "the preview shows presentation");
            Assert.IsTrue(host.sessionProfile.sectors[0].prefab, "inspection sector prefab assigned");
            Assert.IsTrue(host.sessionProfile.sectors[0].config, "inspection sector config assigned");
            Assert.That(host.crowdedJumpSectors.Select(entry => entry.prefab.name),
                Is.EqualTo(new[] { "CrowdedJumpDeparture", "CrowdedJumpArrival" }));
            Assert.That(host.crowdedJumpSectors.All(entry => entry.config), Is.True, "crowded jump configs assigned");
        }

        [Test]
        public void BootScene_InstancesTheGameHostPrefab_WithNoOverrides()
        {
            const string scenePath = "Assets/Scenes/InitScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var bootHost = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<GameHost>(true)).Single(h => h);
                Assert.AreEqual(PreviewHostBootstrap.GameHostPath,
                    AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(bootHost)));
                Assert.IsFalse(PrefabUtility.HasPrefabInstanceAnyOverrides(bootHost.gameObject, false),
                    "Playtest runs the prefab, so a boot-scene override would split it from the shipped game");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
