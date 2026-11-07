#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Game.Travel;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Rendering.Travel
{
    [Category("Sectors")]
    [Category("RequiresGraphics")]
    public sealed class SectorJumpPassPlayModeTests
    {
        private const int Width = 32;
        private const int Height = 16;
        private const int CubePixel = Height / 2 * Width + Width / 4;
        private const int SkyPixel = Height / 2 * Width + Width * 3 / 4;
        private static readonly Color JumpColor = new(.25f, .5f, .75f, 1f);
        private static readonly Color CubeColor = new(0f, 1f, 0f, 1f);
        private static readonly Color SkyColor = new(1f, 0f, 0f, 1f);
        private readonly List<Object> owned = new();
        private SectorJumpFeature feature;
        private Camera camera;

        [TearDown]
        public void TearDown()
        {
            if (feature && camera) feature.Unbind(camera);
            if (camera) camera.targetTexture = null;
            for (var i = owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [UnityTest]
        public IEnumerator BoundPass_CoversOpaqueAndSkyQueueGeometryWithTheJumpImage()
        {
            feature = AssetDatabase.LoadAssetAtPath<SectorJumpFeature>("Assets/Settings/Rendering/SectorJumpFeature.asset");
            var target = Own(new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear));
            target.Create();
            var origin = new Vector3(0f, 0f, 5000f);
            camera = Own(new GameObject("Jump pass camera")).AddComponent<Camera>();
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 20f;
            camera.transform.position = origin + Vector3.back * 10f;
            Block(PrimitiveType.Cube, origin + Vector3.left * 4f, Unlit(CubeColor, (int)RenderQueue.Geometry));
            Block(PrimitiveType.Quad, origin + Vector3.right * 4f, Unlit(SkyColor, (int)RenderQueue.Transparent - 40));
            yield return null;
            yield return null;
            var unbound = Read(target);
            AssertColor(CubeColor, unbound[CubePixel], "cube without the pass");
            AssertColor(SkyColor, unbound[SkyPixel], "sky-queue quad without the pass");

            feature.Bind(camera, new SectorJumpPass(JumpMaterial()));
            yield return null;
            yield return null;
            var bound = Read(target);
            for (var i = 0; i < bound.Length; i++) AssertColor(JumpColor, bound[i], $"pixel {i}");
        }

        private Material JumpMaterial()
        {
            var material = Own(new Material(feature.JumpShader));
            var sky = Own(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true));
            sky.SetPixel(0, 0, JumpColor);
            sky.Apply();
            material.SetTexture("_FromSky", sky);
            material.SetTexture("_ToSky", sky);
            material.SetVector("_Motion", new Vector4(0f, 0f, 0f, (float)Width / Height));
            material.SetVector("_Direction", new Vector4(0f, 1f, 1f, 0f));
            material.SetVector("_Ship", new Vector4(1f, 0f, 0f, 1f));
            material.SetVector("_Exhaust", new Vector4(0f, 0f, 1f, 0f));
            material.SetFloat("_Opacity", 1f);
            return material;
        }

        private Material Unlit(Color color, int queue)
        {
            var material = Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")) { renderQueue = queue });
            material.SetColor("_BaseColor", color);
            return material;
        }

        private void Block(PrimitiveType type, Vector3 position, Material material)
        {
            var block = Own(GameObject.CreatePrimitive(type));
            block.transform.position = position;
            block.transform.localScale = Vector3.one * 4f;
            block.GetComponent<Renderer>().sharedMaterial = material;
        }

        private T Own<T>(T value) where T : Object
        {
            owned.Add(value);
            return value;
        }

        private Color[] Read(RenderTexture target)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            try
            {
                var image = Own(new Texture2D(Width, Height, TextureFormat.RGBAFloat, false, true));
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                return image.GetPixels();
            }
            finally { RenderTexture.active = previous; }
        }

        private static void AssertColor(Color expected, Color actual, string at)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.01f), at);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.01f), at);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.01f), at);
        }
    }
}
#endif
