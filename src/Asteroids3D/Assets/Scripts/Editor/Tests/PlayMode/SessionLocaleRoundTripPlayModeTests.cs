#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// The hangar round trip at session level: a presentation session applies the sector's locale on
    /// every load and hands the boot scene back on every unload, so no sector look carries into the hangar —
    /// including the ambient probe and default reflection derived from the active scene's lighting.
    /// </summary>
    [TestFixture]
    [Category("Sectors")]
    public class SessionLocaleRoundTripPlayModeTests
    {
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/ArenaSector.prefab";
        private const string ConfigPath = "Assets/Settings/Game/DefaultSectorConfig.asset";
        private const string BootCubemapPath = "Assets/Visuals/Locales/Sky/Locales/InitScene/ReflectionCubemap.asset";

        private GameObject root;
        private Scene boot;
        private string locale;
        private bool savedAudioPause;
        private AmbientMode savedAmbientMode;
        private Color savedAmbientLight;
        private DefaultReflectionMode savedReflectionMode;
        private Texture savedReflection;

        [SetUp]
        public void SetUp()
        {
            boot = SceneManager.GetActiveScene();
            savedAudioPause = AudioListener.pause;
            AudioListener.pause = true;
            savedAmbientMode = RenderSettings.ambientMode;
            savedAmbientLight = RenderSettings.ambientLight;
            savedReflectionMode = RenderSettings.defaultReflectionMode;
            savedReflection = RenderSettings.customReflectionTexture;
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
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientLight = savedAmbientLight;
            RenderSettings.defaultReflectionMode = savedReflectionMode;
            RenderSettings.customReflectionTexture = savedReflection;
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

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator EachStep_LightsFromTheActiveScenesFlatAmbientAndCustomReflection()
        {
            // The runner's scene stands in for InitScene, lit the way InitScene is authored.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.95f, 0.97f, 1.05f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(BootCubemapPath);
            Assert.IsNotNull(RenderSettings.customReflectionTexture, $"Boot cubemap missing at {BootCubemapPath}");
            var bootAmbient = RenderSettings.ambientLight;
            var bootReflection = RenderSettings.customReflectionTexture;

            var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(ConfigPath);
            locale = config.Locale?.SceneName;
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
            Assert.AreEqual(locale, SceneManager.GetActiveScene().name);
            Assert.AreNotEqual(bootAmbient, RenderSettings.ambientLight,
                "test premise: the locale's ambient differs from boot, so a stale probe would show");
            Assert.AreNotSame(bootReflection, RenderSettings.customReflectionTexture,
                "test premise: the locale's cubemap differs from boot, so a stale reflection would show");
            AssertLitByActiveScene("after the first load");

            yield return session.UnloadSector();
            Assert.AreEqual(boot.handle, SceneManager.GetActiveScene().handle);
            AssertLitByActiveScene("after the unload");

            yield return session.LoadSector();
            AssertLitByActiveScene("after re-entry");

            yield return session.Teardown();
        }

        // Flat ambient fills the probe with the linear ambient colour; ambientIntensity does not scale it.
        private static void AssertLitByActiveScene(string step)
        {
            var scene = SceneManager.GetActiveScene().name;
            Assert.AreEqual(AmbientMode.Flat, RenderSettings.ambientMode, $"{scene} {step}: test premise");
            var expected = RenderSettings.ambientLight.linear;
            var probe = new Color[1];
            RenderSettings.ambientProbe.Evaluate(new[] { Vector3.up }, probe);
            Assert.That(new[] { probe[0].r, probe[0].g, probe[0].b },
                Is.EqualTo(new[] { expected.r, expected.g, expected.b }).Within(0.005f),
                $"{scene} {step}: the ambient probe must match the active scene's flat ambient");
            Assert.IsNotNull(RenderSettings.customReflectionTexture, $"{scene} {step}: test premise");
            Assert.AreSame(RenderSettings.customReflectionTexture, ReflectionProbe.defaultTexture,
                $"{scene} {step}: the default reflection must resolve to the active scene's custom cubemap");
        }
    }
}
#endif
