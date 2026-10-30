using System.Linq;
using Game;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Services.Locales;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Tests.EditMode.Rendering
{
    /// <summary>
    /// The boot scene carries no sky, so every look the player sees comes from a locale prefab: each
    /// authored sector config names one, and the host names the idle locale shown between sectors.
    /// Configs built at runtime (headless and RL sessions, which never show locales) are exempt.
    /// </summary>
    [Category("Sectors")]
    public class LocaleAssignmentEditModeTests
    {
        private static string[] SectorConfigPaths() => AssetDatabase.FindAssets($"t:{nameof(SectorSettings)}")
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();

        [TestCaseSource(nameof(SectorConfigPaths))]
        public void AuthoredSectorConfig_NamesALocalePrefab(string configPath)
        {
            var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(configPath);
            AssertLocalePrefab(config.Locale, configPath);
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
