using System;
using System.Collections.Generic;
using Objectives;
using UnityEngine;

namespace Game.Services
{
    public interface IObjectiveService
    {
        /// <summary>The active tracker, or null if no objective is set.</summary>
        ObjectiveTracker CurrentTracker { get; }

        /// <summary>Current objective state, or null if inactive.</summary>
        ObjectiveType? CurrentState { get; }

        /// <summary>
        /// Transform the active objective points at (e.g. a key, an extraction zone), or null.
        /// Producers (encounters) report it; UI subscribers (the minimap marker) read it.
        /// </summary>
        Transform CurrentTarget { get; }

        /// <summary>Set the current objective target, raising <see cref="OnTargetChanged"/> on change.</summary>
        void SetTarget(Transform target);

        /// <summary>Raised when <see cref="CurrentTarget"/> changes.</summary>
        event Action<Transform> OnTargetChanged;

        /// <summary>Create and activate a new objective tracker for this sector.</summary>
        void SetObjective(
            MissionDefinition mission,
            IReadOnlyDictionary<string, Func<ObjectiveState>> builders);

        /// <summary>Immediately fail the current objective (event-driven failure).</summary>
        void Fail();

        /// <summary>Restart the current objective from the initial state.</summary>
        void Restart();

        /// <summary>Raised when the tracker transitions between states.</summary>
        event Action<ObjectiveType, ObjectiveType> OnStateChanged;

        /// <summary>Tear down the active tracker.</summary>
        void Clear();
    }
}
