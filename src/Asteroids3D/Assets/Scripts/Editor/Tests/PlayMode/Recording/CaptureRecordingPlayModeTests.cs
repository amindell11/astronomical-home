#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Capture.Recording;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Recording
{
    [Category("Camera"), Category("RequiresGraphics")]
    public sealed class CaptureRecordingPlayModeTests
    {
        private GameObject stage;
        private IEpisodeCapture capture;
        private float priorScale;
        private float priorCaptureDelta;

        [SetUp]
        public void SetUp()
        {
            priorScale = Time.timeScale;
            priorCaptureDelta = Time.captureDeltaTime;
        }

        [TearDown]
        public void TearDown()
        {
            if (capture is Object module && module) Object.DestroyImmediate(module);
            if (stage) Object.DestroyImmediate(stage);
            Time.timeScale = priorScale;
            Time.captureDeltaTime = priorCaptureDelta;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RecordingPairsRenderedIdsAndTimesAndRestoresAfterFinishAbortAndWriteFailure()
        {
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/capture",
                "recording-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
            var priorBackground = Application.runInBackground;
            var priorFocus = UnityEditor.EditorWindow.focusedWindow;
            var viewType = Type.GetType("Capture.GameView.UnityGameViewAdapter, Capture.GameView.Editor", true);
            var viewAdapter = Activator.CreateInstance(viewType);
            string ViewSnapshot() => JsonUtility.ToJson(viewType.GetMethod("Snapshot").Invoke(viewAdapter, null));
            var priorView = ViewSnapshot();
            stage = new GameObject("Prepared capture view");
            var camera = stage.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.blue;
            camera.depth = 1000;
            var canvasRoot = new GameObject("Frame identity HUD", typeof(RectTransform));
            canvasRoot.transform.SetParent(stage.transform);
            canvasRoot.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var bits = new Image[8];
            for (var bit = 0; bit < bits.Length; bit++)
            {
                var panel = new GameObject("bit " + bit, typeof(RectTransform), typeof(CanvasRenderer));
                panel.transform.SetParent(canvasRoot.transform, false);
                var rect = (RectTransform)panel.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(8 + bit * 24, 8);
                rect.sizeDelta = new Vector2(20, 20);
                bits[bit] = panel.AddComponent<Image>();
            }
            var marker = stage.AddComponent<CaptureFrameMarker>();
            marker.Initialize(bits);
            capture = TestAssets.NewNativeCapture();
            CaptureRequest Request(string name) => new(output, name, TestAssets.CaptureCommit(), 320, 240, "proof",
                new Dictionary<string, Func<double>> { ["render_id"] = () => marker.FrameId });
            Time.timeScale = 0;
            Time.captureDeltaTime = 0;
            yield return null;
            yield return null;

            using (var prepared = capture.Prepare(Request("abort-before-ready"))) { }
            Assert.AreEqual(priorBackground, Application.runInBackground);
            Assert.AreEqual(priorFocus, UnityEditor.EditorWindow.focusedWindow);
            yield return AsyncAssert.WaitUntil(() => ViewSnapshot() == priorView, 30f,
                "recording-only Game View configuration and rendered dimensions restored after repaint");
            using (var prepared = capture.Prepare(Request("abort-prepared")))
            {
                yield return prepared.Ready;
                yield return null;
                Assert.IsEmpty(Directory.GetFiles(output, "*.png", SearchOption.AllDirectories), "preparation writes no images");
            }

            CaptureResult result;
            using (var recording = capture.Prepare(Request("finished")))
            {
                yield return recording.Ready;
                recording.Start();
                for (var frame = 0; frame < 20; frame++) yield return null;
                result = recording.Finish();
            }
            AssertPairs(result);
            var completedCount = Directory.GetFiles(result.FrameDirectory, "*.png").Length;
            yield return null;
            yield return null;
            Assert.AreEqual(completedCount, Directory.GetFiles(result.FrameDirectory, "*.png").Length,
                "Finish leaves no active frame pump");
            Assert.AreEqual(priorBackground, Application.runInBackground);
            Assert.AreEqual(priorFocus, UnityEditor.EditorWindow.focusedWindow);
            yield return AsyncAssert.WaitUntil(() => ViewSnapshot() == priorView, 30f,
                "Finish restored recording-only Game View configuration and rendered dimensions");

            using (var recording = capture.Prepare(Request("abort-recording")))
            {
                yield return recording.Ready;
                recording.Start();
                for (var frame = 0; frame < 4; frame++) yield return null;
            }
            var abortCount = Directory.GetFiles(output, "*.png", SearchOption.AllDirectories).Length;
            yield return null;
            yield return null;
            Assert.AreEqual(abortCount, Directory.GetFiles(output, "*.png", SearchOption.AllDirectories).Length,
                "Dispose cancels the automatic pump");

            using (var recording = capture.Prepare(Request("write-failure")))
            {
                yield return recording.Ready;
                var failureDirectory = Array.Find(Directory.GetDirectories(Path.Combine(output, "frames")),
                    path => Path.GetFileName(path).EndsWith("write-failure", StringComparison.Ordinal));
                recording.Start();
                for (var frame = 0; frame < 3; frame++) yield return null;
                var partialFrames = Directory.GetFiles(failureDirectory, "f_*.png");
                Assert.IsNotEmpty(partialFrames, "write failure follows successful images");
                var nextId = partialFrames.Length + 1;
                Directory.CreateDirectory(Path.Combine(failureDirectory, $"f_{nextId:00000}.png"));
                for (var frame = 0; frame < 3; frame++) yield return null;
                Assert.Throws<UnauthorizedAccessException>(() => recording.Finish());
                StringAssert.Contains("\"completed\": false", File.ReadAllText(Path.Combine(failureDirectory, "manifest.json")));
                var partialRows = File.ReadAllLines(Path.Combine(failureDirectory, "frames.csv"));
                Assert.AreEqual(partialFrames.Length + 1, partialRows.Length,
                    "failed PNG write publishes no metadata row");
                CollectionAssert.AreEquivalent(partialFrames, Directory.GetFiles(failureDirectory, "*.png"),
                    "completed partial evidence survives failure");
                for (var row = 1; row < partialRows.Length; row++)
                    Assert.IsTrue(File.Exists(Path.Combine(failureDirectory, partialRows[row].Split(',')[1])));
            }
            Assert.AreEqual(priorBackground, Application.runInBackground);
            Assert.AreEqual(priorFocus, UnityEditor.EditorWindow.focusedWindow);
            using (var recording = capture.Prepare(Request("reused")))
            {
                yield return recording.Ready;
                recording.Start();
                for (var frame = 0; frame < 4; frame++) yield return null;
                AssertPairs(recording.Finish());
            }
            Debug.Log("[Capture] lifecycle and exact frame identity proof -> " + output);
        }

        private static void AssertPairs(CaptureResult result)
        {
            var rows = File.ReadAllLines(result.FrameMetadataPath);
            Assert.Greater(result.FrameCount, 1);
            Assert.AreEqual(result.FrameCount + 1, rows.Length);
            Assert.AreEqual(result.FrameCount, Directory.GetFiles(result.FrameDirectory, "*.png").Length);
            var previous = double.NegativeInfinity;
            double? frozenSim = null;
            for (var index = 1; index < rows.Length; index++)
            {
                var row = rows[index].Split(',');
                Assert.AreEqual(index.ToString(), row[0]);
                Assert.AreEqual($"f_{index:00000}.png", row[1]);
                var unscaled = double.Parse(row[2], CultureInfo.InvariantCulture);
                var sim = double.Parse(row[3], CultureInfo.InvariantCulture);
                Assert.Greater(unscaled, previous);
                previous = unscaled;
                frozenSim ??= sim;
                Assert.AreEqual(frozenSim.Value, sim, "capture does not advance paused simulation");
                var image = new Texture2D(2, 2);
                try
                {
                    Assert.IsTrue(ImageConversion.LoadImage(image, File.ReadAllBytes(Path.Combine(result.FrameDirectory, row[1]))));
                    Assert.AreEqual(320, image.width);
                    Assert.AreEqual(240, image.height);
                    var id = 0;
                    for (var bit = 0; bit < 8; bit++)
                        if (image.GetPixel(18 + bit * 24, 18).r > 0.5f) id |= 1 << bit;
                    Assert.AreEqual(int.Parse(row[4], CultureInfo.InvariantCulture), id,
                        "PNG encodes exactly the render identity sampled in its metadata row");
                }
                finally { Object.DestroyImmediate(image); }
            }
        }
    }
}
#endif
