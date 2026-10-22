using UnityEngine;

namespace Game.Travel
{
    [CreateAssetMenu(menuName = "Game/Sector Jump Profile")]
    public sealed class SectorJumpProfile : ScriptableObject
    {
        [Header("Timing, in seconds")]
        [Min(0.01f)] public float turnDuration = 0.55f;
        [Min(0f)] public float chargeDuration = 0.18f;
        [Min(0.01f)] public float travelDuration = 1.8f;
        [Min(0.01f)] public float settleDuration = 0.45f;
        public AnimationCurve speed = new(new Keyframe(0, 0), new Keyframe(0.22f, 1),
            new Keyframe(0.62f, 1), new Keyframe(1, 0));

        [Header("Background")]
        [Tooltip("0 keeps the softened treatment; 1 shows the moving locale directly.")]
        [Range(0f, 1f)] public float rawBackgroundBlend = 0.35f;
        [Min(0f)] public float skyTravelDistance = 800f;
        [Header("Additional star streaks")]
        [Range(4, 100)] public int streakDensity = 38;
        [Range(0.01f, 0.6f)] public float streakLength = 0.22f;
        [Range(0.3f, 3f)] public float streakWidthPixels = 0.8f;
        [Range(0f, 3f)] public float streakBrightness;
        [Min(0f)] public float streakSpeed = 0.75f;
        public Color streakColor = new(0.55f, 0.78f, 1f);
        [Header("Background shaping")]
        [Range(0f, 4f)] public float nebulaStretch = 0.8f;
        [Range(0f, 0.4f)] public float nebulaSoftness = 0.12f;
        [Range(0f, 0.2f)] public float nebulaDrift = 0.035f;
        public AnimationCurve localeBlend = AnimationCurve.EaseInOut(0.2f, 0, 0.85f, 1);

        [Header("Ship blur, independent of the background")]
        [Range(0f, 2f)] public float shipBlurStrength = 0.75f;
        [Tooltip("Trail length measured in ship-image heights.")]
        [Range(0f, 5f)] public float shipBlurLength = 1.4f;
        [Range(0.5f, 6f)] public float shipBlurFalloff = 2f;
        [Range(0f, 1f)] public float shipCoreSharpness = 0.85f;
        public AnimationCurve shipBlur = new(new Keyframe(0, 0), new Keyframe(0.2f, 1),
            new Keyframe(0.65f, 1), new Keyframe(1, 0));

        [Header("Framing and exhaust")]
        [Range(0.08f, 0.35f)] public float shipImageHeight = 0.18f;
        [Range(0f, 0.3f)] public float chargeZoom = 0.08f;
        [Range(0f, 0.5f)] public float travelPullback = 0.18f;
        [Range(0f, 2f)] public float exhaustLength = 0.65f;
        [Range(0f, 4f)] public float exhaustBrightness = 1.2f;
        public Color exhaustColor = new(1f, 0.18f, 0.06f);
        [Range(0f, 0.6f)] public float vignetteStrength = 0.14f;

        [Header("HUD and ship status bars")]
        [Min(0.01f)] public float hudFadeOut = 0.3f;
        [Min(0.01f)] public float hudFadeIn = 0.35f;

        public float Duration => turnDuration + chargeDuration + travelDuration + settleDuration;

        public float HudAlpha(float time)
        {
            var arrival = turnDuration + chargeDuration + travelDuration;
            return time < arrival ? 1f - Ease(time / hudFadeOut) : Ease((time - arrival) / hudFadeIn);
        }

        public static float Ease(float time)
        {
            var t = Mathf.Clamp01(time);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }
    }
}
