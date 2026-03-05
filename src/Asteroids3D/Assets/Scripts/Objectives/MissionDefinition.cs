using System.Collections.Generic;

namespace Objectives
{
    /// <summary>
    /// Describes the state transition table for a mission using string step IDs.
    /// String keys allow duplicate state types in a mission and leave the door open
    /// for branching transitions later.
    /// </summary>
    public class MissionDefinition
    {
        public string InitialStep { get; }
        public IReadOnlyDictionary<string, string> Transitions { get; }

        public MissionDefinition(string initialStep, Dictionary<string, string> transitions)
        {
            InitialStep = initialStep;
            Transitions = transitions == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(transitions);
        }

        public bool TryGetNext(string current, out string next) =>
            Transitions.TryGetValue(current, out next);

        /// <summary>
        /// Standard sequential single-mission flow:
        /// explore → key → extraction → extracted.
        /// Failure (player destroyed) is handled by ObjectiveTracker independently
        /// via the well-known "failed" step ID.
        /// </summary>
        public static MissionDefinition CreateDefault() => new MissionDefinition(
            "explore",
            new Dictionary<string, string>
            {
                { "explore",    "key"        },
                { "key",        "extraction" },
                { "extraction", "extracted"  }
            });
    }
}
