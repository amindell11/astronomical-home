using System;
using UnityEngine;

namespace Substrate.Services.Environment
{
    /// <summary>
    /// The palette roles a flat background's Blender sidecar exports, parsed once at the import
    /// boundary. Colours are scene-linear, as Blender wrote them.
    /// </summary>
    public sealed class FlatBackgroundSidecar
    {
        public const int SchemaVersion = 1;

        public Color Base { get; }
        public Color Primary { get; }
        public Color Secondary { get; }
        public Color Accent { get; }

        private FlatBackgroundSidecar(Color @base, Color primary, Color secondary, Color accent)
        {
            Base = @base;
            Primary = primary;
            Secondary = secondary;
            Accent = accent;
        }

        public static FlatBackgroundSidecar Parse(string json, string source)
        {
            var raw = JsonUtility.FromJson<Raw>(json);
            if (raw == null || raw.schema_version != SchemaVersion)
                throw new FormatException(
                    $"{source}: flat-background sidecar schema_version {raw?.schema_version} is not {SchemaVersion}.");
            if (raw.palette == null)
                throw new FormatException($"{source}: flat-background sidecar has no palette.");
            return new FlatBackgroundSidecar(Role(raw.palette.@base, "base", source),
                Role(raw.palette.primary, "primary", source), Role(raw.palette.secondary, "secondary", source),
                Role(raw.palette.accent, "accent", source));
        }

        private static Color Role(float[] rgb, string role, string source)
        {
            if (rgb == null || rgb.Length != 3)
                throw new FormatException($"{source}: palette role '{role}' must be three linear RGB values.");
            return new Color(rgb[0], rgb[1], rgb[2]);
        }

        // Field names mirror the sidecar's JSON keys for JsonUtility.
        [Serializable]
        private sealed class Raw
        {
            public int schema_version;
            public RawPalette palette;
        }

        [Serializable]
        private sealed class RawPalette
        {
            public float[] @base;
            public float[] primary;
            public float[] secondary;
            public float[] accent;
        }
    }
}
