using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Environment
{
    public sealed class SkyboxPreviewSession : IDisposable
    {
        private readonly Scene scene;
        private readonly Material original;
        private readonly Material candidate;
        private bool disposed;

        public SkyboxPreviewSession(Material candidate)
        {
            if (!candidate)
                throw new ArgumentException("Choose a skybox material.", nameof(candidate));
            scene = SceneManager.GetActiveScene();
            original = RenderSettings.skybox;
            this.candidate = candidate;
            ShowCandidate(true);
        }

        public void ShowCandidate(bool show)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(SkyboxPreviewSession));
            if (SceneManager.GetActiveScene() != scene)
                throw new InvalidOperationException("End the skybox preview before changing the active environment.");
            RenderSettings.skybox = show ? candidate : original;
            DynamicGI.UpdateEnvironment();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            var active = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            RenderSettings.skybox = original;
            DynamicGI.UpdateEnvironment();
            if (active.IsValid() && active.isLoaded)
                SceneManager.SetActiveScene(active);
        }
    }
}
