#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Capture;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Rendering.Vanguard
{
    [Category("Camera"), Category("RequiresGraphics")]
    public sealed class VanguardStudyPlayModeTests : PlayModeWorldFixture
    {
        private const string Assets = "Assets/Visuals/Ships/Vanguard/DrawnStudy/";

        [UnityTest, Timeout(180000)]
        public IEnumerator TexturedVanguard_CompareNativeLighting()
        {
            var output = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../results/vanguard-study/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(output);
            TestContext.Progress.WriteLine("VANGUARD_STUDY=" + output);
            var root = new GameObject("Vanguard comparison");
            var owned = new List<Object>();
            var quality = QualitySettings.GetQualityLevel();
            var previousSun = RenderSettings.sun;
            var previousAmbient = RenderSettings.ambientLight;
            var previousMode = RenderSettings.ambientMode;
            var previousTarget = RenderTexture.active;
            try
            {
                QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "High Fidelity"), true);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.22f, .25f, .32f);
                var light = new GameObject("Fixed key", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Directional;
                light.intensity = 1;
                light.shadows = LightShadows.Soft;
                light.shadowBias = .02f;
                light.shadowNormalBias = .05f;
                light.cullingMask = 1 << 30;
                light.transform.rotation = Quaternion.Euler(25, -35, 0);
                RenderSettings.sun = light;
                var camera = new GameObject("Canonical ship camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.enabled = false;
                camera.orthographic = true;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.nearClipPlane = 50;
                camera.farClipPlane = 70;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.045f, .062f, .105f);
                camera.cullingMask = 1 << 30;
                var target = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
                var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                owned.Add(target); owned.Add(image);
                camera.targetTexture = target;
                var config = new CaptureConfig { width = 1600, height = 900, minHalfHeight = 3.8f, padding = 0 };
                CaptureFraming.Apply(camera, config, new[] { Vector2.zero });
                Color32[] Read(string name)
                {
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                    return image.GetPixels32();
                }

                var production = new GameObject("Current production Vanguard");
                production.transform.SetParent(root.transform, false);
                var rig = Load<GameObject>("Assets/Prefabs/Ships/Ship_1_Vanguard_VisualRig.prefab");
                foreach (var source in rig.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (source.name != "Model") continue;
                    var model = new GameObject("Model", typeof(MeshFilter), typeof(MeshRenderer));
                    model.transform.SetParent(production.transform, false);
                    model.layer = 30;
                    model.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                    model.transform.localScale = source.transform.lossyScale;
                    model.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                    var authored = source.GetComponent<MeshRenderer>();
                    var copy = model.GetComponent<MeshRenderer>();
                    copy.sharedMaterials = authored.sharedMaterials;
                    copy.shadowCastingMode = authored.shadowCastingMode;
                    copy.receiveShadows = authored.receiveShadows;
                }
                var current = production.GetComponentInChildren<MeshRenderer>();
                Assert.That(current, Is.Not.Null, "The current Vanguard rig must expose its Model.");
                var productionRotation = Quaternion.Euler(0, 0, 180) * Quaternion.Euler(-90, 0, 0);
                production.transform.rotation = productionRotation;
                production.transform.localScale *= 6 / current.bounds.size.y;
                production.transform.position -= current.bounds.center;
                Read("production-top");
                production.transform.rotation = Quaternion.Euler(28, -24, -30) * productionRotation;
                Read("production-quarter");
                production.transform.rotation = productionRotation;
                camera.orthographicSize = 26;
                Read("production-gameplay");
                production.SetActive(false);
                camera.orthographicSize = 3.8f;
                var ship = Object.Instantiate(Load<GameObject>(Assets + "VanguardStructure.fbx"), root.transform);
                ship.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                var allRenderers = ship.GetComponentsInChildren<MeshRenderer>();
                var renderers = Array.FindAll(allRenderers, r => r.name != "Vanguard structural ink");
                var structure = Array.Find(allRenderers, r => r.name == "Vanguard structural ink");
                Assert.That(structure, Is.Not.Null);
                var graphite = NewMaterial("Astronomical/Comparison/Drawn Surface");
                graphite.SetFloat("_TextureStrength", 1);
                graphite.SetFloat("_LineStrength", 0);
                graphite.SetFloat("_SpecularStrength", 0);
                graphite.SetColor("_BaseColor", new Color(.003f, .004f, .009f));
                structure.sharedMaterial = graphite;
                structure.shadowCastingMode = ShadowCastingMode.Off;
                structure.gameObject.layer = 30;
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                ship.transform.localScale *= 6 / bounds.size.y;
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                ship.transform.position -= bounds.center;
                var pose = new GameObject("Ship pose");
                pose.transform.SetParent(root.transform, false);
                ship.transform.SetParent(pose.transform, true);
                var texture = Load<Texture2D>(Assets + "VanguardBaseColor.png");
                var contour = NewMaterial("Astronomical/Comparison/Drawn Contour");
                contour.SetColor("_ContourColor", new Color(.003f, .004f, .009f));
                contour.SetFloat("_ContourPixels", 3.8f);
                contour.SetFloat("_ContourMinimum", .8f);
                var outlines = new List<GameObject>();
                var standard = new List<Material>();
                var drawn = new List<Material>();
                foreach (var renderer in renderers)
                {
                    renderer.gameObject.layer = 30;
                    var core = renderer.name.Contains("Nacelle Core");
                    var canopy = renderer.name.Contains("Canopy");
                    var tint = core ? new Color(39 / 255f, 157 / 255f, 1) :
                        canopy ? new Color(.025f, .065f, .15f) : Color.white;
                    var plain = NewMaterial("Universal Render Pipeline/Lit");
                    plain.SetColor("_BaseColor", tint);
                    plain.SetFloat("_Smoothness", .08f);
                    if (!core && !canopy) plain.SetTexture("_BaseMap", texture);
                    var paint = NewMaterial("Astronomical/Comparison/Drawn Surface");
                    paint.SetColor("_BaseColor", tint);
                    paint.SetFloat("_TextureStrength", 1);
                    paint.SetFloat("_LineStrength", 0);
                    paint.SetFloat("_WearStrength", 0);
                    paint.SetFloat("_ShadowThreshold", .55f);
                    paint.SetFloat("_ShadowSoftness", .18f);
                    paint.SetFloat("_CastShadowStrength", .96f);
                    paint.SetFloat("_PaletteLighting", 1);
                    paint.SetFloat("_AmbientStrength", .35f);
                    paint.SetFloat("_SpecularStrength", 0);
                    paint.SetColor("_ShadowColor", new Color(.35f, .45f, .8f));
                    if (!core && !canopy) paint.SetTexture("_BaseMap", texture);
                    if (core)
                    {
                        plain.SetColor("_BaseColor", Color.black);
                        plain.EnableKeyword("_EMISSION");
                        plain.SetColor("_EmissionColor", tint.linear);
                        paint.SetColor("_BaseColor", Color.black);
                        paint.SetTexture("_EmissionMap", Texture2D.whiteTexture);
                        paint.SetColor("_EmissionColor", tint.linear);
                        paint.SetFloat("_EmissionStrength", 1);
                    }
                    standard.Add(plain); drawn.Add(paint);
                    var outline = new GameObject("Silhouette", typeof(MeshFilter), typeof(MeshRenderer));
                    outline.transform.SetParent(renderer.transform, false);
                    outline.layer = 30;
                    outline.GetComponent<MeshFilter>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    var ink = outline.GetComponent<MeshRenderer>();
                    ink.sharedMaterial = contour;
                    ink.shadowCastingMode = ShadowCastingMode.Off;
                    ink.receiveShadows = false;
                    outlines.Add(outline);
                }
                yield return null;
                Color32[] before = null;
                Color32[] after = null;
                for (var treatment = 0; treatment < 2; treatment++)
                {
                    structure.gameObject.SetActive(treatment == 1);
                    for (var i = 0; i < renderers.Length; i++)
                    {
                        renderers[i].sharedMaterial = treatment == 0 ? standard[i] : drawn[i];
                        outlines[i].SetActive(treatment == 1);
                    }
                    var label = treatment == 0 ? "baseline" : "drawn";
                    pose.transform.rotation = Quaternion.identity;
                    camera.orthographicSize = 3.8f;
                    var pixels = Read(label + "-top");
                    if (treatment == 0) before = pixels; else after = pixels;
                    pose.transform.rotation = Quaternion.Euler(28, -24, -30);
                    Read(label + "-quarter");
                    pose.transform.rotation = Quaternion.identity;
                    camera.orthographicSize = 26;
                    contour.SetFloat("_ContourPixels", 1.4f);
                    Read(label + "-gameplay");
                    contour.SetFloat("_ContourPixels", 3.8f);
                }
                camera.orthographicSize = 3.8f;
                pose.transform.rotation = Quaternion.Euler(28, -24, -30);
                var firstLight = Read("drawn-light-left");
                light.transform.rotation = Quaternion.Euler(-20, 50, 0);
                var secondLight = Read("drawn-light-right");
                Assert.That(Difference(firstLight, secondLight), Is.GreaterThan(5000), "Lighting must move across the same paint.");
                Assert.That(Difference(before, after), Is.GreaterThan(5000), "The native treatments must produce different shaded surfaces.");
                light.transform.rotation = Quaternion.Euler(25, -35, 0);
                for (var bank = -30; bank <= 30; bank += 30)
                {
                    pose.transform.rotation = Quaternion.Euler(0, bank, 0);
                    Read("drawn-bank-" + bank);
                }
                File.WriteAllText(Path.Combine(output, "capture.json"), JsonUtility.ToJson(new CaptureEvidence
                {
                    model = Assets + "VanguardStructure.fbx", lengthWorld = 6, gameplayHalfHeight = 26,
                    changedLightingPixels = Difference(firstLight, secondLight), changedTreatmentPixels = Difference(before, after)
                }, true));

                Material NewMaterial(string shaderName)
                {
                    var shader = Shader.Find(shaderName);
                    Assert.That(shader && shader.isSupported, Is.True, shaderName);
                    var material = new Material(shader);
                    owned.Add(material);
                    return material;
                }
            }
            finally
            {
                RenderTexture.active = previousTarget;
                RenderSettings.sun = previousSun;
                RenderSettings.ambientMode = previousMode;
                RenderSettings.ambientLight = previousAmbient;
                Object.DestroyImmediate(root);
                foreach (var item in owned) Object.DestroyImmediate(item);
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        private static T Load<T>(string path) where T : Object
        {
            var item = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(item, Is.Not.Null, path);
            return item;
        }

        private static int Difference(Color32[] first, Color32[] second)
        {
            var count = 0;
            for (var i = 0; i < first.Length; i++)
                if (Math.Abs(first[i].r - second[i].r) + Math.Abs(first[i].g - second[i].g) + Math.Abs(first[i].b - second[i].b) > 40) count++;
            return count;
        }

        [Serializable]
        private struct CaptureEvidence
        {
            public string model;
            public float lengthWorld, gameplayHalfHeight;
            public int changedLightingPixels, changedTreatmentPixels;
        }
    }
}
#endif
