#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Substrate.Services.Locales;
using Tests.PlayMode.Common.Locales;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// PlayMode coverage for <see cref="LocaleService"/>: showing a locale instantiates it under the root and applies
    /// its lighting, a repeat show keeps the instance, a different locale replaces it, a shared idle locale keeps its
    /// instance across the hand-off, and showing none restores the boot lighting. The active scene never changes.
    /// Locales are runtime-built templates, so the tests depend on no locale asset.
    /// </summary>
    [TestFixture]
    [Category("Sectors")]
    public class LocalePlayModeTests
    {
        private static readonly Color AmbientA = new(0.3f, 0.1f, 0.1f);
        private static readonly Color AmbientB = new(0.1f, 0.3f, 0.1f);

        private Scene boot;
        private GameObject templates;
        private GameObject root;
        private LocaleSky localeA;
        private LocaleSky localeB;
        private LightingSnapshot bootLighting;

        [SetUp]
        public void SetUp()
        {
            boot = SceneManager.GetActiveScene();
            bootLighting = LightingSnapshot.Capture();
            templates = new GameObject("Locale templates");
            templates.SetActive(false);
            localeA = Template("LocaleTestA", AmbientA);
            localeB = Template("LocaleTestB", AmbientB);
            root = new GameObject("Locale root");
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(root);
            Object.Destroy(templates);
            bootLighting.Restore();
        }

        [UnityTest]
        public IEnumerator Show_InstantiatesTheLocaleUnderTheRoot_AndAppliesItsLighting()
        {
            var locales = new LocaleService(root.transform);
            locales.Show(localeA);
            yield return null;
            Assert.That(Instances().Select(i => i.name), Is.EqualTo(new[] { localeA.name }));
            Assert.IsTrue(Instances()[0].Shown);
            Assert.AreEqual(AmbientA, RenderSettings.ambientLight);
            Assert.AreEqual(boot, SceneManager.GetActiveScene(), "showing a locale never changes the active scene");
        }

        [UnityTest]
        public IEnumerator ShowSameLocaleTwice_KeepsTheInstance()
        {
            var locales = new LocaleService(root.transform);
            locales.Show(localeA);
            var shown = Instances().Single();
            locales.Show(localeA);
            yield return null;
            Assert.That(Instances(), Is.EqualTo(new[] { shown }));
        }

        [UnityTest]
        public IEnumerator ShowDifferentLocale_ReplacesTheInstance()
        {
            var locales = new LocaleService(root.transform);
            locales.Show(localeA);
            locales.Show(localeB);
            yield return null;
            Assert.That(Instances().Select(i => i.name), Is.EqualTo(new[] { localeB.name }),
                "The previous locale must be destroyed when a different one is shown.");
            Assert.AreEqual(AmbientB, RenderSettings.ambientLight);
        }

        [UnityTest]
        public IEnumerator SectorNamingTheIdleLocale_KeepsTheSameInstance()
        {
            var locales = new LocaleService(root.transform, localeA);
            locales.ShowIdle();
            var idle = Instances().Single();
            locales.Show(localeA);
            locales.ShowIdle();
            yield return null;
            Assert.That(Instances(), Is.EqualTo(new[] { idle }),
                "A shared locale must survive the hand-off both ways without a new instance.");
        }

        [UnityTest]
        public IEnumerator ShowIdle_WithNoIdleLocale_RestoresTheBootLighting()
        {
            var locales = new LocaleService(root.transform);
            locales.Show(localeA);
            locales.ShowIdle();
            yield return null;
            Assert.That(Instances(), Is.Empty);
            Assert.AreEqual(bootLighting.Ambient, RenderSettings.ambientLight);
        }

        [UnityTest]
        public IEnumerator Clear_RestoresTheBootLightingAndDestroysTheLocale()
        {
            var locales = new LocaleService(root.transform, localeA);
            locales.ShowIdle();
            locales.Clear();
            yield return null;
            Assert.That(Instances(), Is.Empty, "The locale must be destroyed on restore.");
            Assert.AreEqual(bootLighting.Ambient, RenderSettings.ambientLight);
        }

        [UnityTest]
        public IEnumerator ShowUnassignedLocale_KeepsTheShownOne()
        {
            var locales = new LocaleService(root.transform);
            locales.Show(localeA);
            var shown = Instances().Single();
            locales.Show(null);
            yield return null;
            Assert.That(Instances(), Is.EqualTo(new[] { shown }));
        }

        private LocaleSky[] Instances() => root.GetComponentsInChildren<LocaleSky>(true);

        private LocaleSky Template(string name, Color ambient)
        {
            var go = new GameObject(name);
            go.transform.SetParent(templates.transform, false);
            var sky = go.AddComponent<LocaleSky>();
            var lighting = new SerializedObject(sky);
            lighting.FindProperty("lighting.ambientMode").intValue = (int)UnityEngine.Rendering.AmbientMode.Flat;
            lighting.FindProperty("lighting.ambientSkyColor").colorValue = ambient;
            lighting.ApplyModifiedPropertiesWithoutUndo();
            return sky;
        }
    }
}
#endif
