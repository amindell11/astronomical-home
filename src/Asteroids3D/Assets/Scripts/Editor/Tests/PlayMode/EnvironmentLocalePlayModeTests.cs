#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Game.Services;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// PlayMode coverage for the locale (environment) scene seam on <see cref="EnvironmentService"/>:
    /// apply makes the locale the active scene, a repeat apply is a no-op, a different locale swaps the
    /// active scene (unloading the previous), and restore returns the boot scene to active. Locale
    /// scenes are created empty at runtime so the tests exercise the SetActive/diff/restore paths
    /// without depending on Build Settings.
    /// </summary>
    [TestFixture]
    [Category("Sectors")]
    public class EnvironmentLocalePlayModeTests
    {
        private const string LocaleA = "LocaleTestA";
        private const string LocaleB = "LocaleTestB";

        private Scene _boot;
        private readonly List<string> _created = new();

        [SetUp]
        public void SetUp()
        {
            _boot = SceneManager.GetActiveScene();
            CreateEmpty(LocaleA);
            CreateEmpty(LocaleB);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_boot.IsValid() && _boot.isLoaded)
                SceneManager.SetActiveScene(_boot);

            foreach (var name in _created)
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.isLoaded)
                {
                    var op = SceneManager.UnloadSceneAsync(scene);
                    while (op != null && !op.isDone) yield return null;
                }
            }
            _created.Clear();
        }

        [UnityTest]
        public IEnumerator ApplyLocale_MakesLocaleTheActiveScene()
        {
            var env = new EnvironmentService();
            yield return env.ApplyLocaleAsync(LocaleA);
            Assert.AreEqual(LocaleA, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator ApplySameLocaleTwice_StaysActiveAndLoaded()
        {
            var env = new EnvironmentService();
            yield return env.ApplyLocaleAsync(LocaleA);
            yield return env.ApplyLocaleAsync(LocaleA);
            Assert.AreEqual(LocaleA, SceneManager.GetActiveScene().name);
            Assert.IsTrue(SceneManager.GetSceneByName(LocaleA).isLoaded);
        }

        [UnityTest]
        public IEnumerator ApplyDifferentLocale_SwapsActiveAndUnloadsPrevious()
        {
            var env = new EnvironmentService();
            yield return env.ApplyLocaleAsync(LocaleA);
            yield return env.ApplyLocaleAsync(LocaleB);
            Assert.AreEqual(LocaleB, SceneManager.GetActiveScene().name);
            Assert.IsFalse(SceneManager.GetSceneByName(LocaleA).isLoaded,
                "The previous locale must unload when a different one is applied.");
        }

        [UnityTest]
        public IEnumerator RestoreBoot_RestoresActiveAndUnloadsLocale()
        {
            var env = new EnvironmentService();
            yield return env.ApplyLocaleAsync(LocaleA);
            yield return env.RestoreBootEnvironmentAsync();
            Assert.AreEqual(_boot.handle, SceneManager.GetActiveScene().handle);
            Assert.IsFalse(SceneManager.GetSceneByName(LocaleA).isLoaded,
                "The locale must unload on restore.");
        }

        [UnityTest]
        public IEnumerator ApplyUnassignedLocale_IsNoOp()
        {
            var env = new EnvironmentService();
            var before = SceneManager.GetActiveScene().handle;
            yield return env.ApplyLocaleAsync(null);
            yield return env.ApplyLocaleAsync(string.Empty);
            Assert.AreEqual(before, SceneManager.GetActiveScene().handle);
        }

        private void CreateEmpty(string name)
        {
            SceneManager.CreateScene(name);
            _created.Add(name);
        }
    }
}
#endif
