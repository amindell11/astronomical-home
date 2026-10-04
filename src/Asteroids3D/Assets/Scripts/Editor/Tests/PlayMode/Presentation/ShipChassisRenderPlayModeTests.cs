#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Substrate;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    /// <summary>
    /// Golden-image check of a whole chassis prefab: the ship is rendered at three yaws with a fixed
    /// orthographic camera and key light that see only the Ship layer, particle renderers off, and
    /// compared pixel by pixel against <c>results/ship-render/baseline/</c>. A missing baseline is
    /// recorded from the current render, so the first run on a fresh checkout seeds it and the next
    /// run compares.
    /// </summary>
    [Category("Presentation"), Category("RequiresGraphics")]
    public sealed class ShipChassisRenderPlayModeTests
    {
        private const int Size = 768;
        private const int ChangedThreshold = 6;

        private static readonly string OutputDirectory =
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/ship-render"));

        [TestCase("Assets/Prefabs/Ships/Crimson.prefab", 0)]
        [TestCase("Assets/Prefabs/Ships/Crimson.prefab", 90)]
        [TestCase("Assets/Prefabs/Ships/Crimson.prefab", 135)]
        public void Chassis_RendersPixelIdenticalToBaseline(string chassisPath, int yaw)
        {
            var stem = $"{Path.GetFileNameWithoutExtension(chassisPath)}-{yaw}";
            var root = new GameObject("Chassis render");
            var priorTarget = RenderTexture.active;
            var priorAmbient = RenderSettings.ambientLight;
            var priorMode = RenderSettings.ambientMode;
            var target = new RenderTexture(Size, Size, 24);
            var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            try
            {
                var prefab = AssetDatabase.LoadMainAssetAtPath(chassisPath) as GameObject;
                Assert.That(prefab, Is.Not.Null, chassisPath);
                root.SetActive(false);
                var subject = Object.Instantiate(prefab, root.transform);
                foreach (var behaviour in subject.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(behaviour.GetType().Namespace ?? "").StartsWith("UnityEngine", StringComparison.Ordinal))
                        Object.DestroyImmediate(behaviour);
                foreach (var particles in subject.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    particles.enabled = false;
                subject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 0, yaw));
                var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.position = new Vector3(0, 0, -6);
                camera.transform.LookAt(Vector3.zero, Vector3.up);
                camera.orthographic = true;
                camera.orthographicSize = 1.4f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.25f, .28f, .35f);
                camera.cullingMask = 1 << LayerIds.Ship;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.targetTexture = target;
                var light = new GameObject("Key light", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(25, -35, 0);
                light.color = new Color(1, .96f, .9f);
                light.intensity = 1.15f;
                light.cullingMask = 1 << LayerIds.Ship;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.22f, .25f, .32f);
                root.SetActive(true);
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                image.Apply();
                var current = image.GetPixels32();
                Directory.CreateDirectory(OutputDirectory);
                File.WriteAllBytes(Path.Combine(OutputDirectory, stem + ".png"), image.EncodeToPNG());
                Assert.That(current.Any(p => p.r != current[0].r || p.g != current[0].g || p.b != current[0].b), Is.True,
                    "The render must show the chassis, not a uniform frame.");

                var baselineDirectory = Path.Combine(OutputDirectory, "baseline");
                var baselinePath = Path.Combine(baselineDirectory, stem + ".png");
                if (!File.Exists(baselinePath))
                {
                    Directory.CreateDirectory(baselineDirectory);
                    File.Copy(Path.Combine(OutputDirectory, stem + ".png"), baselinePath);
                    Debug.Log($"{stem}: no baseline found; recorded {baselinePath}. Run again to compare.");
                    return;
                }
                var baselineImage = new Texture2D(2, 2, TextureFormat.RGB24, false);
                Assert.That(baselineImage.LoadImage(File.ReadAllBytes(baselinePath)), Is.True, baselinePath);
                Assert.That((baselineImage.width, baselineImage.height), Is.EqualTo((Size, Size)), baselinePath);
                var baseline = baselineImage.GetPixels32();
                Object.DestroyImmediate(baselineImage);
                var maximum = 0;
                var changed = 0;
                for (var i = 0; i < current.Length; i++)
                {
                    var difference = Difference(current[i], baseline[i]);
                    maximum = Math.Max(maximum, difference);
                    if (difference > ChangedThreshold) changed++;
                }
                File.AppendAllText(Path.Combine(OutputDirectory, "report.txt"),
                    $"{stem}: changed pixels={changed}; max difference={maximum}\n");
                Assert.That(changed, Is.Zero, $"{stem}: {changed} pixels differ from the baseline by more than {ChangedThreshold} (max {maximum}).");
            }
            finally
            {
                RenderTexture.active = priorTarget;
                RenderSettings.ambientLight = priorAmbient;
                RenderSettings.ambientMode = priorMode;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }

        private static int Difference(Color32 a, Color32 b) =>
            Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
    }
}
#endif
