using UnityEngine;

namespace Game.Travel
{
    public sealed class SectorJumpMotion
    {
        private readonly float turn;
        private readonly float charge;
        private readonly float departureDistance;
        private readonly float worldBlend;
        private readonly float arrivalDuration;
        private readonly float topSpeed;
        private readonly float arrivalSpeed;

        public SectorJumpMotion(SectorJumpProfile profile, float shipMaxSpeed)
        {
            turn = profile.turnDuration;
            charge = profile.chargeDuration;
            departureDistance = profile.departureDistance;
            worldBlend = profile.worldBlendDuration;
            arrivalDuration = worldBlend + profile.settleDuration;
            topSpeed = shipMaxSpeed;
            arrivalSpeed = shipMaxSpeed * profile.arrivalSpeedFraction;
        }

        public float DepartureSpeedLimit => 1.875f * departureDistance / worldBlend;

        public Vector3 DepartureVelocity(float seconds, Vector3 incoming, Vector3 direction)
        {
            var braking = charge > 0f ? SectorJumpProfile.Ease((seconds - turn) / charge)
                : seconds >= turn ? 1f : 0f;
            var t = Mathf.Clamp01((seconds - turn - charge) / worldBlend);
            var acceleration = 30f * t * t * (t - 1f) * (t - 1f);
            return incoming * (1f - braking) + direction * (departureDistance * acceleration / worldBlend);
        }

        public float ArrivalSpeedAt(float seconds) =>
            Mathf.Lerp(topSpeed, arrivalSpeed, SectorJumpProfile.Ease(seconds / arrivalDuration));

        public float ArrivalDistanceAt(float seconds)
        {
            var time = Mathf.Max(0f, seconds);
            var t = Mathf.Clamp01(time / arrivalDuration);
            var integral = t * t * t * t * (t * t - 3f * t + 2.5f);
            return topSpeed * Mathf.Min(time, arrivalDuration) +
                   (arrivalSpeed - topSpeed) * arrivalDuration * integral +
                   arrivalSpeed * Mathf.Max(0f, time - arrivalDuration);
        }
    }
}
