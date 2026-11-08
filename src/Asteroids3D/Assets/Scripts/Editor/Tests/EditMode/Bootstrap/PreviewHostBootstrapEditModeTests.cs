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
            var run = host.gameHostPrefab.sessionProfile.run;
            Assert.That(host.sessionProfile.run, Is.SameAs(run), "Replay flies the game's own sector run");
            Assert.That(host.crowdedJumpSectors.Select(entry => entry.prefab.name),
                Is.EqualTo(new[] { "CrowdedJumpDeparture", "CrowdedJumpArrival" }));
            Assert.That(host.crowdedJumpSectors.Select(entry => entry.locale.name),
                Is.EqualTo(new[] { "IllustratedBlue", "CopperTurquoise" }));
            var hud = new[] { host.hudQuietSector, host.hudExtractionSector };
            Assert.That(hud.Select(entry => entry.prefab.name), Is.EqualTo(new[] { "HudQuietSector", "HudExtractionSector" }));
            Assert.That(hud.Select(entry => entry.locale), Is.All.SameAs(run.Sectors[0].locale),
                "the HUD fixtures show the run's first locale");
            Assert.That(host.bossSector.locale, Is.SameAs(run.Sectors[^1].locale), "the boss shows the run's last locale");
            Assert.IsTrue(host.sector1EliteSector.prefab, "sector 1's elite fixture loads");
            Assert.AreEqual("EliteSector1", host.sector1EliteSector.prefab.name);
            Assert.That(host.sector1EliteSector.locale, Is.SameAs(run.Sectors[0].locale),
                "sector 1's elite shows the run's first locale");
            Assert.IsTrue(host.sector2EliteSector.prefab, "sector 2's elite fixture loads");
            Assert.AreEqual("EliteSector2", host.sector2EliteSector.prefab.name);
            Assert.That(host.sector2EliteSector.locale, Is.SameAs(run.Sectors[1].locale),
                "sector 2's elite shows the run's second locale");
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
