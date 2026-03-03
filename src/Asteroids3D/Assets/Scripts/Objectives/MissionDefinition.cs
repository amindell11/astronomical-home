using System.Collections.Generic;

namespace Objectives
{
    /// <summary>
    /// Describes the state transition table for a mission.
    /// Each entry maps a source state to the next state when IsComplete is true.
    /// Terminal states (Extracted, Failed) have no entry.
    /// </summary>
    public class MissionDefinition
    {
        public ObjectiveType InitialState { get; }
        public IReadOnlyDictionary<ObjectiveType, ObjectiveType> Transitions { get; }

        public MissionDefinition(ObjectiveType initialState, Dictionary<ObjectiveType, ObjectiveType> transitions)
        {
            InitialState = initialState;
            Transitions = transitions;
        }

        public bool TryGetNext(ObjectiveType current, out ObjectiveType next) =>
            Transitions.TryGetValue(current, out next);

        /// <summary>
        /// Standard sequential single-mission flow:
        /// Explore -> KeyAcquired -> ExtractionChallenge -> Extracted.
        /// Failure (player destroyed) is handled by ObjectiveTracker independently.
        /// </summary>
        public static MissionDefinition CreateDefault() => new MissionDefinition(
            ObjectiveType.Explore,
            new Dictionary<ObjectiveType, ObjectiveType>
            {
                { ObjectiveType.Explore,              ObjectiveType.KeyAcquired         },
                { ObjectiveType.KeyAcquired,          ObjectiveType.ExtractionChallenge },
                { ObjectiveType.ExtractionChallenge,  ObjectiveType.Extracted           }
            });
    }
}
