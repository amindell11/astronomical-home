using UnityEngine;

namespace Game.Travel
{
    public static class SectorJumpShader
    {
        public static void Apply(Material material, SectorJumpProfile profile, SectorJumpState state,
            float aspect, float pixelHeight, int seed)
        {
            var angle = state.HeadingDegrees * Mathf.Deg2Rad;
            material.SetVector("_Direction", new Vector4(state.JumpDirection.x, state.JumpDirection.y,
                Mathf.Cos(angle), Mathf.Sin(angle)));
            material.SetVector("_Motion", new Vector4(state.Speed, state.Distance * profile.streakSpeed,
                state.LocaleBlend, aspect));
            material.SetVector("_Streaks", new Vector4(profile.streakDensity, profile.streakLength,
                profile.streakWidthPixels / pixelHeight, profile.streakBrightness));
            material.SetVector("_StreakDistribution", new Vector4(profile.streakVariation,
                profile.streakSpread, profile.streakFan * Mathf.Deg2Rad, 0f));
            material.SetInteger("_Seed", seed);
            material.SetColor("_StreakColor", profile.streakColor);
            material.SetFloat("_RawBackgroundBlend", profile.rawBackgroundBlend);
            material.SetVector("_Nebula", new Vector4(profile.nebulaStretch, profile.nebulaSoftness,
                profile.nebulaDrift * state.Speed, profile.vignetteStrength));
            material.SetVector("_Ship", new Vector4(profile.shipImageHeight *
                (1f + profile.chargeZoom * state.Charge - profile.travelPullback * state.Speed),
                state.ShipBlurStrength, state.ShipBlurLength, profile.shipCoreSharpness));
            material.SetVector("_Exhaust", new Vector4(profile.exhaustLength,
                profile.exhaustBrightness * (state.Charge * 0.3f + state.Speed), profile.shipBlurFalloff, 0f));
            material.SetColor("_ExhaustColor", profile.exhaustColor);
        }
    }
}
