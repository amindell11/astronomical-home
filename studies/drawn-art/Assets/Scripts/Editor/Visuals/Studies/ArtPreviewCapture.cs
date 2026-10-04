using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Visuals.Studies
{
    public static class ArtPreviewCapture
    {
        public static void BuildAndCapture()
        {
            ArtPreviewAuthoring.Build();
            EditorApplication.delayCall += CaptureBatch;
        }

        private static void CaptureBatch()
        {
            try
            {
                Capture();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Astronomical/Art previews/Capture saved study scenes")]
        public static void Capture()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var quality = QualitySettings.GetQualityLevel();
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/art-previews"));
            Directory.CreateDirectory(output);
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "High Fidelity"), true);
            try
            {
                foreach (var name in new[] { "HangarHero", "SpaceHero", "AsteroidField" })
                {
                    var scene = EditorSceneManager.OpenScene(ArtPreviewAuthoring.Folder + "Scenes/" + name + ".unity");
                    Camera camera = null;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var component in root.GetComponentsInChildren<Component>(true))
                            if (!component) throw new InvalidOperationException(name + " contains a missing script.");
                        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                            if (!filter.sharedMesh || !EditorUtility.IsPersistent(filter.sharedMesh))
                                throw new InvalidOperationException(name + ": unsaved mesh on " + filter.name);
                        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                            foreach (var material in renderer.sharedMaterials)
                            {
                                if (!material || !EditorUtility.IsPersistent(material) || !material.shader.isSupported)
                                    throw new InvalidOperationException(name + ": invalid material on " + renderer.name);
                                if (renderer.name.Contains("Nacelle Core") && !material.GetTexture("_EmissionMap"))
                                    throw new InvalidOperationException(name + ": engine emission mask was not saved.");
                            }
                        if (root.TryGetComponent<Camera>(out var found)) camera = found;
                    }
                    if (!camera) throw new InvalidOperationException(name + ": missing camera.");
                    Render(camera, Path.Combine(output, name + ".png"));
                }
                File.WriteAllText(Path.Combine(output, "verification.txt"),
                    "Reopened and rendered HangarHero, SpaceHero, AsteroidField at 1600x900.\n" +
                    "All meshes and materials persistent; no missing scripts or unsupported material shaders.\n");
                Debug.Log("ART_PREVIEW_CAPTURE=" + output);
            }
            finally
            {
                QualitySettings.SetQualityLevel(quality, true);
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void Render(Camera camera, string path)
        {
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGBHalf) { antiAliasing = 4 };
            var encoded = new RenderTexture(1600, 900, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            var srgb = GL.sRGBWrite;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                GL.sRGBWrite = true;
                Graphics.Blit(target, encoded);
                RenderTexture.active = encoded;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = active;
                GL.sRGBWrite = srgb;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(encoded);
                Object.DestroyImmediate(image);
            }
        }
    }
}
