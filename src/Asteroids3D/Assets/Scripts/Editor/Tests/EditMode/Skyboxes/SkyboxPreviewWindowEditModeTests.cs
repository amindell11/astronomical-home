using System.Collections;
using NUnit.Framework;
using Substrate.Services.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.EditMode.Skyboxes
{
    [Category("Sectors")]
    public class SkyboxPreviewWindowEditModeTests
    {
        [UnityTest]
        public IEnumerator PlayModeWindow_RestoresSkyOnEndSceneChangeAndClose()
        {
            yield return new EnterPlayMode();
            var active = SceneManager.GetActiveScene();
            var original = RenderSettings.skybox;
            var material = new Material(Shader.Find("Skybox/Panoramic"));
            var otherMaterial = new Material(Shader.Find("Skybox/Panoramic"));
            var window = ScriptableObject.CreateInstance<SkyboxPreviewWindow>();
            var other = SceneManager.CreateScene("Skybox Preview Other");
            try
            {
                window.BeginPreview(material);
                Assert.That(RenderSettings.skybox, Is.EqualTo(material));
                window.EndPreview();
                Assert.That(RenderSettings.skybox, Is.EqualTo(original));
                SceneManager.SetActiveScene(other);
                RenderSettings.skybox = otherMaterial;
                SceneManager.SetActiveScene(active);
                window.BeginPreview(material);
                SceneManager.SetActiveScene(other);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(other));
                Assert.That(RenderSettings.skybox, Is.EqualTo(otherMaterial));
                SceneManager.SetActiveScene(active);
                Assert.That(RenderSettings.skybox, Is.EqualTo(original));
                window.BeginPreview(material);
                Object.DestroyImmediate(window);
                Assert.That(RenderSettings.skybox, Is.EqualTo(original));
            }
            finally
            {
                if (window)
                    Object.DestroyImmediate(window);
                SceneManager.SetActiveScene(active);
                RenderSettings.skybox = original;
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(otherMaterial);
            }
            yield return SceneManager.UnloadSceneAsync(other);
            yield return new ExitPlayMode();
            Assert.That(EditorApplication.isPlaying, Is.False);
        }
    }
}
