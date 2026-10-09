#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tests.EditMode.Rendering
{
    [Category("Ships"), Category("RequiresGraphics")]
    public sealed class DrawnSurfaceEditModeTests
    {
        [Test]
        public void PaintedHull_FlashRestoresItsColorsInHighFidelity()
        {
            var priorPipeline = QualitySettings.renderPipeline;
            var priorTarget = RenderTexture.active;
            var priorAsync = ShaderUtil.allowAsyncCompilation;
            var root = new GameObject("Paint flash test");
            var material = new Material(Shader.Find("Astronomical/Drawn/Surface"));
            var target = new RenderTexture(64, 64, 24);
            var image = new Texture2D(64, 64, TextureFormat.RGB24, false);
            try
            {
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                    "Assets/Settings/Rendering/URP-HighFidelity.asset");
                ShaderUtil.allowAsyncCompilation = false;
                var subject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                subject.transform.SetParent(root.transform, false);
                subject.layer = 31;
                var renderer = subject.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                material.EnableKeyword("_PAINT_LAYERS");
                material.SetColor("_PaperColor", new Color(.1f, .2f, .5f));
                material.SetFloat("_PaletteLighting", 1);
                material.SetFloat("_SpecularStrength", 0);
                material.SetFloat("_EmissionStrength", 0);
                material.SetFloat("_LineStrength", 0);
                material.SetFloat("_WearStrength", 0);
                var camera = new GameObject("Flash camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.position = new Vector3(0, 0, -2);
                camera.orthographic = true;
                camera.orthographicSize = .6f;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.targetTexture = target;
                var light = new GameObject("Flash key", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Directional;
                light.cullingMask = 1 << 31;
                light.intensity = 1;

                var baseline = ReadCenter(camera, target, image);
                Assert.That(baseline.b, Is.GreaterThan(.2f), "The painted hull must be lit before flashing.");
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", Color.white);
                block.SetFloat("_DamageFlash", 1);
                renderer.SetPropertyBlock(block);
                var flash = ReadCenter(camera, target, image);
                Assert.That(flash.r, Is.GreaterThan(baseline.r + .2f), "A hit must brighten the painted hull.");
                block.SetFloat("_DamageFlash", 0);
                renderer.SetPropertyBlock(block);
                var restored = ReadCenter(camera, target, image);
                Assert.That(restored.r, Is.EqualTo(baseline.r).Within(1f / 255));
                Assert.That(restored.g, Is.EqualTo(baseline.g).Within(1f / 255));
                Assert.That(restored.b, Is.EqualTo(baseline.b).Within(1f / 255));
            }
            finally
            {
                QualitySettings.renderPipeline = priorPipeline;
                RenderTexture.active = priorTarget;
                ShaderUtil.allowAsyncCompilation = priorAsync;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }

        private static Color ReadCenter(Camera camera, RenderTexture target, Texture2D image)
        {
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            image.Apply();
            return image.GetPixel(32, 32);
        }
    }
}
#endif
