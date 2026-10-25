#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Capture.Recording;
using NUnit.Framework;
using Ships.Loadout;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Recording
{
    [Category("Camera"), Category("RequiresGraphics")]
    public sealed class CapturePresetPlayModeTests
    {
        private IEpisodeCapture capture;
        private float priorCaptureDelta;

        [SetUp]
        public void SetUp()
        {
            priorCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 0;
            capture = TestAssets.NewNativeCapture();
        }

        [TearDown]
        public void TearDown()
        {
            if (capture is Object module && module) Object.DestroyImmediate(module);
            Time.captureDeltaTime = priorCaptureDelta;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator NeutralLineupRecordsSelectedHullsAndRestoresItsPresentationScope()
        {
            yield return null;
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            var priorPresentation = CaptureViewAssertions.PresentationSnapshot();
            var priorView = CaptureViewAssertions.ViewSnapshot();
            var prepare = capture.GetType().GetMethod("PrepareLineupNeutral");
            var invalid = new[] { catalog.Chassis.Single(ship => ship.name == "Junker_1") };
            var failure = Assert.Throws<TargetInvocationException>(() => prepare.Invoke(capture, new object[] { invalid }));
            StringAssert.Contains("visual rig on Junker_1", failure.InnerException.Message);
            Assert.AreEqual(priorPresentation, CaptureViewAssertions.PresentationSnapshot());
            var names = new[] { "Vanguard", "Crimson", "Nightshade", "Valis", "Bulwark" };
            var subjects = names.Select(name => catalog.Chassis.Single(ship => ship.name == name)).ToArray();
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            using ((IDisposable)prepare.Invoke(capture, new object[] { subjects }))
            {
                var prepared = CaptureViewAssertions.PresentationSnapshot();
                Assert.AreEqual(UnityEngine.Rendering.AmbientMode.Flat, RenderSettings.ambientMode);
                Assert.AreEqual(new Color(.24f, .24f, .24f), RenderSettings.ambientLight);
                Assert.AreEqual("Performant", QualitySettings.names[QualitySettings.GetQualityLevel()]);
                Assert.IsFalse(RenderSettings.fog);
                Assert.IsFalse(ShaderUtil.allowAsyncCompilation);
                foreach (var width in new[] { 2048, 1024 })
                {
                    using var recording = capture.Prepare(new CaptureRequest("results/capture",
                        "lineup-neutral-" + width, TestAssets.CaptureCommit(), width, width / 4, stamp));
                    yield return recording.Ready;
                    yield return null;
                    recording.Start();
                    for (var frame = 0; frame < 6; frame++) yield return null;
                    var result = recording.Finish();
                    Assert.AreEqual(prepared, CaptureViewAssertions.PresentationSnapshot(), "recording cleanup preserves the prepared view's settings");
                    Assert.AreEqual(result.FrameCount + 1, File.ReadAllLines(result.FrameMetadataPath).Length);
                    StringAssert.Contains(TestAssets.CaptureCommit(), File.ReadAllText(result.ProvenancePath));
                    AssertLineup(result, width, subjects.Length);
                    Debug.Log("[Capture] neutral lineup -> " + result.FrameDirectory);
                }
            }
            yield return CaptureViewAssertions.Restored(priorPresentation, priorView);
        }

        private static void AssertLineup(CaptureResult result, int width, int chassisCount)
        {
            var files = Directory.GetFiles(result.FrameDirectory, "*.png");
            Assert.AreEqual(result.FrameCount, files.Length);
            Assert.Greater(files.Length, 1);
            Array.Sort(files, StringComparer.Ordinal);
            var image = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(image, File.ReadAllBytes(files[files.Length / 2])));
                Assert.AreEqual(width, image.width);
                Assert.AreEqual(width / 4, image.height);
                var pixels = image.GetPixels32();
                var hullPixels = new int[chassisCount];
                for (var y = 0; y < image.height; y++)
                    for (var x = 0; x < image.width; x++)
                    {
                        var pixel = pixels[y * image.width + x];
                        if (Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)) > 80)
                            hullPixels[Math.Min(chassisCount - 1, x * chassisCount / width)]++;
                    }
                foreach (var count in hullPixels) Assert.Greater(count, 100, "every chassis occupies visible pixels in the row");
            }
            finally { Object.DestroyImmediate(image); }
        }

    }
}
#endif
