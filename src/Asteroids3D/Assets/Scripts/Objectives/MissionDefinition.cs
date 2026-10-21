using System;
using System.Collections.Generic;

namespace Objectives
{
    /// <summary>
    /// Describes the state transition table for a mission using string step IDs.
    /// String keys allow duplicate state types in a mission and leave the door open
    /// for branching transitions later.
    ///
    /// An optional <see cref="FailCriteria"/> lambda is checked every tick — when it
    /// returns true the tracker transitions to the well-known "failed" step.
    /// </summary>
    public class MissionDefinition
    {
        public string InitialStep { get; }
        public IReadOnlyDictionary<string, string> Transitions { get; }

        /// <summary>
        /// Optional predicate evaluated every tick. Returns true when the mission
        /// should fail (e.g. player is dead). Null means "never auto-fail".
        /// </summary>
        public Func<bool> FailCriteria { get; }

        public MissionDefinition(
            string initialStep,
            Dictionary<string, string> transitions,
            Func<bool> failCriteria = null)
        {
            InitialStep = initialStep;
            Transitions = transitions == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(transitions);
            FailCriteria = failCriteria;
        }

        public bool TryGetNext(string current, out string next) =>
            Transitions.TryGetValue(current, out next);

        /// <summary>
        /// Standard sequential single-mission flow:
        /// explore → key → extraction → extracted.
        /// Failure criteria (e.g. player death) is supplied by the caller.
        /// </summary>
        public static MissionDefinition CreateDefault(Func<bool> failCriteria = null) => new MissionDefinition(
            "explore",
            new Dictionary<string, string>
            {
                { "explore",    "key"        },
                { "key",        "extraction" },
                { "extraction", "extracted"  }
            },
            failCriteria);
    }
}
