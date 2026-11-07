using System.Linq;
using Game;
using NUnit.Framework;
using Substrate.Services.Locales;
using Substrate.Sessions;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Tests.EditMode.Rendering.Locales
{
    /// <summary>
    /// The boot scene carries no sky, so every look the player sees comes from a locale prefab: each
    /// row of an authored sector run names one, and the host names the idle locale shown between sectors.
    /// Runs built at runtime (headless and RL sessions, which never show locales) are exempt.
    /// </summary>
    [Category("Sectors")]
    public class LocaleAssignmentEditModeTests
    {
        private static string[] SectorRunPaths() => AssetDatabase.FindAssets($"t:{nameof(SectorRun)}")
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();

        [TestCaseSource(nameof(SectorRunPaths))]
        public void AuthoredSectorRun_NamesALocalePrefabOnEveryRow(string runPath)
        {
            var run = AssetDatabase.LoadAssetAtPath<SectorRun>(runPath);
            Assert.That(run.Sectors, Is.Not.Empty, $"{runPath}: an authored run flies at least one sector.");
            for (var i = 0; i < run.Sectors.Count; i++)
                AssertLocalePrefab(run.Sectors[i].locale, $"{runPath} row {i}");
        }

        [Test]
        public void BootHost_NamesAnIdleLocalePrefab()
        {
            const string scenePath = "Assets/Scenes/InitScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var host = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<GameHost>(true)).Single(h => h);
                AssertLocalePrefab(host.sessionProfile.idleLocale, $"{scenePath} GameHost idle locale");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertLocalePrefab(LocaleSky locale, string owner)
        {
            Assert.IsTrue(locale, $"{owner}: no locale assigned, so it would render on black.");
            Assert.IsTrue(PrefabUtility.IsPartOfPrefabAsset(locale),
                $"{owner}: locale '{locale.name}' must be a prefab asset, not a scene object.");
        }
    }
}
