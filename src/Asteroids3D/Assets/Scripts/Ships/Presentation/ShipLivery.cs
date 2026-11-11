using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ships.Presentation
{
    /// <summary>The colours and shaders a ship wears over its authored paint, pushed once at spawn through <see cref="ShipVisualRig.Wear"/>.</summary>
    public readonly struct ShipLivery
    {
        /// <summary>Every hull material slot in silhouette order; empty keeps the authored paint.</summary>
        public readonly IReadOnlyList<Material> Hull;

        /// <summary>Null keeps the authored flame.</summary>
        public readonly Material Flame;

        public readonly Color ShieldRing;

        /// <summary>Null keeps the authored shaders.</summary>
        public readonly MaterialRestyle Restyle;

        public ShipLivery(IReadOnlyList<Material> hull, Material flame, Color shieldRing, MaterialRestyle restyle = null)
        {
            Hull = hull ?? Array.Empty<Material>();
            Flame = flame;
            ShieldRing = shieldRing;
            Restyle = restyle;
        }
    }

    /// <summary>A rig part that owns some of the hardware a livery recolours.</summary>
    public interface IWearsLivery
    {
        void Wear(in ShipLivery livery);
    }
}
