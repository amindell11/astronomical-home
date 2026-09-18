using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Environment
{
    /// <summary>
    /// Locale switching for one session, held privately by it rather than exposed beside its services:
    /// swap the active (lighting) scene to a sector's authored locale before its content builds, and
    /// put the boot scene's lighting back at teardown. Both steps are presentation-only — the session
    /// skips them headless — and nothing outside the session ever calls them.
    /// </summary>
    public class LocaleService
    {
        private readonly Scene bootScene = SceneManager.GetActiveScene();
        private string loadedLocaleName;

        /// <summary>Make the named scene the active (lighting) scene, additively loading it and unloading the prior locale; no-op when empty (inherit boot lighting) or already applied.</summary>
        public IEnumerator ApplyLocaleAsync(string localeSceneName)
        {
            if (string.IsNullOrWhiteSpace(localeSceneName) || loadedLocaleName == localeSceneName)
                yield break;

            if (!string.IsNullOrEmpty(loadedLocaleName))
                yield return UnloadLocaleAsync(loadedLocaleName);

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
                MakeActive(scene);
            loadedLocaleName = localeSceneName;
        }

        /// <summary>Restore the boot scene as active and unload the applied locale, if any.</summary>
        public IEnumerator RestoreBootEnvironmentAsync()
        {
            if (string.IsNullOrEmpty(loadedLocaleName))
                yield break;

            // Re-activate boot BEFORE the unload so RenderSettings never resolve against a scene going away this frame.
            if (bootScene.IsValid() && bootScene.isLoaded)
                MakeActive(bootScene);

            yield return UnloadLocaleAsync(loadedLocaleName);
            loadedLocaleName = null;
        }

        // SetActiveScene does not recompute the skybox-derived ambient/reflection probe; refresh it here.
        private static void MakeActive(Scene scene)
        {
            SceneManager.SetActiveScene(scene);
            DynamicGI.UpdateEnvironment();
        }

        private static IEnumerator UnloadLocaleAsync(string sceneName)
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
