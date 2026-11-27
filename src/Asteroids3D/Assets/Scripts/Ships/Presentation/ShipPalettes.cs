using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ships.Presentation
{
    /// <summary>
    /// One chassis's hull palettes. Each palette lists that chassis's region materials, and a
    /// material's name is its region: wearing a palette swaps every hull slot whose material shares a
    /// name with one of the palette's materials, so a re-export that reorders slots keeps its paint.
    /// </summary>
    [CreateAssetMenu(fileName = "Palettes", menuName = "Ships/Palettes")]
    public sealed class ShipPalettes : ScriptableObject
    {
        [Serializable]
        public struct Palette
        {
            [Tooltip("The name a faction's locale palettes pick this palette by, e.g. 'shoal-rust'.")]
            public string name;

            [Tooltip("One material per region this palette paints, each named for its region.")]
            public Material[] regions;
        }

        [SerializeField] private Palette[] palettes = Array.Empty<Palette>();

        public bool Has(string palette) => palettes.Any(entry => entry.name == palette);

        public IReadOnlyList<Material> Regions(string palette)
        {
            foreach (var entry in palettes)
                if (entry.name == palette)
                    return entry.regions;
            throw new InvalidOperationException($"Palettes '{name}' have no palette '{palette}'.");
        }

        /// <summary>The first region <paramref name="silhouette"/> wears, as any of these palettes names regions, that <paramref name="palette"/> does not paint; null when it paints them all.</summary>
        public string MissingRegion(string palette, IEnumerable<Renderer> silhouette)
        {
            var regions = palettes.SelectMany(entry => entry.regions).Select(region => region.name).ToHashSet();
            var painted = Regions(palette).Select(region => region.name).ToHashSet();
            return silhouette.SelectMany(surface => surface.sharedMaterials)
                .Select(material => material.name)
                .FirstOrDefault(region => regions.Contains(region) && !painted.Contains(region));
        }
    }
}
