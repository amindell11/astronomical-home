using Cameras.Starfield;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tests.EditMode.Rendering
{
    [TestFixture]
    [Category("Sectors")]
    [Category("RequiresGraphics")]
    public class StarfieldParallaxRenderTests
    {
        [TestCase(0f, 0f, 0f)]
        [TestCase(0.5f, 0f, 1f)]
        [TestCase(1f, 0f, 1f)]
        [TestCase(2f, 0f, 1f)]
        [TestCase(0f, 0.6f, 1f)]
        [TestCase(0.5f, 0.6f, 0f)]
        [TestCase(1f, 0.6f, 0f)]
        [TestCase(2f, 0.6f, 0f)]
        public void CameraTravel_UsesOverallScaleIndependentlyOfZoomResponse(
            float overallScale, float parallax, float nearShare)
        {
            var material = new Material(Shader.Find("Custom/StarField"));
            material.SetFloat("_Seed", 2);
            material.SetFloat("_StarDensity", 1);
            material.SetFloat("_NearLayerShare", nearShare);
            material.SetFloat("_CellScale", 1);
            material.SetFloat("_StarSizeMin", 0.025f);
            material.SetFloat("_StarSizeMax", 0.025f);
            material.SetFloat("_PositionJitter", 0);
            material.SetFloat("_ParallaxNear", parallax);
            material.SetFloat("_ParallaxFar", parallax);
            material.SetFloat("_ParallaxScale", overallScale);
            material.SetFloat("_SizeZoomResponse", 0);
            material.SetFloat("_HaloStrength", 0);
            material.SetFloat("_TwinkleAmount", 0);
            material.SetColor("_ColorCool", Color.white);
            material.SetColor("_ColorWarm", Color.white);
            var cameraObject = new GameObject("Parallax test camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 28;
            var starObject = new GameObject("Parallax test stars", typeof(SpriteRenderer));
            var renderer = starObject.GetComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            var tracker = starObject.AddComponent<StarfieldParallax>();
            var serialized = new SerializedObject(tracker);
            serialized.FindProperty("viewCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-100, -100, 0), new Vector3(100, -100, 0),
                    new Vector3(100, 100, 0), new Vector3(-100, 100, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            var target = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(1024, 1024, TextureFormat.RGBAFloat, false, true);
            var commands = new CommandBuffer();
            var properties = new MaterialPropertyBlock();
            var previousTarget = RenderTexture.active;
            var previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
            var start = new Vector3(0.5f / (1 + parallax), 0.5f / (1 + parallax), -10);
            try
            {
                target.Create();
                foreach (var response in new[] { 0f, 0.5f, 1f })
                {
                    material.SetFloat("_SpacingZoomResponse", response);
                    material.SetFloat("_ParallaxScale", overallScale);
                    camera.transform.position = start;
                    typeof(StarfieldParallax).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(tracker, null);
                    var before = Center();
                    camera.transform.position += Vector3.right * 0.06f;
                    var after = Center();
                    var expected = -0.06f * (1 + parallax) * 1024 / 56 * overallScale;
                    Assert.That(after - before, Is.EqualTo(expected).Within(0.15f),
                        $"Spacing response {response} must not change camera-induced movement.");
                    material.SetFloat("_ParallaxScale", overallScale == 0 ? 2 : 0);
                    Assert.That(Center(), Is.EqualTo(after).Within(0.01f),
                        "Changing overall scale must not reposition stars while the camera is stationary.");
                    if (response == 0)
                    {
                        camera.orthographicSize = 14;
                        Assert.That(Center(), Is.EqualTo(after).Within(0.01f),
                            "Zoom after travel must keep the pattern stable when both zoom responses are zero.");
                        camera.orthographicSize = 28;
                    }
                }
            }
            finally
            {
                RenderTexture.active = previousTarget;
                Shader.SetGlobalVector("_WorldSpaceCameraPos", previousCamera);
                commands.Release();
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(starObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(material);
            }

            float Center()
            {
                typeof(StarfieldParallax).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(tracker, null);
                renderer.GetPropertyBlock(properties);
                commands.Clear();
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, Color.black);
                commands.SetViewProjectionMatrices(camera.worldToCameraMatrix,
                    GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-camera.orthographicSize, camera.orthographicSize, -camera.orthographicSize, camera.orthographicSize, 0.1f, 100), true));
                commands.SetGlobalVector("_WorldSpaceCameraPos", camera.transform.position);
                commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, -1, properties);
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
                pixels.Apply();
                var colors = pixels.GetPixels();
                var light = 0f;
                var moment = 0f;
                for (var y = 502; y < 522; y++)
                for (var x = 502; x < 522; x++)
                {
                    var intensity = colors[y * 1024 + x].r;
                    light += intensity;
                    moment += x * intensity;
                }
                Assert.Greater(light, 0.01f, "The tracked star must remain visible.");
                return moment / light;
            }
        }
    }
}
