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
        private readonly float holdDistance;
        private readonly float exitDuration;
        private readonly float remainingTravel;
        private readonly float arrival;
        private readonly float departure;
        private readonly float travelDuration;

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
            holdDistance = sequence.StateAt(HoldStartsAt, headingDelta).Distance;
            tripDistance = sequence.StateAt(sequence.Duration, headingDelta).Distance;
            exitDuration = profile.worldBlendDuration;
            remainingTravel = profile.travelDuration * 0.5f;
            arrival = profile.turnDuration + profile.chargeDuration + profile.travelDuration;
            departure = profile.turnDuration + profile.chargeDuration;
            travelDuration = profile.travelDuration;
        }

        public float DurationWithHold(float holdSeconds) =>
            sequence.Duration - remainingTravel + exitDuration + holdSeconds;

        public float ExitStartsAt(float holdSeconds) => HoldStartsAt + holdSeconds;

        public float ExitProgress(float elapsedSeconds, float holdSeconds) =>
            Mathf.Clamp01((elapsedSeconds - ExitStartsAt(holdSeconds)) / exitDuration);

        public float WorldBlendAt(float elapsedSeconds, float holdSeconds)
        {
            var held = Mathf.Clamp(elapsedSeconds - HoldStartsAt, 0f, holdSeconds);
            return SectorJumpProfile.Ease((elapsedSeconds - held - departure) / exitDuration) *
                (1f - SectorJumpProfile.Ease(ExitProgress(elapsedSeconds, holdSeconds)));
        }

        public SectorJumpState StateAt(float elapsedSeconds, float holdSeconds)
        {
            var time = Mathf.Clamp(elapsedSeconds, 0f, DurationWithHold(holdSeconds));
            var held = Mathf.Clamp(time - HoldStartsAt, 0f, holdSeconds);
            var exitTime = Mathf.Max(0f, time - ExitStartsAt(holdSeconds));
            var sequenceTime = time <= HoldStartsAt ? time : exitTime < exitDuration
                ? HoldStartsAt + exitTime * remainingTravel / exitDuration
                : arrival + exitTime - exitDuration;
            var state = sequence.StateAt(sequenceTime, headingDelta);
            var distance = (time <= HoldStartsAt ? state.Distance : holdDistance +
                (state.Distance - holdDistance) * exitDuration / remainingTravel) + held * holdSpeed;
            var blend = sequence.LocaleBlendAt(Mathf.Clamp01((time - departure) / travelDuration));
            return new SectorJumpState(time, state.TravelProgress, distance / tripDistance,
                state.Speed, distance, startingHeading + state.HeadingDegrees, jumpDirection,
                blend, state.Charge, state.HudAlpha, state.ShipBlurStrength, state.ShipBlurLength);
        }
    }
}
