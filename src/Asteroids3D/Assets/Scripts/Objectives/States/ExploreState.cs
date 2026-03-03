namespace Objectives.States
{
    /// <summary>
    /// Player explores the map to find and pick up the key.
    /// Transitions to KeyAcquired when the key is collected.
    /// </summary>
    public class ExploreState : ObjectiveState
    {
        private readonly IKeyTracker keyTracker;

        public override ObjectiveType StateType => ObjectiveType.Explore;

        public ExploreState(IKeyTracker keyTracker)
        {
            this.keyTracker = keyTracker;
        }

        public override void Tick(float deltaTime) { }

        /// <summary>Exploration ends the moment the player picks up the key.</summary>
        public override bool IsComplete => keyTracker.PlayerHasKey;

        public override float ComputeUtility() => keyTracker.PlayerHasKey ? 0f : 1f;
    }
}
