using System;
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
            ObjectiveStateFactory factory,
            IPlayerAlive playerAlive);

        /// <summary>Tick the active tracker. Call from SectorManager's Update loop.</summary>
        void Tick(float deltaTime);

        /// <summary>Restart the current objective from the initial state.</summary>
        void Restart();

        /// <summary>Raised when the tracker transitions between states.</summary>
        event Action<ObjectiveType, ObjectiveType> OnStateChanged;

        /// <summary>Tear down the active tracker.</summary>
        void Clear();
    }
}
