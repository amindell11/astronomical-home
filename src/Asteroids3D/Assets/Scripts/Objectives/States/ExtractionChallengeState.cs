using System;

namespace Objectives.States
{
    /// <summary>
    /// Player must reach the extraction zone while not blocked by a nearby chaser.
    /// Delegates the actual check to an <see cref="IExtractionZone"/>.
    /// Optional onEnter callback fires side effects (e.g. activate chaser) when entered.
    /// </summary>
    public class ExtractionChallengeState : ObjectiveState
    {
        private readonly IExtractionZone zone;

        public override ObjectiveType StateType => ObjectiveType.ExtractionChallenge;

        public ExtractionChallengeState(IExtractionZone zone, Action onEnter = null)
            : base(onEnter)
        {
            this.zone = zone ?? throw new ArgumentNullException(nameof(zone));
        }

        public override void Tick(float deltaTime) { }

        public override bool IsComplete => zone.IsPlayerInZone;
    }
}
