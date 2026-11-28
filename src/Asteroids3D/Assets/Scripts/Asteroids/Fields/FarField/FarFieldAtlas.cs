using System.Collections.Generic;
using UnityEngine;

namespace Asteroids.Fields.FarField
{
    /// <summary>
    /// The baked far-field sprite set: a billboard material whose textures hold one cell per
    /// (mesh, view) — rows are mesh indices, columns are <see cref="FarFieldViews"/> — and each mesh's
    /// sprite half-extent at scale 1. Written by the editor bake from the spawn settings' prefab.
    /// </summary>
    public class FarFieldAtlas : ScriptableObject
    {
        [SerializeField] private Material material;
        [SerializeField] private float[] halfSizes;

        [Tooltip("How long a far sprite fades out after its chunk's real rocks spawn, in unscaled seconds.")]
        [SerializeField] private float handOffSeconds = 0.4f;

        public Material Material => material;
        public IReadOnlyList<float> HalfSizes => halfSizes;
        public float HandOffSeconds => handOffSeconds;
        public int Views => FarFieldViews.Count;

        internal void Bind(Material bakedMaterial, float[] bakedHalfSizes)
        {
            material = bakedMaterial;
            halfSizes = bakedHalfSizes;
        }
    }
}
