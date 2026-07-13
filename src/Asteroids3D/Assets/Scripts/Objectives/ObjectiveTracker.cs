using System;
using System.Collections.Generic;

namespace Objectives
{
    /// <summary>Sequential objective state machine over string step IDs; callers supply per-step builder closures capturing their runtime refs.</summary>
    public class ObjectiveTracker
    {
        private ObjectiveState current;
        private string currentStep;
        private readonly MissionDefinition mission;
        private readonly IReadOnlyDictionary<string, Func<ObjectiveState>> builders;

        /// <summary>Fires on every step transition — unlike <see cref="OnStateChanged"/>, which suppresses transitions between steps sharing an ObjectiveType.</summary>
        public event Action<string> OnStepChanged;

        public event Action<ObjectiveType, ObjectiveType> OnStateChanged;

        public ObjectiveType CurrentState => current.StateType;

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

        /// <summary>Advances at most one step per call so UI/audio hooks see every intermediate state.</summary>
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

        /// <summary>Event-driven failure (e.g. player death); no-op in a terminal state.</summary>
        public void Fail()
        {
            if (!IsTerminal(current.StateType))
                TransitionTo("failed");
        }

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

            OnStepChanged?.Invoke(currentStep);
            if (previous != current.StateType)
                OnStateChanged?.Invoke(previous, current.StateType);
        }

        private static bool IsTerminal(ObjectiveType type) =>
            type is ObjectiveType.Extracted or ObjectiveType.Failed or ObjectiveType.Completed;
    }
}
