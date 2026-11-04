using System;
using Ships;
using UnityEngine;

namespace Substrate.Sectors.Factions
{
    [Serializable]
    public class FactionSkin
    {
        [Tooltip("Each hull material a faction ship carries, and the faction's material in its place. Empty → today's paint.")]
        public MaterialSwap[] swaps = Array.Empty<MaterialSwap>();

        [Tooltip("Swaps the elite wears over the faction's, keyed by the same hull materials.")]
        public MaterialSwap[] elite = Array.Empty<MaterialSwap>();

        public void Dress(Ship ship, bool isElite)
        {
            if (swaps.Length == 0 && (!isElite || elite.Length == 0)) return;

            foreach (var hull in ship.VisualRig.Silhouette)
            {
                var materials = hull.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                    materials[i] = Swap(materials[i], isElite);
                hull.sharedMaterials = materials;
            }
        }

        private Material Swap(Material original, bool isElite)
        {
            if (isElite && TryFind(elite, original, out var accent)) return accent;
            return TryFind(swaps, original, out var faction) ? faction : original;
        }

        private static bool TryFind(MaterialSwap[] table, Material original, out Material replacement)
        {
            foreach (var swap in table)
            {
                if (swap.from != original) continue;
                replacement = swap.to;
                return true;
            }
            replacement = null;
            return false;
        }
    }

    [Serializable]
    public struct MaterialSwap
    {
        public Material from;
        public Material to;
    }
}
