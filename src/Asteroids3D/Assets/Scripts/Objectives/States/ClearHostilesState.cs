namespace Objectives.States
{
    /// <summary>Consumed by ClearHostilesState to check if an encounter's wave is spawned and fully dead.</summary>
    public interface IHostileTracker
    {
        bool HostilesCleared { get; }
    }

    public class ClearHostilesState : ObjectiveState
    {
        private readonly IHostileTracker hostiles;

        public override ObjectiveType StateType => ObjectiveType.ClearHostiles;

        public ClearHostilesState(IHostileTracker hostiles)
        {
            this.hostiles = hostiles ?? throw new System.ArgumentNullException(nameof(hostiles));
        }

        public override void Tick(float deltaTime) { }

        public override bool IsComplete => hostiles.HostilesCleared;
    }
}
