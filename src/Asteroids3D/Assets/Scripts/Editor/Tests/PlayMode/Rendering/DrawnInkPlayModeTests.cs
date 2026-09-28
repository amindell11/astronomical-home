#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Tests.PlayMode.Scenarios.Drawn;
using UnityEngine;

namespace Tests.PlayMode.Rendering
{
    [Category("Camera"), Category("RequiresGraphics")]
    public sealed class DrawnInkPlayModeTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void Ink_DefinesAnOverlapInsideTheSilhouette(bool orthographic)
        {
            const int size = 256;
            var priorTarget = RenderTexture.active;
            var root = new GameObject("Ink overlap test");
            var resources = new List<Object>();
            using var ink = new DrawnInkStudy(0);
            try
            {
                var surface = new Material(Shader.Find("Astronomical/Comparison/Drawn Surface"));
                resources.Add(surface);
                surface.SetTexture("_EmissionMap", Texture2D.whiteTexture);
                surface.SetColor("_EmissionColor", Color.white);
                surface.SetFloat("_EmissionStrength", 1);
                for (var i = 0; i < 2; i++)
                {
                    var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    panel.transform.SetParent(root.transform, false);
                    panel.layer = 31;
                    panel.transform.localScale = i == 0 ? new Vector3(2, 2, .2f) : new Vector3(1, 1, .2f);
                    panel.transform.localPosition = i == 0 ? Vector3.zero : new Vector3(.5f, 0, -.4f);
                    panel.GetComponent<MeshRenderer>().sharedMaterial = surface;
                }
                var camera = new GameObject("Ink test camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.position = new Vector3(0, 0, -6);
                camera.orthographic = orthographic;
                camera.orthographicSize = 1.5f;
                camera.fieldOfView = 30;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.gray;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                var target = new RenderTexture(size, size, 24);
                resources.Add(target);
                camera.targetTexture = target;
                var image = new Texture2D(size, size, TextureFormat.RGB24, false);
                resources.Add(image);
                Color32[] Read()
                {
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    image.Apply();
                    return image.GetPixels32();
                }
                var plain = Read();
                ink.Strength = 1;
                var outlined = Read();
                var interior = 0;
                var marked = 0;
                for (var y = 4; y < size - 4; y++)
                for (var x = 4; x < size - 4; x++)
                {
                    var white = true;
                    for (var dy = -4; dy <= 4 && white; dy++)
                    for (var dx = -4; dx <= 4; dx++)
                    {
                        var pixel = plain[(y + dy) * size + x + dx];
                        if (pixel.r >= 240 && pixel.g >= 240 && pixel.b >= 240) continue;
                        white = false;
                        break;
                    }
                    if (!white) continue;
                    interior++;
                    var result = outlined[y * size + x];
                    if (result.r < 80 && result.g < 80 && result.b < 80) marked++;
                }
                Assert.That(interior, Is.GreaterThan(10000), "The overlapping surfaces must fill the white silhouette.");
                Assert.That(marked, Is.GreaterThan(200), "Ink must define overlaps inside the outer border.");
                Assert.That(marked, Is.LessThan(interior / 4), "Ink must leave broad surfaces clear.");
            }
            finally
            {
                RenderTexture.active = priorTarget;
                Object.DestroyImmediate(root);
                foreach (var resource in resources) Object.DestroyImmediate(resource);
            }
        }
    }
}
#endif
