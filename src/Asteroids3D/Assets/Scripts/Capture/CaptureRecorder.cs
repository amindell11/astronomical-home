using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Capture
{
    [Serializable]
    public sealed class CaptureConfig
    {
        public string outputRoot = "results/capture";
        public string clipName = "clip";
        /// <summary>Shared across clips of one run; null → stamped at recorder construction.</summary>
        public string runStamp;
        public int width = 960;
        public int height = 540;
        /// <summary>Capture cadence in fixed steps. 5 → 0.1 s of sim per frame at the default 0.02 fixed dt, real-time playback at 10 fps.</summary>
        public int everyFixedSteps = 5;
        public float minHalfHeight = 22f;
        public float padding = 12f;
    }

    /// <summary>Overhead orthographic frame capture for agent-facing game inspection: auto-frames the given subjects each captured step, composites the <see cref="CaptureDraw"/> overlay, and writes PNG frames plus a manifest for offline mp4/gif assembly (scripts/capture/assemble.py). Needs a graphics device — run via -WithGraphics, never -nographics.</summary>
    public sealed class CaptureRecorder : IDisposable
    {
        private const float CameraHeight = 60f;
        private static readonly Regex SafeName = new("^[A-Za-z0-9_-]+$");

        private readonly CaptureConfig config;
        private readonly string frameDir;
        private readonly GameObject rig;
        private readonly Camera captureCamera;
        private readonly CaptureDraw overlay;
        private readonly RenderTexture target;
        private readonly Texture2D readback;
        private int stepIndex;
        private int frameCount;

        public string FrameDir => frameDir;
        public int FrameCount => frameCount;

        public CaptureRecorder(CaptureConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            Validate(config);

            var stamp = string.IsNullOrEmpty(config.runStamp)
                ? DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                : config.runStamp;
            var outputRoot = Path.IsPathRooted(config.outputRoot)
                ? config.outputRoot
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", config.outputRoot));
            frameDir = Path.Combine(outputRoot, "frames", $"{stamp}-{config.clipName}");
            if (Directory.Exists(frameDir) && Directory.GetFiles(frameDir, "f_*.png").Length > 0)
                throw new InvalidOperationException(
                    $"[Capture] frame dir already contains frames (pick a distinct clipName/runStamp): {frameDir}");
            Directory.CreateDirectory(frameDir);
            WriteManifest();

            rig = new GameObject("[Capture] Rig");
            captureCamera = rig.AddComponent<Camera>();
            captureCamera.enabled = false;
            captureCamera.orthographic = true;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = new Color(0.02f, 0.02f, 0.05f);

            var lightRig = new GameObject("light");
            lightRig.transform.SetParent(rig.transform);
            var directional = lightRig.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.transform.rotation = Quaternion.LookRotation(
                GamePlane.Rotation * new Vector3(0.4f, -0.3f, 1f));

            target = new RenderTexture(config.width, config.height, 24);
            readback = new Texture2D(config.width, config.height, TextureFormat.RGB24, false);
            overlay = new CaptureDraw(rig.transform, captureCamera);
        }

        /// <summary>Call once per fixed step. Captures every <see cref="CaptureConfig.everyFixedSteps"/> steps; the draw callback runs only on captured steps, so skipped steps do no scene queries.</summary>
        public void Step(IReadOnlyList<Vector2> subjects, Action<CaptureDraw> draw = null)
        {
            if (stepIndex++ % config.everyFixedSteps != 0) return;

            ValidateSubjects(subjects);
            FrameSubjects(subjects);

            overlay.BeginFrame();
            if (draw != null)
            {
                try
                {
                    draw(overlay);
                }
                catch
                {
                    overlay.DisableAll();
                    throw;
                }
            }
            overlay.DisableUnused();

            Render();
            frameCount++;
        }

        public void Dispose()
        {
            overlay?.Dispose();
            if (target) UnityEngine.Object.DestroyImmediate(target);
            if (readback) UnityEngine.Object.DestroyImmediate(readback);
            if (rig) UnityEngine.Object.DestroyImmediate(rig);
        }

        /// <summary>Teardown safety net: destroys every root "[Capture]" object a crashed or timed-out run left behind. Never call while a recorder is live.</summary>
        public static void SweepStranded()
        {
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!go.transform.parent && go.name.StartsWith("[Capture]", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(go);
        }

        private static void Validate(CaptureConfig config)
        {
            if (config.width <= 0 || config.height <= 0 || config.width % 2 != 0 || config.height % 2 != 0)
                throw new ArgumentException($"[Capture] dimensions must be positive and even (yuv420p), got {config.width}x{config.height}");
            if (config.everyFixedSteps <= 0)
                throw new ArgumentException($"[Capture] everyFixedSteps must be > 0, got {config.everyFixedSteps}");
            if (config.minHalfHeight < 0f || config.padding < 0f)
                throw new ArgumentException($"[Capture] framing values must be nonnegative, got minHalfHeight={config.minHalfHeight} padding={config.padding}");
            if (string.IsNullOrEmpty(config.outputRoot))
                throw new ArgumentException("[Capture] outputRoot must be set");
            if (string.IsNullOrEmpty(config.clipName) || !SafeName.IsMatch(config.clipName))
                throw new ArgumentException($"[Capture] clipName must be filesystem-safe [A-Za-z0-9_-], got '{config.clipName}'");
            if (!string.IsNullOrEmpty(config.runStamp) && !SafeName.IsMatch(config.runStamp))
                throw new ArgumentException($"[Capture] runStamp must be filesystem-safe [A-Za-z0-9_-], got '{config.runStamp}'");
        }

        // Loud rejection of degenerate subjects: a silent black/misframed clip costs a full re-run to diagnose.
        private static void ValidateSubjects(IReadOnlyList<Vector2> subjects)
        {
            if (subjects == null || subjects.Count == 0)
                throw new ArgumentException("[Capture] Step needs at least one subject to frame");
            for (var i = 0; i < subjects.Count; i++)
            {
                var s = subjects[i];
                if (!float.IsFinite(s.x) || !float.IsFinite(s.y))
                    throw new ArgumentException($"[Capture] subject {i} is not finite ({s}) — was its source destroyed?");
            }
        }

        private void FrameSubjects(IReadOnlyList<Vector2> subjects)
        {
            var min = subjects[0];
            var max = subjects[0];
            for (var i = 1; i < subjects.Count; i++)
            {
                min = Vector2.Min(min, subjects[i]);
                max = Vector2.Max(max, subjects[i]);
            }

            var center = 0.5f * (min + max);
            var xExtent = 0.5f * (max.x - min.x);
            var yExtent = 0.5f * (max.y - min.y);
            var halfHeight = Mathf.Max(
                yExtent + config.padding,
                (xExtent + config.padding) * config.height / config.width,
                config.minHalfHeight);

            var normal = GamePlane.Rotation * Vector3.forward;
            captureCamera.transform.position = GamePlane.PlanePointToWorld(center) + normal * CameraHeight;
            captureCamera.transform.rotation = Quaternion.LookRotation(-normal, GamePlane.Rotation * Vector3.up);
            captureCamera.orthographicSize = halfHeight;
        }

        private void Render()
        {
            overlay.SetVisible(true);
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(captureCamera, request))
                {
                    RenderPipeline.SubmitRenderRequest(captureCamera, request);
                }
                else
                {
                    captureCamera.targetTexture = target;
                    captureCamera.Render();
                    captureCamera.targetTexture = null;
                }
            }
            finally
            {
                overlay.SetVisible(false);
            }

            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, config.width, config.height), 0, 0);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            File.WriteAllBytes(Path.Combine(frameDir, $"f_{frameCount:D5}.png"), readback.EncodeToPNG());
        }

        private void WriteManifest()
        {
            var manifest = new Manifest
            {
                width = config.width,
                height = config.height,
                everyFixedSteps = config.everyFixedSteps,
                fixedDeltaTime = Time.fixedDeltaTime,
                suggestedFps = 1f / (Time.fixedDeltaTime * config.everyFixedSteps),
            };
            File.WriteAllText(Path.Combine(frameDir, "manifest.json"), JsonUtility.ToJson(manifest, true));
        }

        [Serializable]
        private sealed class Manifest
        {
            public int width;
            public int height;
            public int everyFixedSteps;
            public float fixedDeltaTime;
            public float suggestedFps;
        }
    }
}
