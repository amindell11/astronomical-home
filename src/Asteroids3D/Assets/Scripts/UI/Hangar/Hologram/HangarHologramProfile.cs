using Ships.Presentation;
using UI.Projection;
using UnityEngine;

namespace UI.Hangar.Hologram
{
    /// <summary>
    /// Dresses hangar preview copies through its restyle. <c>_DebrisVisibility</c> (swoop fade) and
    /// <c>_ContourPixels</c> (ink scale) stay driven per copy, so a counterpart shader that wants them
    /// declares those names.
    /// </summary>
    [CreateAssetMenu(menuName = "Astronomical/UI/Hangar hologram")]
    public sealed class HangarHologramProfile : ScriptableObject
    {
        [Tooltip("Swaps each preview copy's materials for hologram counterparts and carries the ship's per-renderer values.")]
        [SerializeField] private MaterialRestyle restyle = new();

        [Tooltip("Full-screen material that turns the stage's picture of the ship into the hologram.")]
        [SerializeField] private Material resolve;

        [Tooltip("Opacity of the hologram image on the projector surface.")]
        [SerializeField, Range(0f, 1f)] private float layerOpacity = .75f;

        [Tooltip("How far the hologram and cone dim when they flicker.")]
        [SerializeField, Range(0f, .5f)] private float flickerDepth = .05f;

        [Tooltip("Flickers per second.")]
        [SerializeField, Range(0f, 30f)] private float flickerRate = 9f;

        [Tooltip("Canopy effect strengths on the hologram image.")]
        [SerializeField] private CanopySettings canopy = CanopySettings.Combat;

        [Tooltip("Cone placed at the lens; none leaves the lens bare. A unit quad with its base at the origin, which the projector stretches to the settled ship.")]
        [SerializeField] private GameObject projectorCone;

        public MaterialRestyle Restyle => restyle;
        public Material Resolve => resolve;
        public float LayerOpacity => layerOpacity;
        public float FlickerDepth => flickerDepth;
        public float FlickerRate => flickerRate;
        public CanopySettings Canopy => canopy;
        public GameObject ProjectorCone => projectorCone;

        private void OnValidate() => restyle.Validate(name);
    }
}
