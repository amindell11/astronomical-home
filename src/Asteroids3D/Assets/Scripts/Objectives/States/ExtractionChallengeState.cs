using System;
using UnityEngine;

namespace Objectives.States
{
    /// <summary>
    /// Player must reach the extraction zone while not blocked by a nearby chaser.
    /// No side effects on Enter — chaser spawning is a reaction in CombatSectorManager.
    /// </summary>
    public class ExtractionChallengeState : ObjectiveState
    {
        private readonly Func<Vector3> playerPos;
        private readonly Func<Vector3> zonePos;
        private readonly Func<bool> isBlocked;
        private readonly float extractionRadius;

        public override ObjectiveType StateType => ObjectiveType.ExtractionChallenge;

        public ExtractionChallengeState(
            Func<Vector3> playerPos,
            Func<Vector3> zonePos,
            Func<bool> isBlocked,
            float extractionRadius)
        {
            this.playerPos = playerPos ?? throw new ArgumentNullException(nameof(playerPos));
            this.zonePos = zonePos ?? throw new ArgumentNullException(nameof(zonePos));
            this.isBlocked = isBlocked;
            this.extractionRadius = extractionRadius;
        }

        public override void Tick(float deltaTime) { }

        public override bool IsComplete =>
            Vector3.Distance(playerPos(), zonePos()) <= extractionRadius
            && (isBlocked == null || !isBlocked());
    }
}
