using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Locales
{
    /// <summary>
    /// Locale switching for one session, held privately by it rather than exposed beside its services:
    /// swap the active (lighting) scene to a sector's authored locale before its content builds, return
    /// to the idle locale — the one shown while no sector is loaded — whenever the sector unloads, and
    /// hand the boot scene back when the session tears down. A travel backdrop retains both skies
    /// across the transfer, releasing the unused locale afterward. The session skips these steps headless.
    /// </summary>
    public class LocaleService
    {
        private readonly Scene bootScene = SceneManager.GetActiveScene();
        private readonly string idleLocaleName;
        private string loadedLocaleName;
        private LocaleBackdropPair backdrop;
        private AsyncOperation backdropUnload;
        private string backdropDestination;

        public LocaleService(string idleLocaleName = null)
        {
            this.idleLocaleName = idleLocaleName;
        }

        public IEnumerator PrepareBackdrop(string destination, Transform parent, Action<LocaleBackdropPair> ready)
        {
            if (backdrop) throw new InvalidOperationException("A locale backdrop is already held.");
            if (backdropUnload != null) yield return backdropUnload;
            backdropUnload = null;
            var from = SceneManager.GetSceneByName(loadedLocaleName);
            if (!from.isLoaded)
                throw new InvalidOperationException("A backdrop requires a loaded departure locale.");
            var to = SceneManager.GetSceneByName(destination);
            var ownsDestination = !to.isLoaded;
            if (ownsDestination)
                yield return SceneManager.LoadSceneAsync(destination, LoadSceneMode.Additive);
            to = SceneManager.GetSceneByName(destination);
            var root = new GameObject("Locale backdrop pair");
            root.transform.SetParent(parent, false);
            var pair = root.AddComponent<LocaleBackdropPair>();
            try
            {
                pair.Initialize(from, to, () =>
                {
                    backdrop = null;
                    backdropDestination = null;
                    if (from != to && loadedLocaleName == destination)
                        backdropUnload = SceneManager.UnloadSceneAsync(from);
                    else if (ownsDestination && to.isLoaded)
                        backdropUnload = SceneManager.UnloadSceneAsync(to);
                });
            }
            catch
            {
                UnityEngine.Object.Destroy(root);
                if (ownsDestination) backdropUnload = SceneManager.UnloadSceneAsync(to);
                throw;
            }
            backdrop = pair;
            backdropDestination = destination;
            ready(backdrop);
        }

        public IEnumerator ApplyLocaleAsync(string localeSceneName)
        {
            if (string.IsNullOrWhiteSpace(localeSceneName) || loadedLocaleName == localeSceneName)
                yield break;

            if (backdrop && localeSceneName != backdropDestination)
                throw new InvalidOperationException("A held travel backdrop can only transfer to its destination.");
            if (!backdrop && !string.IsNullOrEmpty(loadedLocaleName))
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

        /// <summary>With no idle locale configured, unloads to the boot scene.</summary>
        public IEnumerator ApplyIdleLocaleAsync()
        {
            if (string.IsNullOrWhiteSpace(idleLocaleName))
                return UnloadLocaleAsync();
            return ApplyLocaleAsync(idleLocaleName);
        }

        /// <summary>Restore the boot scene as active and unload the applied locale, if any.</summary>
        public IEnumerator UnloadLocaleAsync()
        {
            if (backdropUnload != null) yield return backdropUnload;
            backdropUnload = null;
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
