using UnityEngine;

namespace Game.Travel
{
    public static class SectorJumpPreview
    {
        public static void Apply(Material material, SectorJumpProfile profile, float time, float heading,
            float aspect, float pixelHeight)
        {
            var departure = profile.turnDuration + profile.chargeDuration;
            var travel = Mathf.Clamp01((time - departure) / profile.travelDuration);
            var moving = time >= departure && time < departure + profile.travelDuration;
            var speed = moving ? Mathf.Clamp01(profile.speed.Evaluate(travel)) : 0f;
            var blur = moving ? Mathf.Clamp01(profile.shipBlur.Evaluate(travel)) : 0f;
            var charge = SectorJumpProfile.Ease(time / departure) *
                (1f - SectorJumpProfile.Ease((time - departure) / 0.25f));
            var distance = Distance(profile, travel);
            var angle = heading * SectorJumpProfile.Ease(time / profile.turnDuration) * Mathf.Deg2Rad;
            var direction = heading * Mathf.Deg2Rad;
            material.SetVector("_Direction", new Vector4(-Mathf.Sin(direction), Mathf.Cos(direction),
                Mathf.Cos(angle), Mathf.Sin(angle)));
            material.SetVector("_Motion", new Vector4(speed, distance * profile.streakSpeed,
                Mathf.Clamp01(profile.localeBlend.Evaluate(travel)), aspect));
            material.SetVector("_Streaks", new Vector4(profile.streakDensity, profile.streakLength,
                profile.streakWidthPixels / pixelHeight, profile.streakBrightness));
            material.SetColor("_StreakColor", profile.streakColor);
            material.SetFloat("_RawBackgroundBlend", profile.rawBackgroundBlend);
            material.SetVector("_Nebula", new Vector4(profile.nebulaStretch, profile.nebulaSoftness,
                profile.nebulaDrift * speed, profile.vignetteStrength));
            material.SetVector("_Ship", new Vector4(profile.shipImageHeight *
                (1f + profile.chargeZoom * charge - profile.travelPullback * speed),
                blur * profile.shipBlurStrength, profile.shipBlurLength, profile.shipCoreSharpness));
            material.SetVector("_Exhaust", new Vector4(profile.exhaustLength,
                profile.exhaustBrightness * (charge * 0.3f + speed), profile.shipBlurFalloff, 0));
            material.SetColor("_ExhaustColor", profile.exhaustColor);
        }

        public static float TravelFraction(SectorJumpProfile profile, float time)
        {
            var travel = Mathf.Clamp01((time - profile.turnDuration - profile.chargeDuration) / profile.travelDuration);
            var total = Distance(profile, 1);
            return total > 0 ? Distance(profile, travel) / total : 0;
        }

        private static float Distance(SectorJumpProfile profile, float travel)
        {
            var distance = 0f;
            const int samples = 64;
            for (var index = 0; index < samples; index++)
                distance += Mathf.Clamp01(profile.speed.Evaluate(travel * (index + 0.5f) / samples));
            return distance * travel / samples * profile.travelDuration;
        }
    }
}
