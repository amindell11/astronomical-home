using System;

namespace Objectives
{
    /// <summary>
    /// Drives the sequential objective state machine for a single mission encounter.
    /// Flow: Explore -> KeyAcquired -> ExtractionChallenge -> Extracted (success) or Failed (player destroyed).
    ///
    /// Architecture: inject only the specific interfaces each state needs via ObjectiveStateFactory.
    /// No god-class context. Caller owns Tick() and wires IPlayerAlive for failure detection.
    /// </summary>
    public class ObjectiveTracker
    {
        private ObjectiveState current;
        private readonly MissionDefinition mission;
        private readonly ObjectiveStateFactory factory;
        private readonly IPlayerAlive playerAlive;

        /// <summary>Raised when the tracker transitions between states.</summary>
        public event Action<ObjectiveType, ObjectiveType> OnStateChanged;

        public ObjectiveType CurrentState => current.StateType;

        public ObjectiveTracker(
            MissionDefinition mission,
            ObjectiveStateFactory factory,
            IPlayerAlive playerAlive)
        {
            this.mission = mission ?? throw new ArgumentNullException(nameof(mission));
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.playerAlive = playerAlive ?? throw new ArgumentNullException(nameof(playerAlive));

            current = factory.Create(mission.InitialState);
            current.Enter();
        }

        /// <summary>
        /// Advance the state machine by one game tick.
        /// No-op when in a terminal state (Extracted or Failed).
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (IsTerminal(current.StateType))
                return;

            if (!playerAlive.IsAlive)
            {
                TransitionTo(ObjectiveType.Failed);
                return;
            }

            current.Tick(deltaTime);

            if (current.IsComplete && mission.TryGetNext(current.StateType, out var next))
                TransitionTo(next);
        }

        /// <summary>
        /// Restart the encounter from the initial state.
        /// Safe to call from both Extracted and Failed terminal states.
        /// No consequences — same encounter restarts identically.
        /// </summary>
        public void Restart()
        {
            TransitionTo(mission.InitialState);
        }

        private void TransitionTo(ObjectiveType next)
        {
            var previous = current.StateType;
            current.Exit();
            current = factory.Create(next);
            current.Enter();
            OnStateChanged?.Invoke(previous, next);
        }

        private static bool IsTerminal(ObjectiveType type) =>
            type == ObjectiveType.Extracted || type == ObjectiveType.Failed;
    }
}
