using UnityEngine;
using UnityEngine.Serialization;

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
        [SerializeField, Range(0f, 200f)] private float hologramSpill = 8.45f;

        [Tooltip("Color of the light at the launch button.")]
        [SerializeField] private Color launchSpillColor = new(1f, .87f, .70f);

        [Tooltip("Intensity of the light at the launch button.")]
        [SerializeField, Range(0f, 200f)] private float launchSpill = 2.5f;

        [Tooltip("Self-glow of the launch button.")]
        [SerializeField, ColorUsage(false, true)] private Color launchGlow = new(.9f, .6f, .3f);

        [Tooltip("Intensity of the main screen's spill light.")]
        [SerializeField, Range(0f, 200f)] private float mainScreenSpill = 4.55f;

        [Tooltip("Intensity of the detail screen's spill light.")]
        [SerializeField, Range(0f, 200f)] private float detailScreenSpill = 1.3f;

        [Tooltip("Intensity of each weapon screen's spill light.")]
        [SerializeField, Range(0f, 200f)] private float weaponScreenSpill = 1.3f;

        [Tooltip("Color behind the picture on every screen.")]
        [SerializeField] private Color displayBacking = Color.black;

        [Tooltip("Brightness of each lamp's glow card.")]
        [SerializeField, Range(0f, 4f)] private float lampGlow = 1f;

        [Tooltip("Extra width and height around each lamp's lens.")]
        [SerializeField, Range(.05f, 3f)] private float lampGlowSize = .5f;

        [Tooltip("Brightness of each overhead light's glow card.")]
        [SerializeField, Range(0f, 4f), FormerlySerializedAs("downlightGlow")] private float overheadGlow = 1f;

        [Tooltip("Extra width and height around each overhead light's lens.")]
        [SerializeField, Range(.05f, 4f), FormerlySerializedAs("downlightGlowSize")] private float overheadGlowSize = .9f;

        [Tooltip("How strongly the overhead lights shine on the console.")]
        [SerializeField, Range(0f, 60f)] private float overheadLight = 6f;

        [Tooltip("Brightness of the soft glow on the projector lens.")]
        [SerializeField, Range(0f, 4f)] private float lensGlow = 1f;

        [Tooltip("Width and height of the projector lens glow.")]
        [SerializeField, Range(.2f, 6f)] private float lensGlowSize = 2f;

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
        public float OverheadGlow => overheadGlow;
        public float OverheadGlowSize => overheadGlowSize;
        public float OverheadLight => overheadLight;
        public float LensGlow => lensGlow;
        public float LensGlowSize => lensGlowSize;
    }
}
