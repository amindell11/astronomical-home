using System;

namespace Capture
{
    /// <summary>
    /// Preset names are the provenance source. Observer numbers are the authored Main Camera
    /// prefab, not the camera class defaults; an EditMode test fails if that prefab drifts.
    /// Lineup numbers are the ship-lineup neutral key.
    /// </summary>
    public static class CapturePreset
    {
        public const string GameTopdown = "game-topdown";
        public const string LineupNeutral = "lineup-neutral";
        public const string Hud1280x720 = "hud-1280x720";

        public const float ObserverPadding = 5f;
        public const float ObserverMinZoom = 10f;
        public const float ObserverMaxZoom = 17f;
        public static readonly UnityEngine.Vector3 ObserverPlaneOffset = new(0f, 0f, -50f);

        public const float LineupIntensity = 1.6f;
        public static readonly UnityEngine.Vector3 LineupEuler = new(20f, -25f, 0f);
        public const float LineupAmbient = 0.24f;

        public static void Apply(CaptureConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrEmpty(config.preset)) return;
            switch (config.preset)
            {
                case GameTopdown:
                    config.framing = CaptureFramingMode.Observer;
                    break;
                case LineupNeutral:
                    break;
                case Hud1280x720:
                    config.width = 1280;
                    config.height = 720;
                    break;
                default:
                    throw new ArgumentException($"[Capture] unknown preset '{config.preset}'.", nameof(config));
            }
        }
    }
}
