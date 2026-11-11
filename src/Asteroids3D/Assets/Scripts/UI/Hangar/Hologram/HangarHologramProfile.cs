using System;
using Ships.Presentation;
using UnityEngine;

namespace UI.Hangar.Hologram
{
    /// <summary>
    /// Dresses hangar preview copies through its restyle. <c>_DebrisVisibility</c> (swoop fade) and
    /// <c>_ContourPixels</c> (ink scale) stay driven per copy, so a counterpart shader that wants them
    /// declares those names. The cone prefab is authored in framing-radius units with its base at the origin.
    /// </summary>
    [CreateAssetMenu(menuName = "Astronomical/UI/Hangar hologram")]
    public sealed class HangarHologramProfile : ScriptableObject
    {
        [Tooltip("Swaps each preview copy's materials for hologram counterparts and carries the ship's per-renderer values.")]
        [SerializeField] private MaterialRestyle restyle = new();

        [Tooltip("Opacity of the stage image on the projector surface.")]
        [SerializeField, Range(0f, 1f)] private float layerOpacity = .75f;

        [Tooltip("Cone placed at the lens. Authored in framing-radius units with its base at the origin.")]
        [SerializeField] private GameObject projectorCone;

        public MaterialRestyle Restyle => restyle;
        public float LayerOpacity => layerOpacity;
        public GameObject ProjectorCone => projectorCone;

        private void OnValidate()
        {
            restyle.Validate(name);
            if (!projectorCone)
                throw new InvalidOperationException($"'{name}' needs a projector cone.");
        }
    }
}
