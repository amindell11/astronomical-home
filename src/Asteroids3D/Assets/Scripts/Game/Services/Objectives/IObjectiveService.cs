using System;
using System.Collections.Generic;
using Objectives;
using UnityEngine;

namespace Game.Services
{
    public interface IObjectiveService
    {
        ObjectiveTracker SpineTracker { get; }

        ObjectiveType? SpineState { get; }

        // Producers (encounters) report the spine target; UI subscribers (the minimap marker) read it.
        Transform SpineTarget { get; }

        void SetSpineTarget(Transform target);

        event Action<Transform> OnSpineTargetChanged;

        void SetSpineObjective(
            MissionDefinition mission,
            IReadOnlyDictionary<string, Func<ObjectiveState>> builders);

        void FailSpine();

        void RestartSpine();

        event Action<ObjectiveType, ObjectiveType> OnSpineStateChanged;

        void ClearSpine();

        LocalObjectiveHandle OpenLocal(
            MissionDefinition mission,
            IReadOnlyDictionary<string, Func<ObjectiveState>> builders,
            Transform target = null);

        IReadOnlyList<LocalObjectiveHandle> Locals { get; }

        event Action OnLocalsChanged;

        void ClearAll();
    }
}
