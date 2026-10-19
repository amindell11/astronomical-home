#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tests.EditMode.Rendering.Illustrated
{
    [Category("Ships"), Category("RequiresGraphics")]
    public sealed class DrawnSurfaceEditModeTests
    {
        [Test]
        public void Nightshade_PaintTexturesAreImported()
        {
            var ship = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/Nightshade.prefab");
            foreach (var renderer in ship.GetComponentsInChildren<MeshRenderer>(true))
            foreach (var material in renderer.sharedMaterials)
            {
                if (!material.IsKeywordEnabled("_PAINT_LAYERS")) continue;
                foreach (var slot in new[] {"_PaintShadowMap", "_PaintLightMap", "_PaintInkMap", "_EmissionMap"})
                    Assert.That(material.GetTexture(slot), Is.Not.Null, material.name + " " + slot);
            }
        }

        [TestCase(false), TestCase(true)]
        public void PaintedHull_FlashRestoresItsColorsInHighFidelity(bool lightDriven)
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
                if (lightDriven) material.EnableKeyword("_LIGHT_DRIVEN_PAINT");
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

        [Test]
        public void LightDrivenHull_RespondsToLightWhileInkAndCameraStayIndependent()
        {
            var priorPipeline = QualitySettings.renderPipeline;
            var priorTarget = RenderTexture.active;
            var priorSun = RenderSettings.sun;
            var priorAsync = ShaderUtil.allowAsyncCompilation;
            var root = new GameObject("Hull lighting test");
            var material = new Material(Shader.Find("Astronomical/Drawn/Surface"));
            var target = new RenderTexture(64, 64, 24);
            var image = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var shadow = new Texture2D(64, 64, TextureFormat.RGBA32, true, true);
            var ink = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            try
            {
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                    "Assets/Settings/Rendering/URP-HighFidelity.asset");
                ShaderUtil.allowAsyncCompilation = false;
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    shadow.SetPixel(x, y, Color.white * (x / 63f * .6f));
                    ink.SetPixel(x, y, x < 13 ? Color.white : Color.black);
                }
                shadow.Apply();
                ink.Apply();
                var subject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                subject.transform.SetParent(root.transform, false);
                subject.layer = 31;
                subject.GetComponent<Renderer>().sharedMaterial = material;
                material.EnableKeyword("_PAINT_LAYERS");
                material.EnableKeyword("_LIGHT_DRIVEN_PAINT");
                material.SetTexture("_PaintShadowMap", shadow);
                material.SetTexture("_PaintInkMap", ink);
                material.SetColor("_PaperColor", new Color(.2f, .4f, .4f));
                material.SetColor("_ShadowColor", new Color(.1f, .1f, .1f));
                material.SetFloat("_PaintLightStrength", 0);
                material.SetFloat("_ShadowThreshold", .35f);
                material.SetFloat("_ShadowSoftness", .1f);
                material.SetFloat("_PaletteLighting", 1);
                material.SetFloat("_AmbientStrength", 0);
                material.SetFloat("_SpecularStrength", 0);
                material.SetFloat("_EmissionStrength", 0);
                material.SetFloat("_LineStrength", 0);
                material.SetFloat("_WearStrength", 0);
                var camera = new GameObject("Hull camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.position = new Vector3(0, 0, -2);
                camera.orthographic = true;
                camera.orthographicSize = .6f;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.targetTexture = target;
                var light = new GameObject("Hull key", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Directional;
                light.cullingMask = 1 << 31;
                light.intensity = 1;
                RenderSettings.sun = light;

                var front = ReadCenter(camera, target, image);
                var inkFront = image.GetPixel(12, 32);
                light.transform.rotation = Quaternion.Euler(0, 60, 0);
                var grazing = ReadCenter(camera, target, image);
                Assert.That(front.g - grazing.g, Is.GreaterThan(.15f), "Lighting must move the painted boundary.");
                Assert.That(image.GetPixel(12, 32).maxColorComponent, Is.LessThan(.15f), "Ink stays dark under changed lighting.");
                Assert.That(inkFront.maxColorComponent, Is.LessThan(.15f));
                camera.transform.position = Quaternion.Euler(0, 15, 0) * new Vector3(0, 0, -2);
                camera.transform.LookAt(Vector3.zero);
                var orbit = ReadCenter(camera, target, image);
                Assert.That(orbit.g, Is.EqualTo(grazing.g).Within(2f / 255), "Camera motion must not relight the hull.");
            }
            finally
            {
                QualitySettings.renderPipeline = priorPipeline;
                RenderSettings.sun = priorSun;
                RenderTexture.active = priorTarget;
                ShaderUtil.allowAsyncCompilation = priorAsync;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(shadow);
                Object.DestroyImmediate(ink);
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
