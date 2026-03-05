using System;
using System.Collections.Generic;
using Objectives;

namespace Game.Services
{
    /// <summary>
    /// Owns the active ObjectiveTracker and implements IObjectiveTrackerAdapter
    /// so diagnostics can subscribe without knowing the concrete service.
    /// </summary>
    public class ObjectiveService : IObjectiveService, IObjectiveTrackerAdapter
    {
        public ObjectiveTracker CurrentTracker { get; private set; }
        public ObjectiveType? CurrentState => CurrentTracker?.CurrentState;

        // IObjectiveTrackerAdapter — returns Explore as default when no tracker is active
        ObjectiveType IObjectiveTrackerAdapter.CurrentState => CurrentTracker?.CurrentState ?? ObjectiveType.Explore;

        public event Action<ObjectiveType, ObjectiveType> OnStateChanged;

        public void SetObjective(
            MissionDefinition mission,
            IReadOnlyDictionary<string, Func<ObjectiveState>> builders,
            Func<bool> isPlayerAlive)
        {
            Clear();

            CurrentTracker = new ObjectiveTracker(mission, builders, isPlayerAlive);
            CurrentTracker.OnStateChanged += ForwardStateChanged;
        }

        public void Tick(float deltaTime)
        {
            CurrentTracker?.Tick(deltaTime);
        }

        public void Restart()
        {
            CurrentTracker?.Restart();
        }

        public void Clear()
        {
            if (CurrentTracker != null)
            {
                CurrentTracker.OnStateChanged -= ForwardStateChanged;
                CurrentTracker = null;
            }
        }

        private void ForwardStateChanged(ObjectiveType from, ObjectiveType to)
        {
            OnStateChanged?.Invoke(from, to);
        }
    }
}
