using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using World;

namespace Game.Services
{
    public class EnvironmentService : IEnvironmentService
    {
        private readonly Scene bootScene;
        private string loadedLocaleName;

        public EnvironmentService()
        {
            bootScene = SceneManager.GetActiveScene();
        }

        public WorldRoot World { get; private set; }

        public Transform WorldFollowerTransform =>
            World && World.Follower ? World.Follower.transform : null;

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

        public IEnumerator RestoreBootEnvironmentAsync()
        {
            if (string.IsNullOrEmpty(loadedLocaleName))
                yield break;

            // Restore the boot scene as active before unloading the locale so RenderSettings never
            // resolve against a scene that is going away this frame.
            if (bootScene.IsValid() && bootScene.isLoaded)
                MakeActive(bootScene);

            yield return UnloadLocaleAsync(loadedLocaleName);
            loadedLocaleName = null;
        }

        public void HomeToStableScene(GameObject go)
        {
            if (!go || !bootScene.IsValid() || !bootScene.isLoaded) return;
            if (go.scene != bootScene)
                SceneManager.MoveGameObjectToScene(go, bootScene);
        }

        public void SpawnWorld(WorldRoot prefab)
        {
            if (!prefab) return;
            World = UnityEngine.Object.Instantiate(prefab);
        }

        public void AdoptWorld(WorldRoot existing)
        {
            if (!existing) return;
            World = existing;
            existing.transform.SetParent(null, true);
        }

        public void Clear()
        {
            if (!World) return;
            UnityEngine.Object.Destroy(World.gameObject);
            World = null;
        }

        // SetActiveScene switches RenderSettings but does not recompute the skybox-derived
        // ambient/reflection probe; refresh it so hulls light against the newly-active sky.
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
