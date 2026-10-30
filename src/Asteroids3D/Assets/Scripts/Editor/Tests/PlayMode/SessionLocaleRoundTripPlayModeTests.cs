#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Services.Locales;
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
    /// The hangar round trip at session level: a presentation session shows its idle locale from compose, shows the
    /// sector's locale on every load and returns to the idle one on every unload, keeping one instance of a locale both
    /// name, so no sector look carries into the hangar, including the ambient probe and default reflection derived from
    /// the shown locale's lighting. The active scene stays the boot scene throughout.
    /// </summary>
    [TestFixture]
    [Category("Sectors")]
    public class SessionLocaleRoundTripPlayModeTests
    {
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/ArenaSector.prefab";
        private const string ConfigPath = "Assets/Settings/Game/DefaultSectorConfig.asset";
        private const string OtherLocalePath = "Assets/Prefabs/Locales/Locale_1.prefab";

        private GameObject root;
        private Scene boot;
        private LightingSnapshot bootLighting;
        private bool savedAudioPause;

        [SetUp]
        public void SetUp()
        {
            boot = SceneManager.GetActiveScene();
            bootLighting = LightingSnapshot.Capture();
            savedAudioPause = AudioListener.pause;
            AudioListener.pause = true;
        }

        // A failed assertion mid-trip must not leave a locale's lighting for later fixtures.
        [TearDown]
        public void TearDown()
        {
            if (root) Object.DestroyImmediate(root);
            bootLighting.Restore();
            AudioListener.pause = savedAudioPause;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator IdleLocaleSharedWithTheSector_KeepsItsInstanceAcrossTheHandOff()
        {
            var config = LoadConfig();
            var session = CreateSession(config, config.Locale);

            yield return session.Compose();
            var idle = Shown();
            Assert.AreEqual(config.Locale.name, idle.name, "Compose shows the idle locale.");

            yield return session.LoadSector();
            Assert.AreSame(idle, Shown(), "A sector naming the idle locale must keep its instance.");

            yield return session.UnloadSector();
            Assert.AreSame(idle, Shown(), "Unloading the sector keeps the shared locale.");

            yield return session.Teardown();
            yield return null;
            Assert.That(Instances(), Is.Empty, "Teardown destroys the locale.");
            Assert.AreEqual(bootLighting.Ambient, RenderSettings.ambientLight, "Teardown restores the boot lighting.");
            Assert.AreEqual(boot, SceneManager.GetActiveScene());
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator EachStep_LightsFromTheShownLocalesFlatAmbientAndCustomReflection()
        {
            var config = LoadConfig();
            var other = AssetDatabase.LoadAssetAtPath<LocaleSky>(OtherLocalePath);
            Assert.IsNotNull(other, $"Locale missing at {OtherLocalePath}");
            Assert.AreNotSame(other, config.Locale, "test premise: the idle and sector locales differ");
            var session = CreateSession(config, other);

            yield return session.Compose();
            Assert.AreEqual(other.name, Shown().name);
            var idleAmbient = RenderSettings.ambientLight;
            var idleReflection = RenderSettings.customReflectionTexture;
            AssertLitByShownLocale("after compose");

            yield return session.LoadSector();
            Assert.AreEqual(config.Locale.name, Shown().name);
            Assert.AreNotEqual(idleAmbient, RenderSettings.ambientLight,
                "test premise: the sector's ambient differs from the idle locale's, so a stale probe would show");
            Assert.AreNotSame(idleReflection, RenderSettings.customReflectionTexture,
                "test premise: the sector's cubemap differs from the idle locale's, so a stale reflection would show");
            AssertLitByShownLocale("after the first load");

            yield return session.UnloadSector();
            yield return null;
            Assert.AreEqual(other.name, Shown().name);
            Assert.That(Instances().Select(i => i.name), Is.EqualTo(new[] { other.name }),
                "Unloading the sector destroys its locale.");
            AssertLitByShownLocale("after the unload");

            yield return session.LoadSector();
            AssertLitByShownLocale("after re-entry");

            yield return session.Teardown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator UnparentedObjectFromTheIdleLocale_SurvivesASectorWithADifferentLocale()
        {
            var config = LoadConfig();
            var other = AssetDatabase.LoadAssetAtPath<LocaleSky>(OtherLocalePath);
            Assert.AreNotSame(other, config.Locale, "test premise: the idle and sector locales differ");
            var session = CreateSession(config, other);

            yield return session.Compose();
            var bare = new GameObject("Unparented after compose");
            try
            {
                yield return session.LoadSector();
                yield return null;
                Assert.IsTrue(bare, "An object created without a parent must outlive the idle locale's hand-off.");
                Assert.AreEqual(boot, SceneManager.GetActiveScene(), "Showing a locale never changes the active scene.");
                yield return session.Teardown();
            }
            finally
            {
                if (bare) Object.Destroy(bare);
            }
        }

        private static SectorSettings LoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(ConfigPath);
            Assert.IsNotNull(config, $"Sector config missing at {ConfigPath}");
            Assert.IsTrue(config.Locale, "The default sector must name a locale.");
            return config;
        }

        private Session CreateSession(SectorSettings config, LocaleSky idleLocale)
        {
            root = new GameObject("SessionRoot");
            return TestSession.Create(root, new SessionProfile
            {
                sectors = new[]
                {
                    new SectorEntry
                    {
                        prefab = AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath),
                        config = config
                    }
                },
                idleLocale = idleLocale,
                presentation = true
            });
        }

        private LocaleSky[] Instances() => root.GetComponentsInChildren<LocaleSky>(true);

        private LocaleSky Shown() => Instances().Single(locale => locale.Shown);

        // Flat ambient fills the probe with the linear ambient colour; ambientIntensity does not scale it.
        private void AssertLitByShownLocale(string step)
        {
            var locale = Shown().name;
            Assert.AreEqual(AmbientMode.Flat, RenderSettings.ambientMode, $"{locale} {step}: test premise");
            var expected = RenderSettings.ambientLight.linear;
            var probe = new Color[1];
            RenderSettings.ambientProbe.Evaluate(new[] { Vector3.up }, probe);
            Assert.That(new[] { probe[0].r, probe[0].g, probe[0].b },
                Is.EqualTo(new[] { expected.r, expected.g, expected.b }).Within(0.005f),
                $"{locale} {step}: the ambient probe must match the shown locale's flat ambient");
            Assert.IsNotNull(RenderSettings.customReflectionTexture, $"{locale} {step}: test premise");
            Assert.AreSame(RenderSettings.customReflectionTexture, ReflectionProbe.defaultTexture,
                $"{locale} {step}: the default reflection must resolve to the shown locale's custom cubemap");
        }
    }
}
#endif
