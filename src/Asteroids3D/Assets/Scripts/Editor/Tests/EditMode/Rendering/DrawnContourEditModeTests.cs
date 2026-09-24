#if UNITY_EDITOR
using System.Collections.Generic;
using Asteroids.Spawning;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Rendering
{
    [Category("Camera"), Category("RequiresGraphics")]
    public sealed class DrawnContourEditModeTests
    {
        private const int Size = 768;

        [TestCase(0.3403712f, 0.5011027f, 0.1985515f, -0.7704679f)]
        [TestCase(0.05013389f, 0.07380714f, 0.02924529f, -0.9955822f)]
        [TestCase(-0.2176847f, -0.3204816f, -0.1269832f, -0.9131157f)]
        public void AsteroidContour_LeavesSurfaceInteriorClear(float x, float y, float z, float w)
        {
            var priorTarget = RenderTexture.active;
            var root = new GameObject("Contour test");
            var resources = new List<Object>();
            try
            {
                var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(
                    "Assets/Settings/Asteroids/SpawnSettings.asset");
                var subject = new GameObject("Surface", typeof(MeshFilter), typeof(MeshRenderer));
                subject.transform.SetParent(root.transform, false);
                subject.transform.rotation = new Quaternion(x, y, z, w);
                subject.layer = 31;
                subject.GetComponent<MeshFilter>().sharedMesh = settings.meshInfos[0].mesh;
                var surface = new Material(Shader.Find("Astronomical/Comparison/Drawn Surface"));
                resources.Add(surface);
                surface.SetColor("_BaseColor", Color.white);
                surface.SetTexture("_EmissionMap", Texture2D.whiteTexture);
                surface.SetColor("_EmissionColor", Color.white);
                surface.SetFloat("_EmissionStrength", 1);
                subject.GetComponent<MeshRenderer>().sharedMaterial = surface;

                var outline = new GameObject("Contour", typeof(MeshFilter), typeof(MeshRenderer));
                outline.transform.SetParent(subject.transform, false);
                outline.layer = 31;
                outline.GetComponent<MeshFilter>().sharedMesh = settings.meshInfos[0].mesh;
                var ink = new Material(Shader.Find("Astronomical/Comparison/Drawn Contour"));
                resources.Add(ink);
                ink.SetColor("_ContourColor", Color.black);
                ink.SetFloat("_ContourMinimum", 1);
                ink.SetFloat("_ContourPixels", 3.2f);
                outline.GetComponent<MeshRenderer>().sharedMaterial = ink;

                var camera = new GameObject("Contour camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.position = new Vector3(0, 0, 6);
                camera.transform.LookAt(Vector3.zero);
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
                camera.allowHDR = false;
                camera.allowMSAA = false;
                var target = new RenderTexture(Size, Size, 24);
                resources.Add(target);
                camera.targetTexture = target;
                var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                resources.Add(image);

                outline.SetActive(false);
                var surfacePixels = ReadPixels(camera, target, image);
                outline.SetActive(true);
                var contourPixels = ReadPixels(camera, target, image);
                var whiteSums = new int[(Size + 1) * (Size + 1)];
                for (var row = 0; row < Size; row++)
                {
                    var rowSum = 0;
                    for (var column = 0; column < Size; column++)
                    {
                        var pixel = surfacePixels[row * Size + column];
                        if (pixel.r > 240 && pixel.g > 240 && pixel.b > 240)
                            rowSum++;
                        whiteSums[(row + 1) * (Size + 1) + column + 1] =
                            whiteSums[row * (Size + 1) + column + 1] + rowSum;
                    }
                }

                var totalInk = 0;
                var interiorInk = 0;
                for (var row = 0; row < Size; row++)
                {
                    for (var column = 0; column < Size; column++)
                    {
                        var pixel = contourPixels[row * Size + column];
                        if (pixel.r >= 35 || pixel.g >= 35 || pixel.b >= 35)
                            continue;
                        totalInk++;
                        if (row < 8 || row >= Size - 8 || column < 8 || column >= Size - 8)
                            continue;
                        var top = (row - 8) * (Size + 1);
                        var bottom = (row + 9) * (Size + 1);
                        var whiteCount = whiteSums[bottom + column + 9] - whiteSums[bottom + column - 8]
                            - whiteSums[top + column + 9] + whiteSums[top + column - 8];
                        if (whiteCount == 17 * 17)
                            interiorInk++;
                    }
                }

                Assert.That(whiteSums[whiteSums.Length - 1], Is.GreaterThan(10000), "Surface must render white.");
                Assert.That(totalInk, Is.GreaterThan(200), "Contour must remain visible.");
                Assert.That(interiorInk, Is.Zero, "Contour must not paint fragmented marks inside the surface.");
            }
            finally
            {
                RenderTexture.active = priorTarget;
                Object.DestroyImmediate(root);
                foreach (var resource in resources)
                    Object.DestroyImmediate(resource);
            }
        }

        private static Color32[] ReadPixels(Camera camera, RenderTexture target, Texture2D image)
        {
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            image.Apply();
            return image.GetPixels32();
        }
    }
}
#endif
