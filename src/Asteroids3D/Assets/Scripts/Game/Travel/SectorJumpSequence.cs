using System;
using UnityEngine;

namespace Game.Travel
{
    public sealed class SectorJumpSequence
    {
        private const int Segments = 256;
        private readonly float[] speeds = new float[Segments + 1];
        private readonly float[] distances = new float[Segments + 1];
        private readonly float turnDuration;
        private readonly float departure;
        private readonly float travelDuration;
        private readonly float arrival;
        private readonly float hudFadeOut;
        private readonly float hudFadeIn;
        private readonly float blurStrength;
        private readonly float blurLength;
        private readonly float blendStart;
        private readonly float blendEnd;

        public float Duration { get; }

        // SmoothStep is the Hermite of AnimationCurve.EaseInOut's flat-tangent keys.
        public float LocaleBlendAt(float progress) => progress >= blendEnd ? 1f : progress <= blendStart ? 0f :
            Mathf.SmoothStep(0f, 1f, (progress - blendStart) / (blendEnd - blendStart));

        public SectorJumpSequence(SectorJumpProfile profile)
        {
            if (!profile) throw new ArgumentNullException(nameof(profile));
            if (!(profile.turnDuration > 0f) || !(profile.chargeDuration >= 0f) ||
                !(profile.travelDuration > 0f) || !(profile.settleDuration > 0f) ||
                !(profile.hudFadeOut > 0f) || !(profile.hudFadeIn > 0f))
                throw new ArgumentException("Jump durations must be positive; charge may be zero.", nameof(profile));

            turnDuration = profile.turnDuration;
            departure = turnDuration + profile.chargeDuration;
            travelDuration = profile.travelDuration;
            arrival = departure + travelDuration;
            Duration = profile.Duration;
            hudFadeOut = profile.hudFadeOut;
            hudFadeIn = profile.hudFadeIn;
            blurStrength = profile.shipBlurStrength;
            blurLength = profile.shipBlurLength;
            blendStart = profile.localeBlendStart;
            blendEnd = profile.localeBlendEnd;

            for (var index = 0; index <= Segments; index++)
            {
                speeds[index] = Mathf.Max(0f, profile.speed.Evaluate((float)index / Segments));
                if (index > 0)
                    distances[index] = distances[index - 1] +
                        (speeds[index - 1] + speeds[index]) * 0.5f * travelDuration / Segments;
            }
            if (!(distances[Segments] > 0f))
                throw new ArgumentException("Jump speed must produce positive travel distance.", nameof(profile));
        }

        public SectorJumpState StateAt(float seconds, float heading)
        {
            var time = Mathf.Clamp(seconds, 0f, Duration);
            var travel = Mathf.Clamp01((time - departure) / travelDuration);
            var sample = travel * Segments;
            var index = Mathf.Min(Mathf.FloorToInt(sample), Segments - 1);
            var fraction = sample - index;
            var speed = Mathf.LerpUnclamped(speeds[index], speeds[index + 1], fraction);
            var distance = distances[index] + fraction * (speeds[index] + speed) *
                0.5f * travelDuration / Segments;
            if (time < departure || time >= arrival) speed = 0f;
            var angle = heading * SectorJumpProfile.Ease(time / turnDuration);
            var direction = heading * Mathf.Deg2Rad;
            var charge = SectorJumpProfile.Ease(time / departure) *
                (1f - SectorJumpProfile.Ease((time - departure) / 0.25f));
            var hudAlpha = time < arrival
                ? 1f - SectorJumpProfile.Ease(time / hudFadeOut)
                : SectorJumpProfile.Ease((time - arrival) / hudFadeIn);
            return new SectorJumpState(time, travel, distance / distances[Segments], speed, distance,
                angle, new Vector2(-Mathf.Sin(direction), Mathf.Cos(direction)),
                LocaleBlendAt(travel), charge, hudAlpha,
                speed * blurStrength, speed * blurLength);
        }
    }
}
