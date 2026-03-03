using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Session
{
    internal sealed class SessionEnvironmentLoader
    {
        private readonly Transform referencePlane;

        public SessionEnvironmentLoader(Transform referencePlane)
        {
            this.referencePlane = referencePlane ? referencePlane : throw new ArgumentNullException(nameof(referencePlane));
        }

        public IEnumerator LoadEnvironment(SectorSessionConfig config, Action onSceneLoadedBySession)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (config.LoadWorldScene)
            {
                yield return LoadWorldScene(config.WorldSceneName, onSceneLoadedBySession);
            }
            else
            {
                AlignReferencePlane();
            }
        }

        public void UnloadOwnedWorldScene(SectorSessionConfig config, bool worldSceneLoadedBySession)
        {
            if (!worldSceneLoadedBySession)
                return;
            if (config == null || string.IsNullOrWhiteSpace(config.WorldSceneName))
                return;

            var scene = SceneManager.GetSceneByName(config.WorldSceneName);
            if (scene.isLoaded)
                SceneManager.UnloadSceneAsync(config.WorldSceneName);
        }

        private IEnumerator LoadWorldScene(string worldSceneName, Action onSceneLoadedBySession)
        {
            if (string.IsNullOrWhiteSpace(worldSceneName))
                yield break;

            if (!SceneManager.GetSceneByName(worldSceneName).isLoaded)
            {
                var loadOp = SceneManager.LoadSceneAsync(worldSceneName, LoadSceneMode.Additive);
                if (loadOp == null)
                    throw new InvalidOperationException($"Failed to start async load for scene '{worldSceneName}'. Verify the scene exists and is added to Build Settings.");

                while (!loadOp.isDone)
                    yield return null;

                onSceneLoadedBySession?.Invoke();
            }

            AlignReferencePlane();
        }

        private void AlignReferencePlane()
        {
            referencePlane.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
        }
    }
}
