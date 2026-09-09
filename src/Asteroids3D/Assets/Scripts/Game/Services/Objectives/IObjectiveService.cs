using System;
using System.Collections.Generic;
using Objectives;
using UnityEngine;

namespace Game.Services.Objectives
{
    public interface IObjectiveService
    {
        ObjectiveTracker SpineTracker { get; }

        ObjectiveType? SpineState { get; }

        string SpineStep { get; }

        // The spine owner mutates the target through its handle; UI subscribers (the minimap marker) read it here.
        Transform SpineTarget { get; }

        event Action<Transform> OnSpineTargetChanged;

        SpineObjectiveHandle SetSpineObjective(
            MissionDefinition mission,
            IReadOnlyDictionary<string, Func<ObjectiveState>> builders,
            Transform target = null);

        event Action<ObjectiveType, ObjectiveType> OnSpineStateChanged;

        // Step-level (string ids); fires for same-type step transitions and on install with the initial step.
        event Action<string> OnSpineStepChanged;

        LocalObjectiveHandle OpenLocal(
            MissionDefinition mission,
            IReadOnlyDictionary<string, Func<ObjectiveState>> builders,
            Transform target = null);

        IReadOnlyList<LocalObjectiveHandle> Locals { get; }

        event Action OnLocalsChanged;

        void ClearAll();
    }
}
