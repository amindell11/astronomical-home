using UnityEngine;

namespace Game.Travel
{
    public readonly struct SectorJumpState
    {
        public float Seconds { get; }
        public float TravelProgress { get; }
        public float TravelFraction { get; }
        public float Speed { get; }
        public float Distance { get; }
        public float HeadingDegrees { get; }
        public Vector2 JumpDirection { get; }
        public float LocaleBlend { get; }
        public float Charge { get; }
        public float HudAlpha { get; }
        public float ShipBlurStrength { get; }
        public float ShipBlurLength { get; }

        internal SectorJumpState(float seconds, float travelProgress, float travelFraction, float speed,
            float distance, float headingDegrees, Vector2 jumpDirection, float localeBlend, float charge,
            float hudAlpha, float shipBlurStrength, float shipBlurLength)
        {
            Seconds = seconds;
            TravelProgress = travelProgress;
            TravelFraction = travelFraction;
            Speed = speed;
            Distance = distance;
            HeadingDegrees = headingDegrees;
            JumpDirection = jumpDirection;
            LocaleBlend = localeBlend;
            Charge = charge;
            HudAlpha = hudAlpha;
            ShipBlurStrength = shipBlurStrength;
            ShipBlurLength = shipBlurLength;
        }
    }
}
