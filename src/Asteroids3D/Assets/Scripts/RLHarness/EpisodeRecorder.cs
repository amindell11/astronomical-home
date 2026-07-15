using System.IO;
using Game;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Opt-in overhead frame capture for episode runs (record.flag): renders an orthographic follow view of both ships to PNG frames on demand, for offline GIF/video assembly. Requires a graphics device — editor watch lane only, never -nographics batch.</summary>
    public sealed class EpisodeRecorder : System.IDisposable
    {
        private const int Width = 960;
        private const int Height = 540;
        private const float MinHalfHeight = 22f;
        private const float FramePadding = 12f;
        private const float CameraHeight = 60f;

        private readonly string frameDir;
        private readonly GameObject rig;
        private readonly Camera camera;
        private readonly RecorderOverlay overlay;
        private readonly RenderTexture target;
        private readonly Texture2D readback;
        private int frameIndex;

        public string FrameDir => frameDir;

        public EpisodeRecorder(string outputRoot, string runStamp, int episodeIndex)
        {
            frameDir = Path.Combine(outputRoot, "frames", $"{runStamp}-ep{episodeIndex:D2}");
            Directory.CreateDirectory(frameDir);

            rig = new GameObject("[EpisodeRecorder]") { hideFlags = HideFlags.HideAndDontSave };
            camera = rig.AddComponent<Camera>();
            camera.orthographic = true;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.05f);

            var light = new GameObject("[EpisodeRecorderLight]") { hideFlags = HideFlags.HideAndDontSave };
            light.transform.SetParent(rig.transform);
            var directional = light.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.transform.rotation = Quaternion.LookRotation(
                GamePlane.Rotation * new Vector3(0.4f, -0.3f, 1f));

            target = new RenderTexture(Width, Height, 24);
            readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            overlay = new RecorderOverlay(rig.transform);
        }

        public void CaptureFrame(Ship a, Ship b)
        {
            if (!a || !b) return;
            overlay.Sync(a, b);

            var posA = GamePlane.WorldPointToPlane(a.transform.position);
            var posB = GamePlane.WorldPointToPlane(b.transform.position);
            var mid = 0.5f * (posA + posB);
            var separation = (posA - posB).magnitude;

            var normal = GamePlane.Rotation * Vector3.forward;
            camera.transform.position = GamePlane.PlanePointToWorld(mid) + normal * CameraHeight;
            camera.transform.rotation = Quaternion.LookRotation(-normal, GamePlane.Rotation * Vector3.up);
            var halfWidthNeeded = (0.5f * separation + FramePadding) * Height / (float)Width;
            camera.orthographicSize = Mathf.Max(MinHalfHeight, halfWidthNeeded);

            camera.targetTexture = target;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            RenderTexture.active = previous;
            camera.targetTexture = null;

            File.WriteAllBytes(
                Path.Combine(frameDir, $"f_{frameIndex++:D5}.png"),
                readback.EncodeToPNG());
        }

        public void Dispose()
        {
            overlay?.Dispose();
            if (target) Object.DestroyImmediate(target);
            if (readback) Object.DestroyImmediate(readback);
            if (rig) Object.DestroyImmediate(rig);
        }
    }
}
