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
            this.playerPosition = playerPosition ?? throw new System.ArgumentNullException(nameof(playerPosition));
            this.extractionZone = extractionZone ?? throw new System.ArgumentNullException(nameof(extractionZone));
            this.extractionBlocker = extractionBlocker;
            this.chaserSpawner = chaserSpawner;
            this.parameters = parameters ?? throw new System.ArgumentNullException(nameof(parameters));
        }

        public override void Enter()
        {
            chaserSpawner?.SpawnChaser();
        }

        public override void Tick(float deltaTime) { }

        public override bool IsComplete =>
            Vector3.Distance(playerPosition.Position, extractionZone.Position) <= parameters.ExtractionRadius
            && (extractionBlocker == null || !extractionBlocker.IsExtractionBlocked);
    }
}
