using System;
using Objectives.States;

namespace Objectives
{
    public class ObjectiveStateFactory
    {
        private readonly IKeyTracker keyTracker;
        private readonly IPlayerPosition playerPosition;
        private readonly IExtractionZone extractionZone;
        private readonly IExtractionBlocker extractionBlocker;
        private readonly IExtractionChaserSpawner chaserSpawner;
        private readonly ObjectiveParams parameters;

        public ObjectiveStateFactory(
            IKeyTracker keyTracker,
            IPlayerPosition playerPosition,
            IExtractionZone extractionZone,
            IExtractionBlocker extractionBlocker,
            IExtractionChaserSpawner chaserSpawner,
            ObjectiveParams parameters)
        {
            this.keyTracker = keyTracker;
            this.playerPosition = playerPosition;
            this.extractionZone = extractionZone;
            this.extractionBlocker = extractionBlocker;
            this.chaserSpawner = chaserSpawner;
            this.parameters = parameters;
        }

        public ObjectiveState Create(ObjectiveType type) => type switch
        {
            ObjectiveType.Explore             => new ExploreState(keyTracker),
            ObjectiveType.KeyAcquired         => new KeyAcquiredState(),
            ObjectiveType.ExtractionChallenge => new ExtractionChallengeState(playerPosition, extractionZone, extractionBlocker, chaserSpawner, parameters),
            ObjectiveType.Extracted           => new ExtractedState(),
            ObjectiveType.Failed              => new FailedState(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}
