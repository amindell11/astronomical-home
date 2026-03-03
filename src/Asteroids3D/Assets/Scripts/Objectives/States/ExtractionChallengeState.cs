using UnityEngine;

namespace Objectives.States
{
    public class ExtractionChallengeState : ObjectiveState
    {
        private readonly IPlayerPosition playerPosition;
        private readonly IExtractionZone extractionZone;
        private readonly ObjectiveParams parameters;

        public override ObjectiveType StateType => ObjectiveType.ExtractionChallenge;

        public ExtractionChallengeState(IPlayerPosition playerPosition, IExtractionZone extractionZone, ObjectiveParams parameters)
        {
            this.playerPosition = playerPosition;
            this.extractionZone = extractionZone;
            this.parameters = parameters;
        }

        public override void Tick(float deltaTime) { }

        public override bool IsComplete =>
            Vector3.Distance(playerPosition.Position, extractionZone.Position) < parameters.ExtractionRadius;

        public override float ComputeUtility() => 1f;
    }
}
