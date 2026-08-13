using System;
using System.Collections.Generic;
using Game.Services;
using Ships;

namespace Game.Diagnostics
{
    /// <summary>What a painter factory is handed: the two subject ships and the projectile service the harness composed. Held instead of an <c>EpisodePair</c> so the contract stays in Game.Core, below the RL harness.</summary>
    public readonly struct PainterContext
    {
        public readonly Ship a;
        public readonly Ship b;
        public readonly IProjectileService projectiles;

        public PainterContext(Ship a, Ship b, IProjectileService projectiles)
        {
            this.a = a;
            this.b = b;
            this.projectiles = projectiles;
        }
    }

    /// <summary>The painter name registry — the selection grammar behind RL_HARNESS_PAINTERS. Every diagnostic now draws as a native gizmo, so nothing registers here and every name fails at the boundary.</summary>
    public static class DiagnosticPainters
    {
        private static readonly Dictionary<string, Func<PainterContext, IDiagnosticPainter>> Factories = new();

        // Presets share the atom namespace so one grammar selects both.
        private static readonly Dictionary<string, string[]> Presets = BuildPresets();

        public static string RegisteredNames => string.Join(", ", Factories.Keys);
        public static string PresetNames => string.Join(", ", Presets.Keys);

        public static bool IsRegistered(string name) => name != null && Factories.ContainsKey(name);

        public static bool TryExpandPreset(string name, out string[] atoms)
        {
            atoms = null;
            return name != null && Presets.TryGetValue(name, out atoms);
        }

        public static IDiagnosticPainter Create(string name, in PainterContext context) =>
            Factories.TryGetValue(name, out var factory)
                ? factory(context)
                : throw new ArgumentException($"No painter named '{name}'; registered painters: {RegisteredNames}.");

        // Internal so the duplicate-name failure stays testable without mutating the live registry.
        internal static Dictionary<string, string[]> BuildPresets(params (string name, string[] atoms)[] entries)
        {
            var presets = new Dictionary<string, string[]>();
            foreach (var (name, atoms) in entries)
            {
                if (Factories.ContainsKey(name) || presets.ContainsKey(name))
                    throw new InvalidOperationException(
                        $"Preset name '{name}' duplicates a registered painter or preset.");
                presets[name] = atoms;
            }
            return presets;
        }
    }
}
