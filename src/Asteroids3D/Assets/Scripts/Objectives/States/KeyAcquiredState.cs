namespace Objectives.States
{
    /// <summary>
    /// Momentary acknowledgment state entered when the key is collected.
    /// Immediately complete — the tracker transitions to ExtractionChallenge on the next Tick.
    /// UI/audio hooks can observe OnStateChanged to play a fanfare or banner.
    /// </summary>
    public class KeyAcquiredState : ObjectiveState
    {
        public override ObjectiveType StateType => ObjectiveType.KeyAcquired;

        public override void Tick(float deltaTime) { }

        /// <summary>Always true — this state is a momentary transition marker.</summary>
        public override bool IsComplete => true;
    }
}
