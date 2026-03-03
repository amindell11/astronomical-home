using UnityEngine;

namespace Objectives.States
{
    public class ExtractionChallengeState : ObjectiveState
    {
        private readonly IPlayerPosition playerPosition;
        private readonly IExtractionZone extractionZone;
        private readonly IExtractionBlocker extractionBlocker;
        private readonly IExtractionChaserSpawner chaserSpawner;
        private readonly ObjectiveParams parameters;

        public override ObjectiveType StateType => ObjectiveType.ExtractionChallenge;

        public ExtractionChallengeState(
            IPlayerPosition playerPosition,
            IExtractionZone extractionZone,
            IExtractionBlocker extractionBlocker,
            IExtractionChaserSpawner chaserSpawner,
            ObjectiveParams parameters)
        {
            this.playerPosition = playerPosition;
            this.extractionZone = extractionZone;
            this.extractionBlocker = extractionBlocker;
            this.chaserSpawner = chaserSpawner;
            this.parameters = parameters;
        }

        public override void Enter()
        {
            chaserSpawner?.SpawnChaser();
        }

        public override void Tick(float deltaTime) { }

        public override bool IsComplete =>
            Vector3.Distance(playerPosition.Position, extractionZone.Position) <= parameters.ExtractionRadius
            && (extractionBlocker == null || !extractionBlocker.IsExtractionBlocked);

        public override float ComputeUtility() => 1f;
    }
}
