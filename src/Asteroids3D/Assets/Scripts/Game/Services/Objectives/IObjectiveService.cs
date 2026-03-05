using System;
using System.Collections.Generic;
using Objectives;

namespace Game.Services
{
    public interface IObjectiveService
    {
        /// <summary>The active tracker, or null if no objective is set.</summary>
        ObjectiveTracker CurrentTracker { get; }

        /// <summary>Current objective state, or null if inactive.</summary>
        ObjectiveType? CurrentState { get; }

        /// <summary>Create and activate a new objective tracker for this sector.</summary>
        void SetObjective(
            MissionDefinition mission,
            IReadOnlyDictionary<string, Func<ObjectiveState>> builders,
            Func<bool> isPlayerAlive);

        /// <summary>Restart the current objective from the initial state.</summary>
        void Restart();

        /// <summary>Raised when the tracker transitions between states.</summary>
        event Action<ObjectiveType, ObjectiveType> OnStateChanged;

        /// <summary>Tear down the active tracker.</summary>
        void Clear();
    }
}
