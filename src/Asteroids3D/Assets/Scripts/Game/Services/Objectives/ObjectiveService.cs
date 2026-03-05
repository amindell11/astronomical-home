using System;
using System.Collections.Generic;
using Objectives;
using UnityEngine;

namespace Game.Services
{
    /// <summary>
    /// MonoBehaviour service that owns the active ObjectiveTracker.
    /// Ticks itself via Update — sectors don't need to call Tick manually.
    /// Implements IObjectiveTrackerAdapter so diagnostics can subscribe.
    /// </summary>
    public class ObjectiveService : MonoBehaviour, IObjectiveService, IObjectiveTrackerAdapter
    {
        public ObjectiveTracker CurrentTracker { get; private set; }
        public ObjectiveType? CurrentState => CurrentTracker?.CurrentState;

        ObjectiveType IObjectiveTrackerAdapter.CurrentState => CurrentTracker?.CurrentState ?? ObjectiveType.Explore;

        public event Action<ObjectiveType, ObjectiveType> OnStateChanged;

        public void SetObjective(
            MissionDefinition mission,
            IReadOnlyDictionary<string, Func<ObjectiveState>> builders)
        {
            Clear();

            CurrentTracker = new ObjectiveTracker(mission, builders);
            CurrentTracker.OnStateChanged += ForwardStateChanged;
        }

        private void Update()
        {
            CurrentTracker?.Tick(Time.deltaTime);
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
