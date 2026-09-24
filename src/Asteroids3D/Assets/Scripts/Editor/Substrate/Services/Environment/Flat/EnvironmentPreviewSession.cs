using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Environment.Flat
{
    public sealed class EnvironmentPreviewSession : IDisposable
    {
        private readonly EnvironmentAuthoring environment;
        private readonly bool temporary;
        private readonly Scene scene;
        private bool disposed;

        public EnvironmentPreviewSession(FlatBackgroundAsset candidate)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Enter Play Mode to compare in the flight camera.");
            if (!candidate) throw new ArgumentException("Choose an imported background.", nameof(candidate));
            scene = SceneManager.GetActiveScene();
            environment = EnvironmentPreviewWindow.FindEnvironment(scene);
            temporary = !environment;
            if (temporary)
            {
                var root = new GameObject("Environment (Preview)") { hideFlags = HideFlags.DontSave };
                SceneManager.MoveGameObjectToScene(root, scene);
                environment = root.AddComponent<EnvironmentAuthoring>();
                var serialized = new SerializedObject(environment);
                serialized.FindProperty("backgroundShader").objectReferenceValue = EnvironmentPreviewWindow.BackgroundShader();
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Show(candidate);
        }

        public void Show(FlatBackgroundAsset candidate)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EnvironmentPreviewSession));
            if (!environment || scene != SceneManager.GetActiveScene())
                throw new InvalidOperationException("End preview before changing the active locale.");
            if (candidate) environment.Preview(candidate);
            else environment.EndPreview();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (!environment) return;
            environment.EndPreview();
            if (temporary) UnityEngine.Object.Destroy(environment.gameObject);
        }
    }
}
