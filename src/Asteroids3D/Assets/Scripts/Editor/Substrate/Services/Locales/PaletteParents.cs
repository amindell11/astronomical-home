using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Substrate.Services.Locales
{
    /// <summary>
    /// Writes a flat background's palette parents: one generated Material Variant per palette-bound
    /// sky layer, beside the sidecar, whose only overrides are palette-mapped colours. A locale's sky
    /// layer materials vary these, so a reimport refreshes inherited colours while native overrides
    /// survive, and native Revert returns a property to the palette. Owns the provisional role →
    /// property mapping. Design: arc #678.
    /// </summary>
    public static class PaletteParents
    {
        private static readonly Binding[] Bindings =
        {
            new("FarNebula", "Assets/Visuals/Locales/Sky/NebulaMaterial.mat",
                palette => palette.Secondary, palette => palette.Primary),
            new("CloseNebula", "Assets/Visuals/Locales/Sky/ForegroundNebulaMaterial.mat",
                palette => palette.Secondary, palette => palette.Accent),
        };

        public static string PathFor(string sidecarPath, string layer) =>
            $"{Path.ChangeExtension(sidecarPath, null)}.{layer}.mat";

        public static void Write(string sidecarPath, FlatBackgroundSidecar palette)
        {
            foreach (var binding in Bindings)
            {
                var shared = AssetDatabase.LoadAssetAtPath<Material>(binding.SharedPath);
                if (!shared)
                    throw new InvalidOperationException($"Shared sky layer material {binding.SharedPath} is missing.");
                var path = PathFor(sidecarPath, binding.Layer);
                var parent = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!parent)
                {
                    // A clone matches its parent everywhere, so parenting it records no override.
                    parent = new Material(shared) { parent = shared };
                    AssetDatabase.CreateAsset(parent, path);
                }
                // Material colours are authored in sRGB; the sidecar's roles are scene-linear.
                parent.SetColor("_NebulaCool", binding.Cool(palette).gamma);
                parent.SetColor("_NebulaWarm", binding.Warm(palette).gamma);
                AssetDatabase.SaveAssetIfDirty(parent);
            }
        }

        private readonly struct Binding
        {
            public readonly string Layer;
            public readonly string SharedPath;
            public readonly Func<FlatBackgroundSidecar, Color> Cool;
            public readonly Func<FlatBackgroundSidecar, Color> Warm;

            public Binding(string layer, string sharedPath, Func<FlatBackgroundSidecar, Color> cool,
                Func<FlatBackgroundSidecar, Color> warm)
            {
                Layer = layer;
                SharedPath = sharedPath;
                Cool = cool;
                Warm = warm;
            }
        }
    }
}
