using System;

namespace Objectives
{
    public interface IObjectiveTrackerAdapter
    {
        event Action<ObjectiveType, ObjectiveType> OnStateChanged;
        ObjectiveType CurrentState { get; }
    }
}
