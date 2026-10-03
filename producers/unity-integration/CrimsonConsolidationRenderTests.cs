using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Rendering.Illustrated
{
    [Category("Presentation"), Category("RequiresGraphics")]
    public sealed class CrimsonConsolidationRenderTests
    {
        [TestCase(0)]
        [TestCase(90)]
        [TestCase(135)]
        public void CombinedMaterialsPreserveTheRenderedHull(int yaw)
        {
            var root = new GameObject("Crimson render comparison");
            var priorTarget = RenderTexture.active;
            var priorAmbient = RenderSettings.ambientLight;
            var priorMode = RenderSettings.ambientMode;
            var target = new RenderTexture(768, 768, 24);
            var image = new Texture2D(768, 768, TextureFormat.RGB24, false);
            var surface = new Mesh { indexFormat = IndexFormat.UInt32 };
            var outline = new Mesh { indexFormat = IndexFormat.UInt32 };
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ships/Ship_2_IllustratedRig.prefab");
                var original = prefab.transform.Find("Model/Crimson");
                var subject = Object.Instantiate(original.gameObject, root.transform);
                subject.transform.localPosition = original.localPosition;
                subject.transform.localRotation = Quaternion.Euler(0, 0, yaw) * original.localRotation;
                subject.transform.localScale = original.localScale;
                subject.layer = 31;
                var renderer = subject.GetComponent<MeshRenderer>();
                var mesh = subject.GetComponent<MeshFilter>().sharedMesh;
                surface.CombineMeshes(new[] { new CombineInstance { mesh = mesh, subMeshIndex = 0 } }, true, false);
                outline.CombineMeshes(new[] { new CombineInstance { mesh = mesh, subMeshIndex = 1 } }, true, false);
                var split = new GameObject("Split reference");
                split.transform.SetParent(root.transform, false);
                split.transform.localPosition = subject.transform.localPosition;
                split.transform.localRotation = subject.transform.localRotation;
                split.transform.localScale = subject.transform.localScale;
                var painted = new GameObject("Paint", typeof(MeshFilter), typeof(MeshRenderer));
                painted.transform.SetParent(split.transform, false);
                painted.layer = 31;
                painted.GetComponent<MeshFilter>().sharedMesh = surface;
                painted.GetComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterials[0];
                var ink = new GameObject("Ink", typeof(MeshFilter), typeof(MeshRenderer));
                ink.transform.SetParent(split.transform, false);
                ink.layer = 31;
                ink.GetComponent<MeshFilter>().sharedMesh = outline;
                ink.GetComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterials[1];
                ink.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                ink.GetComponent<MeshRenderer>().receiveShadows = false;
                var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.position = new Vector3(0, 0, -6);
                camera.transform.LookAt(Vector3.zero, Vector3.up);
                camera.orthographic = true;
                camera.orthographicSize = 1.4f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.25f, .28f, .35f);
                camera.cullingMask = 1 << 31;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.targetTexture = target;
                var light = new GameObject("Key light", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(25, -35, 0);
                light.color = new Color(1, .96f, .9f);
                light.intensity = 1.15f;
                light.cullingMask = 1 << 31;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.22f, .25f, .32f);
                const string directory = "D:/amind/git/astronomical-home/results/visual-playable/";
                subject.SetActive(false);
                var reference = Read(camera, target, image);
                File.WriteAllBytes(directory + $"crimson-split-reference-{yaw}.png", image.EncodeToPNG());
                ink.SetActive(false);
                var withoutInk = Read(camera, target, image);
                split.SetActive(false);
                subject.SetActive(true);
                var combined = Read(camera, target, image);
                File.WriteAllBytes(directory + $"crimson-single-mesh-{yaw}.png", image.EncodeToPNG());
                var different = combined.Where((pixel, i) => Difference(pixel, reference[i]) > 6).Count();
                var inkPixels = combined.Where((pixel, i) => Difference(pixel, withoutInk[i]) > 20).Count();
                Assert.That(different, Is.LessThan(300), "Combining materials must preserve the rendered appearance.");
                Assert.That(inkPixels, Is.GreaterThan(100), "The combined renderer must still draw visible outlines.");
                File.AppendAllText(directory + "crimson-render-comparison.txt", $"yaw={yaw}: changed pixels={different}; outline pixels={inkPixels}\n");
            }
            finally
            {
                RenderTexture.active = priorTarget;
                RenderSettings.ambientLight = priorAmbient;
                RenderSettings.ambientMode = priorMode;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(surface);
                Object.DestroyImmediate(outline);
            }
        }

        static int Difference(Color32 a, Color32 b) => Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
        static Color32[] Read(Camera camera, RenderTexture target, Texture2D image)
        {
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            return image.GetPixels32();
        }
    }
}
