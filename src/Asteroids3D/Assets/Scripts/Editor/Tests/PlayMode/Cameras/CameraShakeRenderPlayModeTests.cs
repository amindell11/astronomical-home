using System.Collections.Generic;
using Cameras;
using NUnit.Framework;
using UnityEngine;

namespace Tests.PlayMode.Cameras
{
    [Category("Camera"), Category("RequiresGraphics")]
    public sealed class CameraShakeRenderPlayModeTests
    {
        private const int Size = 128;

        [Test]
        public void ShakenView_Offset_MovesTheRenderedWorld()
        {
            RenderScene(Vector3.zero, (camera, target, image) =>
            {
                var before = Centroid(ReadPixels(camera, target, image));
                camera.worldToCameraMatrix = CameraUtils.ShakenView(
                    camera.worldToCameraMatrix, new Vector2(1f, 0f), 0f);
                try
                {
                    var after = Centroid(ReadPixels(camera, target, image));
                    Assert.That(Mathf.Abs(after.x - before.x), Is.EqualTo(Size / (2f * 5f)).Within(1.5f));
                    Assert.That(after.y, Is.EqualTo(before.y).Within(1.5f));
                }
                finally
                {
                    camera.ResetWorldToCameraMatrix();
                }
            });
        }

        [Test]
        public void ShakenView_Roll_TurnsTheRenderedWorld()
        {
            RenderScene(new Vector3(3f, 0f, 0f), (camera, target, image) =>
            {
                camera.worldToCameraMatrix = CameraUtils.ShakenView(
                    camera.worldToCameraMatrix, Vector2.zero, 90f);
                var centroid = Centroid(ReadPixels(camera, target, image));
                Assert.That(Mathf.Abs(centroid.x - Size / 2f), Is.LessThan(2f));
                Assert.That(
                    Mathf.Abs(Mathf.Abs(centroid.y - Size / 2f) - 3f * Size / (2f * 5f)),
                    Is.LessThan(2f));
            });
        }

        private static void RenderScene(Vector3 spherePosition, System.Action<Camera, RenderTexture, Texture2D> body)
        {
            var priorTarget = RenderTexture.active;
            var root = new GameObject("Shake render");
            var resources = new List<Object>();
            try
            {
                var target = new RenderTexture(Size, Size, 24);
                resources.Add(target);
                var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                resources.Add(image);
                var camera = new GameObject("Shake camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.transform.rotation = Quaternion.identity;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                camera.aspect = 1f;

                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(root.transform, false);
                sphere.transform.position = spherePosition;
                sphere.transform.localScale = Vector3.one * 0.6f;
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                resources.Add(material);
                material.SetColor("_BaseColor", Color.white);
                sphere.GetComponent<MeshRenderer>().sharedMaterial = material;

                body(camera, target, image);
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

        private static Vector2 Centroid(Color32[] pixels)
        {
            var count = 0;
            var sum = Vector2.zero;
            for (var index = 0; index < pixels.Length; index++)
            {
                if (pixels[index].r <= 128)
                    continue;
                count++;
                sum += new Vector2(index % Size, index / Size);
            }

            Assert.Greater(count, 10);
            return sum / count;
        }
    }
}
