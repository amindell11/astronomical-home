#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Rendering
{
    [Category("Camera"), Category("RequiresGraphics")]
    public sealed class AsteroidLightingPlayModeTests
    {
        [Test]
        public void StationaryAsteroid_DarkRegionsRespondToLight()
        {
            const int size = 512;
            const string folder = "Assets/Visuals/Environment/Asteroids/DrawnStudy/";
            var output = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../results/asteroid-light-study/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(output);
            TestContext.Progress.WriteLine("ASTEROID_LIGHT_STUDY=" + output);
            var priorTarget = RenderTexture.active;
            var priorSun = RenderSettings.sun;
            var quality = QualitySettings.GetQualityLevel();
            var root = new GameObject("Asteroid light response");
            var resources = new List<Object>();
            try
            {
                QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "High Fidelity"), true);
                var source = AssetDatabase.LoadAssetAtPath<Material>(folder + "AsteroidFracturePaint.mat");
                Assert.That(source, Is.Not.Null);
                var material = new Material(source);
                resources.Add(material);
                var rock = new GameObject("Stationary rock", typeof(MeshFilter), typeof(MeshRenderer));
                rock.transform.SetParent(root.transform, false);
                rock.layer = 31;
                rock.transform.rotation = Quaternion.Euler(20, 0, 12);
                rock.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<GameObject>(
                    folder + "AsteroidFractureStudy.fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
                rock.GetComponent<MeshRenderer>().sharedMaterial = material;

                var light = new GameObject("Orbiting key", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Directional;
                light.intensity = 1;
                light.shadows = LightShadows.None;
                light.shadowBias = .025f;
                light.shadowNormalBias = .1f;
                light.cullingMask = 1 << 31;
                RenderSettings.sun = light;
                var camera = new GameObject("Fixed camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.position = new Vector3(0, 0, 6);
                camera.transform.LookAt(Vector3.zero);
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.10f, .13f, .19f);
                camera.allowHDR = false;
                camera.allowMSAA = false;
                var target = new RenderTexture(size, size, 24);
                resources.Add(target);
                camera.targetTexture = target;
                var image = new Texture2D(size, size, TextureFormat.RGB24, false);
                resources.Add(image);
                Color32[] Read(string name, bool normals = false)
                {
                    if (normals)
                    {
                        using var commands = new UnityEngine.Rendering.CommandBuffer();
                        commands.SetRenderTarget(target);
                        commands.ClearRenderTarget(true, true, Color.black);
                        commands.SetViewProjectionMatrices(camera.worldToCameraMatrix,
                            GL.GetGPUProjectionMatrix(camera.projectionMatrix, true));
                        commands.DrawMesh(rock.GetComponent<MeshFilter>().sharedMesh, rock.transform.localToWorldMatrix,
                            material, 0, material.FindPass("DepthNormals"));
                        Graphics.ExecuteCommandBuffer(commands);
                    }
                    else camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                    return image.GetPixels32();
                }

                light.transform.rotation = Quaternion.Euler(0, 180, 0);
                material.SetFloat("_AmbientStrength", 0);
                material.SetColor("_BaseColor", Color.white);
                material.SetColor("_ShadowColor", Color.white);
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                material.SetFloat("_LineStrength", 0);
                var mask = Read("mask");
                material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                material.SetFloat("_LineStrength", source.GetFloat("_LineStrength"));
                var albedo = Read("fully-lit");
                var drawing = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    folder + "AsteroidSurfaceDrawing.fbx"), rock.transform);
                var drawingMaterial = AssetDatabase.LoadAssetAtPath<Material>(folder + "AsteroidSurfaceDrawing.mat");
                foreach (var renderer in drawing.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.gameObject.layer = rock.layer;
                    renderer.sharedMaterial = drawingMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var drawnAlbedo = Read("drawing-fully-lit");
                var drawingPixels = 0;
                for (var i = 0; i < mask.Length; i++)
                    if (mask[i].r >= 245 && mask[i].g >= 245 && mask[i].b >= 245 &&
                        albedo[i].r - drawnAlbedo[i].r > 20) drawingPixels++;
                drawing.SetActive(false);
                material.CopyPropertiesFromMaterial(source);
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(20, 115, 0);
                var left = Read("light-left");
                light.transform.rotation = Quaternion.Euler(20, 245, 0);
                var right = Read("light-right");
                material.DisableKeyword("_NORMALMAP");
                var smoothRight = Read("relief-off-right");
                light.transform.rotation = Quaternion.Euler(20, 115, 0);
                var smoothLeft = Read("relief-off-left");
                var smoothNormals = Read("depth-normal-off", true);
                material.EnableKeyword("_NORMALMAP");
                var reliefNormals = Read("depth-normal-on", true);
                var normalPixels = 0;
                for (var i = 0; i < smoothNormals.Length; i++)
                    if (Math.Abs(reliefNormals[i].r - smoothNormals[i].r) +
                        Math.Abs(reliefNormals[i].g - smoothNormals[i].g) +
                        Math.Abs(reliefNormals[i].b - smoothNormals[i].b) > 20) normalPixels++;
                var reliefPixels = 0;
                var reliefReversals = 0;
                for (var i = 0; i < mask.Length; i++)
                {
                    if (mask[i].r < 245 || mask[i].g < 245 || mask[i].b < 245) continue;
                    var deltaLeft = left[i].r + left[i].g + left[i].b - smoothLeft[i].r - smoothLeft[i].g - smoothLeft[i].b;
                    var deltaRight = right[i].r + right[i].g + right[i].b - smoothRight[i].r - smoothRight[i].g - smoothRight[i].b;
                    if (Math.Abs(deltaLeft) > 35 || Math.Abs(deltaRight) > 35) reliefPixels++;
                    if ((deltaLeft > 20 && deltaRight < -20) || (deltaLeft < -20 && deltaRight > 20)) reliefReversals++;
                }
                var surface = 0;
                var fixedBlack = 0;
                var changedDark = 0;
                for (var i = 0; i < mask.Length; i++)
                {
                    if (mask[i].r < 245 || mask[i].g < 245 || mask[i].b < 245) continue;
                    surface++;
                    if (Dark(albedo[i])) fixedBlack++;
                    if (Dark(left[i]) != Dark(right[i])) changedDark++;
                }
                drawing.SetActive(true);
                var contourMaterial = new Material(Shader.Find("Astronomical/Comparison/Drawn Contour"));
                resources.Add(contourMaterial);
                contourMaterial.SetFloat("_ContourPixels", 5.5f);
                contourMaterial.SetFloat("_ContourMinimum", .6f);
                contourMaterial.SetColor("_ContourColor", new Color(.003f, .004f, .009f));
                var shell = new GameObject("Study contour", typeof(MeshFilter), typeof(MeshRenderer));
                shell.transform.SetParent(rock.transform, false);
                shell.layer = rock.layer;
                shell.GetComponent<MeshFilter>().sharedMesh = rock.GetComponent<MeshFilter>().sharedMesh;
                var outline = shell.GetComponent<MeshRenderer>();
                outline.sharedMaterial = contourMaterial;
                outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outline.receiveShadows = false;
                light.transform.rotation = Quaternion.Euler(20, 115, 0);
                Read("drawing-left");
                light.transform.rotation = Quaternion.Euler(20, 245, 0);
                Read("drawing-right");
                for (var frame = 0; frame < 72; frame++)
                {
                    light.transform.rotation = Quaternion.Euler(20, 100 + frame * 5, 0);
                    Read("f_" + frame.ToString("D5"));
                }
                var pose = rock.transform.rotation;
                light.transform.rotation = Quaternion.Euler(20, 115, 0);
                var turntable = Path.Combine(output, "turntable");
                Directory.CreateDirectory(turntable);
                for (var frame = 0; frame < 72; frame++)
                {
                    rock.transform.rotation = Quaternion.Euler(20, frame * 5, 12);
                    Read("turntable/f_" + frame.ToString("D5"));
                }
                File.WriteAllText(Path.Combine(turntable, "manifest.json"),
                    "{\"width\":512,\"height\":512,\"suggestedFps\":24,\"steps\":72}");
                rock.transform.rotation = pose;
                light.enabled = false;
                RenderSettings.sun = null;
                var sceneLights = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Scenes/Lighting.prefab"), root.transform);
                foreach (var sceneLight in sceneLights.GetComponentsInChildren<Light>()) sceneLight.cullingMask = 1 << 31;
                Read("scene-lighting");
                File.WriteAllText(Path.Combine(output, "manifest.json"),
                    "{\"width\":512,\"height\":512,\"suggestedFps\":24,\"steps\":72}");
                File.WriteAllText(Path.Combine(output, "measurement.json"),
                    $"{{\"normalPixels\":{normalPixels},\"reliefPixels\":{reliefPixels},\"reliefReversals\":{reliefReversals},\"drawingPixels\":{drawingPixels},\"surfacePixels\":{surface},\"fixedBlackPixels\":{fixedBlack},\"changedDarkPixels\":{changedDark}}}");
                Assert.That(normalPixels, Is.GreaterThan(50), "Depth normals must include the sculpted relief.");
                Assert.That(reliefPixels, Is.GreaterThan(200), "Sculpted relief must visibly affect lighting.");
                Assert.That(reliefReversals, Is.GreaterThan(50), "Relief must brighten and darken with opposed lights.");
                Assert.That(drawingPixels, Is.InRange(60, surface / 12), "Authored drawing must remain sparse linework.");
                Assert.That(surface, Is.GreaterThan(10000), "The stationary mesh must fill the diagnostic silhouette.");
                Assert.That(fixedBlack, Is.LessThan(surface / 1000), "Fully lit stone must not contain painted black shadows.");
                Assert.That(changedDark, Is.GreaterThan(surface / 10), "Moving the light must move substantial dark regions.");
            }
            finally
            {
                RenderTexture.active = priorTarget;
                RenderSettings.sun = priorSun;
                Object.DestroyImmediate(root);
                foreach (var resource in resources) Object.DestroyImmediate(resource);
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        private static bool Dark(Color32 pixel) => pixel.r < 35 && pixel.g < 35 && pixel.b < 35;
    }
}
#endif
