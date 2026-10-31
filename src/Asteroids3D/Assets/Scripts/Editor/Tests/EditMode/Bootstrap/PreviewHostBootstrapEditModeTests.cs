using System.Linq;
using Game;
using Game.Preview;
using NUnit.Framework;
using UI.Projection;
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

        [TestCase(PreviewEntry.RunRecap)]
        [TestCase(PreviewEntry.Combat)]
        public void BuildHost_AssignsEntryTheShippedGameHostAndTheFixtureSectors(PreviewEntry entry)
        {
            host = PreviewHostBootstrap.BuildHost(entry);

            Assert.AreEqual(entry, host.Entry);
            Assert.IsFalse(host.gameObject.activeSelf, "the host waits for play mode to take the snapshot");
            Assert.AreEqual(PreviewHostBootstrap.GameHostPath, AssetDatabase.GetAssetPath(host.gameHostPrefab),
                "Playtest runs the GameHost prefab the boot scene instances");
            Assert.IsTrue(host.sessionProfile.presentation, "the preview shows presentation");
            Assert.IsTrue(host.sessionProfile.sectors[0].prefab, "replay sector prefab assigned");
            Assert.IsTrue(host.sessionProfile.sectors[0].config, "replay sector config assigned");
            Assert.That(host.crowdedJumpSectors.Select(entry => entry.prefab.name),
                Is.EqualTo(new[] { "CrowdedJumpDeparture", "CrowdedJumpArrival" }));
            Assert.That(host.crowdedJumpSectors.All(entry => entry.config), Is.True, "crowded jump configs assigned");
        }

        [Test]
        public void Activate_SnapshotsTheFeelsTheGameShips_ThenActivatesTheHost()
        {
            host = PreviewHostBootstrap.BuildHost();
            var snapshot = FeelSnapshot.instance;
            try
            {
                PreviewHostBootstrap.Activate(host, snapshot);

                Assert.IsTrue(host.gameObject.activeSelf);
                var game = host.gameHostPrefab;
                var canopy = AssetDatabase.LoadAssetAtPath<CanopyProjectionProfile>("Assets/Settings/UI/CanopyProjection.asset");
                Assert.That(HudTarget.CanopyProfile(game), Is.SameAs(canopy), "the rig's canopy prefab ships it");
                foreach (var feel in TuningTarget.ProfilesOfAll(game).Append(canopy))
                    Assert.IsFalse(snapshot.HasUnsavedEdits(feel), $"{feel.name} is under the snapshot");
            }
            finally
            {
                snapshot.Close();
            }
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
