using System;
using System.Collections;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Locales
{
    /// <summary>
    /// Locale switching for one session, held privately by it rather than exposed beside its services:
    /// swap the active (lighting) scene to a sector's authored locale before its content builds, return
    /// to the idle locale — the one shown while no sector is loaded — whenever the sector unloads, and
    /// hand the boot scene back when the session tears down. Every step is presentation-only — the
    /// session skips them headless — and nothing outside the session ever calls them.
    /// </summary>
    public class LocaleService
    {
        private readonly Scene bootScene = SceneManager.GetActiveScene();
        private readonly string idleLocaleName;
        private string loadedLocaleName;

        public LocaleService(string idleLocaleName = null)
        {
            this.idleLocaleName = idleLocaleName;
        }

        /// <summary>Make the named scene the active (lighting) scene, additively loading it and unloading the prior locale; no-op when empty or already applied.</summary>
        public IEnumerator ApplyLocaleAsync(string localeSceneName)
        {
            if (string.IsNullOrWhiteSpace(localeSceneName) || loadedLocaleName == localeSceneName)
                yield break;

            if (!string.IsNullOrEmpty(loadedLocaleName))
                yield return UnloadSceneAsync(loadedLocaleName);

            var scene = SceneManager.GetSceneByName(localeSceneName);
            if (!scene.isLoaded)
            {
                var loadOp = SceneManager.LoadSceneAsync(localeSceneName, LoadSceneMode.Additive);
                if (loadOp == null)
                    throw new InvalidOperationException(
                        $"Failed to load locale scene '{localeSceneName}'. " +
                        "Verify it exists and is enabled in Build Settings.");

                while (!loadOp.isDone)
                    yield return null;

                scene = SceneManager.GetSceneByName(localeSceneName);
            }

            if (scene.isLoaded)
                SceneManager.SetActiveScene(scene);
            loadedLocaleName = localeSceneName;
        }

        /// <summary>Apply the idle locale, keeping it loaded when it is already applied; with none configured, unload to the boot scene.</summary>
        public IEnumerator ApplyIdleLocaleAsync()
        {
            if (string.IsNullOrWhiteSpace(idleLocaleName))
                return UnloadLocaleAsync();
            return ApplyLocaleAsync(idleLocaleName);
        }

        /// <summary>Restore the boot scene as active and unload the applied locale, if any.</summary>
        public IEnumerator UnloadLocaleAsync()
        {
            if (string.IsNullOrEmpty(loadedLocaleName))
                yield break;

            // Re-activate boot BEFORE the unload so RenderSettings never resolve against a scene going away this frame.
            if (bootScene.IsValid() && bootScene.isLoaded)
                SceneManager.SetActiveScene(bootScene);

            yield return UnloadSceneAsync(loadedLocaleName);
            loadedLocaleName = null;
        }

        private static IEnumerator UnloadSceneAsync(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(sceneName);
                while (op != null && !op.isDone)
                    yield return null;
            }
        }
    }
}
