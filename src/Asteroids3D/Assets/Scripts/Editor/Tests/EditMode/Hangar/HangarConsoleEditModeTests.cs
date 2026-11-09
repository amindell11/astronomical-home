using System.Linq;
using System.IO;
using NUnit.Framework;
using UI.Hangar.Hardware;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Hangar
{
    [Category("UI")]
    public sealed class HangarConsoleEditModeTests
    {
        private const string PrefabPath = "Assets/Prefabs/UI/HangarConsole.prefab";

        [Test]
        public void ControlsHaveIndependentUnitScalePivotsAndRealMeshes()
        {
            var console = AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath);
            var parts = new[]
            {
                console.MainScreenGlass.transform, console.DetailScreenGlass.transform,
                console.SelectorKnob, console.PreviousButton, console.NextButton,
                console.LaunchButton, console.ProjectorLens.transform
            };
            Assert.That(parts.Select(part => part.name), Is.EquivalentTo(new[]
            {
                "MainScreenGlass", "DetailScreenGlass", "SelectorKnob", "PreviousButton",
                "NextButton", "LaunchButton", "ProjectorLens"
            }));
            Assert.That(parts.Distinct().Count(), Is.EqualTo(7));
            foreach (var part in parts)
            {
                Assert.That(Vector3.Distance(part.lossyScale, Vector3.one), Is.LessThan(.001f), part.name);
                var mesh = part.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.GreaterThan(8), part.name);
                Assert.That(mesh.bounds.center.magnitude, Is.LessThan(.5f), part.name + " pivot");
            }
        }

        [TestCase(1920, 1080)]
        [TestCase(1920, 1200)]
        [TestCase(2520, 1080)]
        public void AllControlsRemainInsideTheFrame(int width, int height)
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath));
            try
            {
                instance.Resize(new Vector2Int(width, height));
                var camera = instance.HardwareCamera;
                camera.aspect = (float)width / height;
                foreach (var control in new[] { instance.SelectorKnob, instance.PreviousButton,
                             instance.NextButton, instance.LaunchButton, instance.ProjectorLens.transform })
                {
                    var point = camera.WorldToViewportPoint(control.position);
                    Assert.That(point.x, Is.InRange(.01f, .99f), control.name);
                    Assert.That(point.y, Is.InRange(.01f, .99f), control.name);
                    Assert.That(point.z, Is.GreaterThan(0f), control.name);
                }
                Assert.That(instance.Texture.width, Is.EqualTo(width));
                Assert.That(instance.Texture.height, Is.EqualTo(height));
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void TallerViewKeepsHeaderAndDeckCoveringTheFrame()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath));
            try
            {
                instance.Resize(new Vector2Int(1920, 1080));
                var camera = instance.HardwareCamera;
                var top = camera.ViewportToWorldPoint(new Vector3(.5f, 1f, 10f));
                var bottom = camera.ViewportToWorldPoint(new Vector3(.5f, 0f, 10f));
                // Source-camera silhouette measurements constrain the finite header and deck.
                var headerEdge = camera.ViewportToWorldPoint(new Vector3(.5f, 1.035f, 10f));
                var deckEdge = camera.ViewportToWorldPoint(new Vector3(.5f, -.076f, 10f));

                instance.Resize(new Vector2Int(1920, 1200));
                Assert.That(camera.WorldToViewportPoint(headerEdge).y, Is.InRange(1f, 1.01f));
                Assert.That(camera.WorldToViewportPoint(deckEdge).y, Is.InRange(-.01f, 0f));

                foreach (var size in new[] { new Vector2Int(2520, 1080), new Vector2Int(1920, 1080) })
                {
                    instance.Resize(size);
                    Assert.That(camera.WorldToViewportPoint(top).y, Is.EqualTo(1f).Within(.0001f));
                    Assert.That(camera.WorldToViewportPoint(bottom).y, Is.EqualTo(0f).Within(.0001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void CameraMatchesTheApprovedLensAndPose()
        {
            var console = AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath);
            var camera = console.HardwareCamera;
            Assert.That(Vector3.Distance(camera.transform.localPosition, new Vector3(0f, 6.8f, -18f)), Is.LessThan(.0001f));
            Assert.That(Vector3.Angle(camera.transform.forward, new Vector3(0f, -.1131574f, .9935771f)), Is.LessThan(.001f));
            Assert.That(camera.fieldOfView, Is.EqualTo(34.11393f).Within(.0001f));
        }

        [Test]
        public void GlassUsesTheWholeDisplayUvRange()
        {
            var console = AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath);
            foreach (var glass in new[] { console.MainScreenGlass, console.DetailScreenGlass })
            {
                var mesh = glass.GetComponent<MeshFilter>().sharedMesh;
                var data = Mesh.AcquireReadOnlyMeshData(mesh);
                using var uv = new Unity.Collections.NativeArray<Vector2>(data[0].vertexCount, Unity.Collections.Allocator.Temp);
                data[0].GetUVs(0, uv);
                Assert.That(uv.Min(point => point.x), Is.EqualTo(0f).Within(.001f), glass.name);
                Assert.That(uv.Max(point => point.x), Is.EqualTo(1f).Within(.001f), glass.name);
                Assert.That(uv.Min(point => point.y), Is.EqualTo(0f).Within(.001f), glass.name);
                Assert.That(uv.Max(point => point.y), Is.EqualTo(1f).Within(.001f), glass.name);
                data.Dispose();
            }
        }

        [Test]
        [Category("RequiresLfsPng")]
        public void StaticDisplaysKeepTheirAuthoredPartialPixelCoverage()
        {
            var console = AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath);
            foreach (var glass in new[] { console.MainScreenGlass, console.DetailScreenGlass })
            {
                var material = glass.sharedMaterial;
                var path = AssetDatabase.GetAssetPath(material.GetTexture("_EmissionMap"));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), glass.name);
                Assert.That(material.GetFloat("_UseEmissionAlpha"), Is.EqualTo(1f), glass.name);
                var texture = new Texture2D(2, 2);
                try
                {
                    ImageConversion.LoadImage(texture, File.ReadAllBytes(path));
                    Assert.That(texture.GetPixels32().Count(pixel => pixel.a > 0 && pixel.a < 255),
                        Is.GreaterThan(50), glass.name + " antialiased reference pixels");
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }
    }
}
