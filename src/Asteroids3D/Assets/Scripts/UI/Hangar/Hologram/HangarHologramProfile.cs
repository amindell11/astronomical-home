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

        [Tooltip("Half-angle of the beam, in degrees.")]
        [SerializeField, Range(10f, 80f)] private float reachHalfAngle = 55f;

        [Tooltip("How far the beam reaches above the lens.")]
        [SerializeField, Range(1f, 30f)] private float reachLength = 12f;

        [Tooltip("The widest the beam gets, in units either side of its middle. Above that it rises as a column.")]
        [SerializeField, Range(1f, 20f)] private float reachWidth = 4.5f;

        [Tooltip("Share of the half-angle that is the soft edge band.")]
        [SerializeField, Range(.01f, .5f)] private float reachSoftness = .18f;

        [Tooltip("How hard the picture breaks up inside the edge band.")]
        [SerializeField, Range(0f, 1f)] private float sputter = .7f;

        [Tooltip("Scan lines across the surface's height.")]
        [SerializeField, Range(20f, 600f)] private float sputterLines = 220f;

        [Tooltip("Brightness of the fringe at the cone's edge.")]
        [SerializeField, Range(0f, 4f)] private float edgeFringe = 1.2f;

        [Tooltip("Half the visible beam's spread at the lens, in degrees.")]
        [SerializeField, Range(5f, 80f)] private float beamHalfAngle = 26f;

        [Tooltip("How far above the lens the visible beam's light carries, in units.")]
        [SerializeField, Range(.5f, 30f)] private float beamLength = 3.5f;

        [Tooltip("Brightness of the beam.")]
        [SerializeField, Range(0f, 3f)] private float beamLight = .6f;

        [Tooltip("How far the beam and hologram dip while a ship switch is under way.")]
        [SerializeField, Range(0f, 1f)] private float switchFlicker = .35f;

        [Tooltip("Canopy effect strengths on the hologram image.")]
        [SerializeField] private CanopySettings canopy = CanopySettings.Combat;

        [Tooltip("Cone placed at the lens; none leaves the lens bare. A unit quad with its base at the origin, which the projector scales over the reach.")]
        [SerializeField] private GameObject projectorCone;

        public MaterialRestyle Restyle => restyle;
        public Material Resolve => resolve;
        public float LayerOpacity => layerOpacity;
        public float FlickerDepth => flickerDepth;
        public float FlickerRate => flickerRate;
        public float ReachHalfAngle => reachHalfAngle;
        public float ReachLength => reachLength;
        public float ReachWidth => reachWidth;
        public float ReachSoftness => reachSoftness;
        public float Sputter => sputter;
        public float SputterLines => sputterLines;
        public float EdgeFringe => edgeFringe;
        public float BeamHalfAngle => beamHalfAngle;
        public float BeamLength => beamLength;
        public float BeamLight => beamLight;
        public float SwitchFlicker => switchFlicker;
        public CanopySettings Canopy => canopy;
        public GameObject ProjectorCone => projectorCone;

        private void OnValidate() => restyle.Validate(name);
    }
}
