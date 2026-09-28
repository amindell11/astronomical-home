#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// The hangar round trip at session level: a presentation session applies the sector's locale on
    /// every load and hands the boot scene back on every unload, so no sector look carries into the hangar.
    /// </summary>
    [TestFixture]
    [Category("Sectors")]
    public class SessionLocaleRoundTripPlayModeTests
    {
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/ArenaSector.prefab";
        private const string ConfigPath = "Assets/Settings/Game/DefaultSectorConfig.asset";

        private GameObject root;
        private Scene boot;
        private string locale;
        private bool savedAudioPause;

        [SetUp]
        public void SetUp()
        {
            boot = SceneManager.GetActiveScene();
            savedAudioPause = AudioListener.pause;
            AudioListener.pause = true;
        }

        // A failed assertion mid-trip must not leave the locale active for later fixtures.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root) Object.DestroyImmediate(root);
            SceneManager.SetActiveScene(boot);
            if (!string.IsNullOrEmpty(locale) && SceneManager.GetSceneByName(locale).isLoaded)
                yield return SceneManager.UnloadSceneAsync(locale);
            AudioListener.pause = savedAudioPause;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator UnloadSector_RestoresTheBootScene_AndReloadReappliesTheLocale()
        {
            var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(ConfigPath);
            Assert.IsNotNull(config, $"Sector config missing at {ConfigPath}");
            locale = config.Locale?.SceneName;
            Assert.That(locale, Is.Not.Null.And.Not.Empty, "The default sector must name a locale for this round trip to mean anything.");

            root = new GameObject("SessionRoot");
            var session = TestSession.Create(root, new SessionProfile
            {
                sectorEntry = new SectorEntry
                {
                    prefab = AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath),
                    config = config
                },
                presentation = true
            });

            yield return session.Compose();
            yield return session.LoadSector();
            Assert.AreEqual(locale, SceneManager.GetActiveScene().name, "The first load makes the locale active.");

            yield return session.UnloadSector();
            Assert.AreEqual(boot.handle, SceneManager.GetActiveScene().handle,
                "Unloading the sector must hand the active scene back to the boot scene.");
            Assert.IsFalse(SceneManager.GetSceneByName(locale).isLoaded, "Unloading the sector must unload its locale.");

            yield return session.LoadSector();
            Assert.AreEqual(locale, SceneManager.GetActiveScene().name, "Re-entry reapplies the locale.");

            yield return session.Teardown();
            Assert.AreEqual(boot.handle, SceneManager.GetActiveScene().handle);
        }
    }
}
#endif
