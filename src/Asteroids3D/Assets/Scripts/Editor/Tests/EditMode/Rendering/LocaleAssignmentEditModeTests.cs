using System.Linq;
using Game;
using NUnit.Framework;
using Substrate.Sectors;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Tests.EditMode.Rendering
{
    /// <summary>
    /// The boot scene carries no sky, so every look the player sees comes from a named locale: each
    /// authored sector config names one, and the host names the idle locale shown between sectors.
    /// Configs built at runtime (headless and RL sessions, which never apply locales) are exempt.
    /// </summary>
    [Category("Sectors")]
    public class LocaleAssignmentEditModeTests
    {
        private static string[] SectorConfigPaths() => AssetDatabase.FindAssets($"t:{nameof(SectorSettings)}")
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();

        [TestCaseSource(nameof(SectorConfigPaths))]
        public void AuthoredSectorConfig_NamesABuiltLocale(string configPath)
        {
            var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(configPath);
            AssertBuiltLocale(config.Locale, configPath);
        }

        [Test]
        public void BootHost_NamesABuiltIdleLocale()
        {
            const string scenePath = "Assets/Scenes/InitScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var host = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<GameHost>(true)).Single(h => h);
                AssertBuiltLocale(host.sessionProfile.idleLocale, $"{scenePath} GameHost idle locale");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertBuiltLocale(SceneReference locale, string owner)
        {
            var name = locale?.SceneName;
            Assert.That(name, Is.Not.Null.And.Not.Empty, $"{owner}: no locale assigned, so it would render on black.");
            Assert.That(EditorBuildSettings.scenes.Where(s => s.enabled)
                    .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path)), Has.Member(name),
                $"{owner}: locale '{name}' is not an enabled Build Settings scene.");
        }
    }
}
