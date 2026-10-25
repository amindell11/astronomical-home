#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Capture;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class CaptureWriterProofScenario : CaptureScenario
{
    private const int Width = 640;
    private const int Height = 360;
    private const int FramesPerWindow = 6;
    private readonly List<string> rows = new() { "id,filename,unscaled_seconds,sim_seconds,render_frame,gizmos,alpha,pixel_id,colored_ink,green_silhouette" };

    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.Combat;
    public override CaptureConfig Config => new()
    {
        clipName = "PrivateWriterProof", width = Width, height = Height, everyFixedSteps = 1,
    };

    public override IEnumerator Run()
    {
        var (a, _) = SpawnCombatShip(new Vector2(-6, 0), -90, 0);
        var (b, _) = SpawnCombatShip(new Vector2(6, 0), 90, 1);
        yield return new WaitForFixedUpdate();
        var selection = Selection.objects;
        var active = Selection.activeObject;
        var background = Application.runInBackground;
        var quality = QualitySettings.GetQualityLevel();
        var annotations = GizmoUtility.GetGizmoInfo();
        var timeScale = Time.timeScale;
        var captureDelta = Time.captureDeltaTime;
        PlayModeWindow.GetRenderingResolution(out var beforeWidth, out var beforeHeight);
        var canvasGo = new GameObject("WriterProofHUD", typeof(RectTransform), typeof(Canvas));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.GetComponent<Canvas>().sortingOrder = 30000;
        var bits = new Image[6];
        Panel(canvasGo.transform, new Rect(8, Height - 32, 24, 24), Color.red);
        Panel(canvasGo.transform, new Rect(8, 8, 24, 24), Color.green);
        Panel(canvasGo.transform, new Rect(Width - 32, 8, 24, 24), Color.blue);
        Panel(canvasGo.transform, new Rect(Width - 92, Height - 32, 60, 24), new Color(0.2f, 0.5f, 0.75f));
        for (var i = 0; i < bits.Length; i++)
            bits[i] = Panel(canvasGo.transform, new Rect(60 + i * 20, Height - 32, 16, 24), Color.black);
        var fade = Panel(canvasGo.transform, new Rect(Width / 2 - 20, 8, 40, 24), Color.white);
        var samples = new List<(int ink, int green)>();
        var fades = new List<int>();
        var firstSim = 0f;
        var previousTime = float.NegativeInfinity;
        string output = null;
        try
        {
            Film(a, b);
            output = Capture.FrameDir;
            var firstRender = Time.renderedFrameCount;
            yield return AsyncAssert.WaitUntil(() => Screen.width == Width && Screen.height == Height &&
                Time.renderedFrameCount > firstRender, 30f, "native Game View resolution and first render");
            Time.timeScale = 0;
            Time.captureDeltaTime = 0;
            firstSim = Time.time;
            var profile = GizmoUtility.GetGizmoInfo().Where(g => g.gizmoEnabled).ToArray();
            Assert.IsNotEmpty(profile);
            using (var cancelled = new PrivateWriter()) { }
            var start = Time.unscaledTime;
            var id = 0;
            for (var window = 0; window < 3; window++)
            {
                var enabled = window != 1;
                foreach (var annotation in profile)
                {
                    var changed = annotation;
                    changed.gizmoEnabled = enabled;
                    GizmoUtility.ApplyGizmoInfo(changed, false);
                }
                using var writer = new PrivateWriter();
                for (var frame = 0; frame < FramesPerWindow; frame++)
                {
                    id++;
                    for (var bit = 0; bit < bits.Length; bit++)
                        bits[bit].color = (id & (1 << bit)) != 0 ? Color.white : Color.black;
                    var alpha = Mathf.Clamp01((Time.unscaledTime - start) / 2f);
                    fade.color = new Color(1, 1, 1, alpha);
                    yield return new WaitForEndOfFrame();
                    var unscaled = Time.unscaledTime;
                    var sim = Time.time;
                    var filename = $"writer_{id:00000}.png";
                    writer.Write(Path.Combine(output, filename));
                    var image = new Texture2D(2, 2);
                    try
                    {
                        Assert.IsTrue(ImageConversion.LoadImage(image, File.ReadAllBytes(Path.Combine(output, filename))));
                        Assert.AreEqual(Width, image.width);
                        Assert.AreEqual(Height, image.height);
                        AssertColor(image.GetPixel(20, Height - 20), Color.red, "top-left red");
                        AssertColor(image.GetPixel(20, 20), Color.green, "bottom-left green");
                        AssertColor(image.GetPixel(Width - 20, 20), Color.blue, "bottom-right blue");
                        var pixelId = 0;
                        for (var bit = 0; bit < bits.Length; bit++)
                            if (image.GetPixel(68 + bit * 20, Height - 20).r > 0.5f) pixelId |= 1 << bit;
                        Assert.AreEqual(id, pixelId, "image identity equals metadata identity on every PNG");
                        Assert.Greater(unscaled, previousTime, "unscaled timestamps advance while simulation is frozen");
                        Assert.AreEqual(firstSim, sim, "writer does not advance simulation");
                        previousTime = unscaled;
                        var ink = 0;
                        var green = 0;
                        var nonBackground = 0;
                        var pixels = image.GetPixels32();
                        for (var y = 45; y < Height - 45; y++)
                        for (var x = 40; x < Width - 40; x++)
                        {
                            var p = pixels[y * Width + x];
                            var max = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
                            var min = Mathf.Min(p.r, Mathf.Min(p.g, p.b));
                            if (max > 24) nonBackground++;
                            if (max - min > 40 && max > 48) ink++;
                            if (p.g > p.r + 30 && p.g > p.b + 30 && p.g > 48) green++;
                        }
                        Assert.Less(nonBackground, Width * Height / 2, "presentation-off native view remains mostly background");
                        samples.Add((ink, green));
                        fades.Add(image.GetPixels32()[20 * Width + Width / 2].r);
                        rows.Add(string.Join(",", id, filename, unscaled.ToString("R", CultureInfo.InvariantCulture),
                            sim.ToString("R", CultureInfo.InvariantCulture), Time.renderedFrameCount, enabled ? 1 : 0,
                            alpha.ToString("R", CultureInfo.InvariantCulture), pixelId, ink, green));
                        File.WriteAllLines(Path.Combine(output, "writer-proof.csv"), rows);
                    }
                    finally { Object.DestroyImmediate(image); }
                    yield return null;
                }
            }
            Assert.Greater(samples.Take(FramesPerWindow).Sum(s => s.ink), 0, "gizmos initially visible");
            Assert.AreEqual(0, samples.Skip(FramesPerWindow).Take(FramesPerWindow).Sum(s => s.ink), "gizmos disabled");
            Assert.Greater(samples.Skip(FramesPerWindow * 2).Sum(s => s.ink), 0, "gizmos visible again");
            Assert.Greater(samples.Take(FramesPerWindow).Sum(s => s.green), 0, "native collider silhouette visible");
            Assert.Greater(fades.Last(), fades.First() + 25, "screen-space HUD fade progresses while sim is frozen");
            var count = Directory.GetFiles(output, "writer_*.png").Length;
            Assert.AreEqual(FramesPerWindow * 3, count);
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.AreEqual(count, Directory.GetFiles(output, "writer_*.png").Length, "synchronous writer resource disposal preserves completed output");
            for (var frame = 0; frame < 6; frame++)
            {
                yield return null;
                Capture.Step();
            }
            yield return new WaitForEndOfFrame();
            using (var referenceWriter = new PrivateWriter())
                referenceWriter.Write(Path.Combine(output, "private-colour-reference.png"));
        }
        finally
        {
            Capture.End();
            Time.timeScale = timeScale;
            Time.captureDeltaTime = captureDelta;
            Object.DestroyImmediate(canvasGo);
        }
        CollectionAssert.AreEqual(selection, Selection.objects);
        Assert.AreEqual(active, Selection.activeObject);
        Assert.AreEqual(background, Application.runInBackground);
        Assert.AreEqual(quality, QualitySettings.GetQualityLevel());
        PlayModeWindow.GetRenderingResolution(out var restoredWidth, out var restoredHeight);
        Assert.AreEqual(beforeWidth, restoredWidth);
        Assert.AreEqual(beforeHeight, restoredHeight);
        var restored = GizmoUtility.GetGizmoInfo().ToDictionary(g => g.name);
        foreach (var annotation in annotations)
        {
            Assert.AreEqual(annotation.gizmoEnabled, restored[annotation.name].gizmoEnabled, annotation.name);
            Assert.AreEqual(annotation.iconEnabled, restored[annotation.name].iconEnabled, annotation.name);
        }
        Assert.AreEqual(0, PrivateWriter.LiveResources);
        var referenceFrames = Directory.GetFiles(output, "f_*.png");
        Assert.IsNotEmpty(referenceFrames, "installed Recorder produced the colour comparison frame");
        Array.Sort(referenceFrames, StringComparer.Ordinal);
        var candidate = new Texture2D(2, 2);
        var reference = new Texture2D(2, 2);
        try
        {
            ImageConversion.LoadImage(candidate, File.ReadAllBytes(Path.Combine(output, "private-colour-reference.png")));
            ImageConversion.LoadImage(reference, File.ReadAllBytes(referenceFrames[referenceFrames.Length / 2]));
            var actual = candidate.GetPixel(Width - 62, Height - 20);
            var expected = reference.GetPixel(Width - 62, Height - 20);
            AssertColor(actual, expected, "non-endpoint RGB agrees with installed Recorder");
            Assert.That(expected.r, Is.InRange(0.1f, 0.8f));
            Assert.That(expected.g, Is.InRange(0.2f, 0.9f));
            File.WriteAllText(Path.Combine(output, "colour-parity.txt"),
                $"graphicsUVStartsAtTop={SystemInfo.graphicsUVStartsAtTop}; candidate={actual}; Recorder={expected}\n");
        }
        finally
        {
            Object.DestroyImmediate(candidate);
            Object.DestroyImmediate(reference);
        }
        File.WriteAllText(Path.Combine(output, "writer-proof-passed.txt"),
            "18 exact PNG/row identities; native gizmos on/off/on; collider silhouette; asymmetric RGB/HUD; non-endpoint Recorder colour parity; frozen sim and advancing unscaled time; resource dispose/recreate and editor restoration passed. Automatic recording-pump cancellation remains unproven.\n");
        Debug.Log("[WriterProof] passed -> " + output);
    }

    private static Image Panel(Transform parent, Rect bounds, Color color)
    {
        var root = new GameObject("ProofPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = bounds.position;
        rect.sizeDelta = bounds.size;
        var image = root.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void AssertColor(Color actual, Color expected, string corner)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.025f), corner);
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.025f), corner);
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.025f), corner);
    }

    private sealed class PrivateWriter : IDisposable
    {
        private readonly RenderTexture target = new(Width, Height, 0, RenderTextureFormat.ARGB32);
        private readonly Texture2D image = new(Width, Height, TextureFormat.RGB24, false);
        private bool disposed;
        public static int LiveResources { get; private set; }

        public PrivateWriter()
        {
            target.Create();
            LiveResources += 2;
        }

        public void Write(string path)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PrivateWriter));
            var previous = RenderTexture.active;
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(target);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    var source = image.GetPixels32();
                    var upright = new Color32[source.Length];
                    for (var y = 0; y < Height; y++)
                        Array.Copy(source, y * Width, upright, (Height - 1 - y) * Width, Width);
                    image.SetPixels32(upright);
                }
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
            LiveResources -= 2;
        }
    }
}
#endif
