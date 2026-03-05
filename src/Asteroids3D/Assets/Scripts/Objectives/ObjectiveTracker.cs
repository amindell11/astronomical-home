using System;
using System.Collections.Generic;

namespace Objectives
{
    /// <summary>
    /// Drives the sequential objective state machine for a single mission encounter.
    /// Uses string step IDs and a builder dictionary instead of enum-keyed factory.
    ///
    /// Architecture: caller builds a Dictionary&lt;string, Func&lt;ObjectiveState&gt;&gt; with
    /// closures capturing runtime refs. No god-class context or factory needed.
    /// </summary>
    public class ObjectiveTracker
    {
        private ObjectiveState current;
        private string currentStep;
        private readonly MissionDefinition mission;
        private readonly IReadOnlyDictionary<string, Func<ObjectiveState>> builders;
        /// <summary>Raised when the tracker transitions between states.</summary>
        public event Action<ObjectiveType, ObjectiveType> OnStateChanged;

        /// <summary>The ObjectiveType of the active state (for UI/diagnostics).</summary>
        public ObjectiveType CurrentState => current.StateType;

        /// <summary>The string step ID of the active state.</summary>
        public string CurrentStep => currentStep;

        public ObjectiveTracker(
            MissionDefinition mission,
            IReadOnlyDictionary<string, Func<ObjectiveState>> builders)
        {
            this.mission = mission ?? throw new ArgumentNullException(nameof(mission));
            this.builders = builders ?? throw new ArgumentNullException(nameof(builders));

            currentStep = mission.InitialStep;
            current = builders[currentStep]();
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

            if (mission.FailCriteria != null && mission.FailCriteria())
            {
                TransitionTo("failed");
                return;
            }

            current.Tick(deltaTime);

            if (current.IsComplete && mission.TryGetNext(currentStep, out var next))
                TransitionTo(next);
        }

        /// <summary>
        /// Immediately transition to the "failed" step.
        /// Use for event-driven failure (e.g. player death).
        /// No-op if already in a terminal state.
        /// </summary>
        public void Fail()
        {
            if (!IsTerminal(current.StateType))
                TransitionTo("failed");
        }

        /// <summary>
        /// Restart the encounter from the initial state.
        /// Safe to call from both Extracted and Failed terminal states.
        /// </summary>
        public void Restart()
        {
            TransitionTo(mission.InitialStep);
        }

        private void TransitionTo(string nextStep)
        {
            var previous = current.StateType;
            current.Exit();
            currentStep = nextStep;
            current = builders[nextStep]();
            current.Enter();

            if (previous != current.StateType)
                OnStateChanged?.Invoke(previous, current.StateType);
        }

        private static bool IsTerminal(ObjectiveType type) =>
            type == ObjectiveType.Extracted || type == ObjectiveType.Failed;
    }
}
