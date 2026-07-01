using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using World;

namespace Game.Services
{
    public class EnvironmentService : IEnvironmentService
    {
        private string loadedSceneName;

        public WorldRoot World { get; private set; }

        public Transform WorldFollowerTransform =>
            World && World.Follower ? World.Follower.transform : null;

        public SphereCollider AsteroidCullingBoundary =>
            World ? World.AsteroidCullingBoundary : null;

        public IEnumerator LoadSceneAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                yield break;

            if (!SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (loadOp == null)
                    throw new InvalidOperationException(
                        $"Failed to start async load for scene '{sceneName}'. " +
                        "Verify the scene exists and is added to Build Settings.");

                while (!loadOp.isDone)
                    yield return null;

                loadedSceneName = sceneName;
            }
        }

        public IEnumerator UnloadSceneAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                yield break;

            // Only unload scenes this service loaded
            if (loadedSceneName != sceneName)
                yield break;

            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(sceneName);
                while (op != null && !op.isDone)
                    yield return null;
            }

            loadedSceneName = null;
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
            // Detach from the sector so Clear()/lifetime matches a spawned world.
            existing.transform.SetParent(null, true);
        }

        public void Clear()
        {
            if (World != null)
            {
                UnityEngine.Object.Destroy(World.gameObject);
                World = null;
            }
            // Scene unloading is explicit via UnloadSceneAsync, not part of Clear
        }
    }
}
