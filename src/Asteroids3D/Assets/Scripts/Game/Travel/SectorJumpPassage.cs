using UnityEngine;

namespace Game.Travel
{
    public sealed class SectorJumpPassage
    {
        private readonly SectorJumpSequence sequence;
        private readonly float startingHeading;
        private readonly float headingDelta;
        private readonly Vector2 jumpDirection;
        private readonly float holdSpeed;
        private readonly float tripDistance;

        public float HoldStartsAt { get; }

        public SectorJumpPassage(SectorJumpProfile profile, float startingHeading, float gateHeading)
        {
            sequence = new SectorJumpSequence(profile);
            this.startingHeading = startingHeading;
            headingDelta = Mathf.DeltaAngle(startingHeading, gateHeading);
            var direction = gateHeading * Mathf.Deg2Rad;
            jumpDirection = new Vector2(-Mathf.Sin(direction), Mathf.Cos(direction));
            HoldStartsAt = profile.turnDuration + profile.chargeDuration + profile.travelDuration * 0.5f;
            holdSpeed = sequence.StateAt(HoldStartsAt, headingDelta).Speed;
            tripDistance = sequence.StateAt(sequence.Duration, headingDelta).Distance;
        }

        public float DurationWithHold(float holdSeconds) => sequence.Duration + holdSeconds;

        public SectorJumpState StateAt(float elapsedSeconds, float holdSeconds)
        {
            var time = Mathf.Clamp(elapsedSeconds, 0f, DurationWithHold(holdSeconds));
            var held = Mathf.Clamp(time - HoldStartsAt, 0f, holdSeconds);
            var sequenceTime = time <= HoldStartsAt ? time :
                HoldStartsAt + Mathf.Max(0f, time - HoldStartsAt - holdSeconds);
            var state = sequence.StateAt(sequenceTime, headingDelta);
            var distance = state.Distance + held * holdSpeed;
            var blend = state.TravelProgress <= 0.5f ? 0f :
                sequence.LocaleBlendAt((state.TravelProgress - 0.5f) * 2f);
            return new SectorJumpState(time, state.TravelProgress, distance / tripDistance,
                state.Speed, distance, startingHeading + state.HeadingDegrees, jumpDirection,
                blend, state.Charge, state.HudAlpha, state.ShipBlurStrength, state.ShipBlurLength);
        }
    }
}
