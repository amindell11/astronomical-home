using System;
using System.Collections.Generic;
using System.IO;
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
        /// <summary>The recorder's naming contract, for callers that must reject a hostile name at their own boundary.</summary>
        public static bool IsSafeName(string name) => CaptureArtifacts.IsSafeName(name);

        private readonly CaptureConfig config;
        private readonly CaptureArtifacts artifacts;
        private readonly CaptureCost cost = new();
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
            artifacts = new CaptureArtifacts(config);
            frameDir = artifacts.FrameDir;

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
            cost.Sample();
            if (stepIndex++ % config.everyFixedSteps != 0) return;

            CaptureFraming.Apply(captureCamera, config, subjects);

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
            // A failed manifest write must not strand the rig for the next capture in this Editor.
            try
            {
                artifacts.Complete(cost);
            }
            finally
            {
                overlay?.Dispose();
                if (target) UnityEngine.Object.DestroyImmediate(target);
                if (readback) UnityEngine.Object.DestroyImmediate(readback);
                if (rig) UnityEngine.Object.DestroyImmediate(rig);
            }
        }

        /// <summary>Teardown safety net: destroys every root "[Capture]" object a crashed or timed-out run left behind. Never call while a recorder is live.</summary>
        public static void SweepStranded()
        {
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!go.transform.parent && go.name.StartsWith("[Capture]", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(go);
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
    }
}
