#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    /// The hangar round trip at session level: a presentation session shows its idle locale from
    /// compose, applies the sector's locale on every load and returns to the idle one on every unload —
    /// keeping a locale both name loaded straight through — so no sector look carries into the hangar,
    /// including the ambient probe and default reflection derived from the active scene's lighting.
    /// </summary>
    [TestFixture]
    [Category("Sectors")]
    public class SessionLocaleRoundTripPlayModeTests
    {
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/ArenaSector.prefab";
        private const string ConfigPath = "Assets/Settings/Game/DefaultSectorConfig.asset";
        private const string OtherLocalePath = "Assets/Scenes/Locales/Locale_1.unity";

        private GameObject root;
        private Scene boot;
        private readonly List<string> locales = new();
        private bool savedAudioPause;

        [SetUp]
        public void SetUp()
        {
            boot = SceneManager.GetActiveScene();
            savedAudioPause = AudioListener.pause;
            AudioListener.pause = true;
        }

        // A failed assertion mid-trip must not leave a locale active for later fixtures.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root) Object.DestroyImmediate(root);
            SceneManager.SetActiveScene(boot);
            foreach (var locale in locales)
                if (SceneManager.GetSceneByName(locale).isLoaded)
                    yield return SceneManager.UnloadSceneAsync(locale);
            locales.Clear();
            AudioListener.pause = savedAudioPause;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator IdleLocaleSharedWithTheSector_StaysLoadedAcrossTheHandOff()
        {
            var config = LoadConfig(out var sectorLocale);
            var session = CreateSession(config, sectorLocale);

            yield return session.Compose();
            Assert.AreEqual(sectorLocale, SceneManager.GetActiveScene().name, "Compose shows the idle locale.");
            var idle = SceneManager.GetActiveScene().handle;

            yield return session.LoadSector();
            Assert.AreEqual(idle, SceneManager.GetActiveScene().handle,
                "A sector naming the idle locale must not unload and reload it.");

            yield return session.UnloadSector();
            Assert.AreEqual(idle, SceneManager.GetActiveScene().handle, "Unloading the sector keeps the shared locale.");

            yield return session.Teardown();
            Assert.AreEqual(boot.handle, SceneManager.GetActiveScene().handle);
            Assert.IsFalse(SceneManager.GetSceneByName(sectorLocale).isLoaded, "Teardown unloads the locale.");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator EachStep_LightsFromTheActiveScenesFlatAmbientAndCustomReflection()
        {
            var config = LoadConfig(out var sectorLocale);
            var other = AssetDatabase.LoadAssetAtPath<SceneAsset>(OtherLocalePath);
            Assert.IsNotNull(other, $"Locale missing at {OtherLocalePath}");
            Assert.AreNotEqual(other.name, sectorLocale, "test premise: the idle and sector locales differ");
            locales.Add(other.name);
            var session = CreateSession(config, other.name);

            yield return session.Compose();
            Assert.AreEqual(other.name, SceneManager.GetActiveScene().name);
            var idleAmbient = RenderSettings.ambientLight;
            var idleReflection = RenderSettings.customReflectionTexture;
            AssertLitByActiveScene("after compose");

            yield return session.LoadSector();
            Assert.AreEqual(sectorLocale, SceneManager.GetActiveScene().name);
            Assert.AreNotEqual(idleAmbient, RenderSettings.ambientLight,
                "test premise: the sector's ambient differs from the idle locale's, so a stale probe would show");
            Assert.AreNotSame(idleReflection, RenderSettings.customReflectionTexture,
                "test premise: the sector's cubemap differs from the idle locale's, so a stale reflection would show");
            AssertLitByActiveScene("after the first load");

            yield return session.UnloadSector();
            Assert.AreEqual(other.name, SceneManager.GetActiveScene().name);
            Assert.IsFalse(SceneManager.GetSceneByName(sectorLocale).isLoaded, "Unloading the sector unloads its locale.");
            AssertLitByActiveScene("after the unload");

            yield return session.LoadSector();
            AssertLitByActiveScene("after re-entry");

            yield return session.Teardown();
        }

        private SectorSettings LoadConfig(out string sectorLocale)
        {
            var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(ConfigPath);
            Assert.IsNotNull(config, $"Sector config missing at {ConfigPath}");
            sectorLocale = config.Locale?.SceneName;
            Assert.That(sectorLocale, Is.Not.Null.And.Not.Empty, "The default sector must name a locale.");
            locales.Add(sectorLocale);
            return config;
        }

        private Session CreateSession(SectorSettings config, string idleLocale)
        {
            root = new GameObject("SessionRoot");
            return TestSession.Create(root, new SessionProfile
            {
                sectorEntry = new SectorEntry
                {
                    prefab = AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath),
                    config = config
                },
                idleLocale = SceneReferenceTo(idleLocale),
                presentation = true
            });
        }

        private static SceneReference SceneReferenceTo(string sceneName)
        {
            var reference = new SceneReference();
            typeof(SceneReference).GetField("sceneName", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(reference, sceneName);
            return reference;
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
