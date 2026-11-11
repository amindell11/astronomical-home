#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UI.Projection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Rendering.Projection
{
    [Category("UI")]
    [Category("RequiresGraphics")]
    public sealed class CanopyImagePlayModeTests
    {
        private const string FeaturePath = "Assets/Settings/Rendering/CanopyProjectionFeature.asset";
        private readonly List<Object> owned = new();
        private Camera host;
        private GameObject imageObject;

        [TearDown]
        public void TearDown()
        {
            if (host) host.targetTexture = null;
            if (imageObject) Object.DestroyImmediate(imageObject);
            for (var i = owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [UnityTest]
        public IEnumerator ImageOutput_FollowsItsInput_ThroughTheFeature()
        {
            var hostObject = Own(new GameObject("Canopy image host"));
            host = hostObject.AddComponent<Camera>();
            hostObject.AddComponent<UniversalAdditionalCameraData>();
            host.cullingMask = 0;
            var hostTarget = Own(new RenderTexture(16, 16, 0, RenderTextureFormat.ARGB32));
            hostTarget.Create();
            host.targetTexture = hostTarget;

            imageObject = new GameObject("Canopy image");
            imageObject.SetActive(false);
            var image = imageObject.AddComponent<CanopyImage>();
            var feature = AssetDatabase.LoadAssetAtPath<CanopyProjectionFeature>(FeaturePath);
            using (var wiring = new SerializedObject(image))
            {
                wiring.FindProperty("feature").objectReferenceValue = feature;
                wiring.ApplyModifiedPropertiesWithoutUndo();
            }
            imageObject.SetActive(true);

            var settings = default(CanopySettings);
            var input = Filled(32, 16);
            image.Initialize(host, () => input, () => settings);
            yield return null;
            yield return null;
            Assert.That(image.Output.width, Is.EqualTo(input.width));
            Assert.That(image.Output.height, Is.EqualTo(input.height));
            AssertMatches(input, image.Output);

            var output = image.Output;
            input = Filled(64, 32);
            yield return null;
            yield return null;
            Assert.That(image.Output, Is.SameAs(output));
            Assert.That(image.Output.width, Is.EqualTo(64));
            Assert.That(image.Output.height, Is.EqualTo(32));
            AssertMatches(input, image.Output);

            settings = CanopySettings.Combat;
            Clear(input);
            yield return new WaitForSecondsRealtime(3f);
            var faded = Read(image.Output);
            for (var i = 0; i < faded.Length; i++)
            {
                Assert.That(faded[i].r, Is.LessThan(.0005f));
                Assert.That(faded[i].a, Is.LessThan(.0005f));
            }

            image.enabled = false;
            image.enabled = true;
            yield return null;
            yield return null;
            AssertMatches(input, image.Output);
        }

        private RenderTexture Filled(int width, int height)
        {
            var source = Own(new Texture2D(width, height, TextureFormat.RGBA32, false));
            var pixels = new Color[width * height];
            var color = new Color(.6f, .3f, .2f, 1f);
            for (var y = height / 2; y < height; y++)
            for (var x = 0; x < width; x++)
                pixels[y * width + x] = color;
            source.SetPixels(pixels);
            source.Apply();
            var target = Own(new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB));
            target.Create();
            Graphics.Blit(source, target);
            return target;
        }

        private void Clear(RenderTexture target)
        {
            var blank = Own(new Texture2D(1, 1, TextureFormat.RGBA32, false));
            blank.SetPixel(0, 0, Color.clear);
            blank.Apply();
            Graphics.Blit(blank, target);
        }

        private void AssertMatches(RenderTexture input, RenderTexture output)
        {
            var actual = Read(output);
            var expected = Read(input);
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i].r, Is.EqualTo(expected[i].r).Within(.01f));
                Assert.That(actual[i].g, Is.EqualTo(expected[i].g).Within(.01f));
                Assert.That(actual[i].b, Is.EqualTo(expected[i].b).Within(.01f));
                Assert.That(actual[i].a, Is.EqualTo(expected[i].a).Within(.01f));
            }
        }

        private Color[] Read(RenderTexture texture)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            try
            {
                var image = Own(new Texture2D(texture.width, texture.height, TextureFormat.RGBAFloat, false, true));
                image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                image.Apply();
                return image.GetPixels();
            }
            finally { RenderTexture.active = previous; }
        }

        private T Own<T>(T value) where T : Object
        {
            owned.Add(value);
            return value;
        }
    }
}
#endif
