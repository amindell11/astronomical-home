using UnityEngine;

namespace UI.Hangar.Hardware
{
    [CreateAssetMenu(fileName = "HangarLightProfile", menuName = "Astronomical/UI/Hangar light")]
    public sealed class HangarLightProfile : ScriptableObject
    {
        [Tooltip("Faint starlight on the painted console.")]
        [SerializeField] private Color ambient = new(0.4614f, 0.4845f, 0.5271f);

        [Tooltip("Brightness of the baked emission on the painted console.")]
        [SerializeField, Range(0f, 4f)] private float emissionStrength = 1f;

        [Tooltip("Tint on the projector lens's own glow.")]
        [SerializeField, ColorUsage(false, true)] private Color lensTint = Color.white;

        [Tooltip("Color of the light at the projector lens.")]
        [SerializeField] private Color hologramSpillColor = new(1f, .87f, .70f);

        [Tooltip("Intensity of the light at the projector lens.")]
        [SerializeField, Range(0f, 30f)] private float hologramSpill = 8.45f;

        [Tooltip("Color of the light at the launch button.")]
        [SerializeField] private Color launchSpillColor = new(1f, .87f, .70f);

        [Tooltip("Intensity of the light at the launch button.")]
        [SerializeField, Range(0f, 30f)] private float launchSpill = 2.5f;

        [Tooltip("Self-glow of the launch button.")]
        [SerializeField, ColorUsage(false, true)] private Color launchGlow = new(.9f, .6f, .3f);

        [Tooltip("Intensity of the main screen's spill light.")]
        [SerializeField, Range(0f, 30f)] private float mainScreenSpill = 4.55f;

        [Tooltip("Intensity of the detail screen's spill light.")]
        [SerializeField, Range(0f, 30f)] private float detailScreenSpill = 1.3f;

        [Tooltip("Intensity of each weapon screen's spill light.")]
        [SerializeField, Range(0f, 30f)] private float weaponScreenSpill = 1.3f;

        [Tooltip("Color behind the picture on every screen.")]
        [SerializeField] private Color displayBacking = Color.black;

        [Tooltip("Brightness of each lamp's glow card.")]
        [SerializeField, Range(0f, 4f)] private float lampGlow = 1f;

        [Tooltip("Width and height of each lamp's glow card.")]
        [SerializeField, Range(.05f, 3f)] private float lampGlowSize = .5f;

        [Tooltip("Brightness of each downlight's glow card.")]
        [SerializeField, Range(0f, 4f)] private float downlightGlow = 1f;

        [Tooltip("Width and height of each downlight's glow card.")]
        [SerializeField, Range(.05f, 4f)] private float downlightGlowSize = .9f;

        [Tooltip("Brightness of each downlight's cone.")]
        [SerializeField, Range(0f, 2f)] private float coneStrength = .3f;

        [Tooltip("How far each downlight cone reaches.")]
        [SerializeField, Range(.5f, 14f)] private float coneLength = 6f;

        [Tooltip("Width of each downlight cone at its far end.")]
        [SerializeField, Range(.2f, 8f)] private float coneWidth = 3f;

        [Tooltip("Width of each downlight cone at the lens, as a fraction of its far-end width.")]
        [SerializeField, Range(.02f, 1f)] private float coneTopShare = .18f;

        [Tooltip("How quickly each downlight cone fades along its length.")]
        [SerializeField, Range(.2f, 4f)] private float coneFalloff = 1.4f;

        public Color Ambient => ambient;
        public float EmissionStrength => emissionStrength;
        public Color LensTint => lensTint;
        public Color HologramSpillColor => hologramSpillColor;
        public float HologramSpill => hologramSpill;
        public Color LaunchSpillColor => launchSpillColor;
        public float LaunchSpill => launchSpill;
        public Color LaunchGlow => launchGlow;
        public float MainScreenSpill => mainScreenSpill;
        public float DetailScreenSpill => detailScreenSpill;
        public float WeaponScreenSpill => weaponScreenSpill;
        public Color DisplayBacking => displayBacking;
        public float LampGlow => lampGlow;
        public float LampGlowSize => lampGlowSize;
        public float DownlightGlow => downlightGlow;
        public float DownlightGlowSize => downlightGlowSize;
        public float ConeStrength => coneStrength;
        public float ConeLength => coneLength;
        public float ConeWidth => coneWidth;
        public float ConeTopShare => coneTopShare;
        public float ConeFalloff => coneFalloff;
    }
}
